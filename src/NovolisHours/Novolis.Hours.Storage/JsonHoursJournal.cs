using System.Collections.Immutable;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Append-only journal backed by the Novolis JSON repository provider.</summary>
public sealed class JsonHoursJournal : IHoursJournal, IHoursJournalBatch
{
    private readonly IRepository<HoursJournalDocument> repository;
    private readonly HoursChangeFeed changeFeed;
    private readonly SemaphoreSlim writeGate = new(1, 1);

    /// <summary>Initializes a JSON-backed journal.</summary>
    public JsonHoursJournal(
        IRepository<HoursJournalDocument> repository,
        HoursChangeFeed changeFeed)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.changeFeed = changeFeed ?? throw new ArgumentNullException(nameof(changeFeed));
    }

    /// <inheritdoc />
    public async ValueTask AppendAsync(HoursEvent entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AppendCoreAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask AppendBatchAsync(
        IReadOnlyCollection<HoursEvent> batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0)
        {
            throw new ArgumentException("A journal batch cannot be empty.", nameof(batch));
        }

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var materialized = batch.ToArray();
            var envelope = HoursJournalBatch.Create(materialized);
            if (materialized.Any(item => ExistingEventIds().Contains(item.Id)))
            {
                throw new InvalidOperationException(
                    "A journal batch contains an event identity that already exists.");
            }

            await repository.UpsertAsync(
                new HoursJournalDocument(envelope.Id, envelope),
                cancellationToken).ConfigureAwait(false);
            foreach (var item in materialized)
            {
                changeFeed.Publish(item);
            }
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async ValueTask AppendCoreAsync(
        HoursEvent entry,
        CancellationToken cancellationToken)
    {
        if (ExistingEventIds().Contains(entry.Id))
        {
            throw new InvalidOperationException($"Journal event '{entry.Id}' already exists.");
        }

        await repository.UpsertAsync(
            new HoursJournalDocument(entry.Id, entry),
            cancellationToken).ConfigureAwait(false);
        foreach (var expanded in HoursJournalBatch.Expand(entry))
        {
            changeFeed.Publish(expanded);
        }
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(repository.All()
            .Select(item => item.Event)
            .SelectMany(HoursJournalBatch.Expand)
            .Where(item => string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(repository.All()
            .Select(item => item.Event)
            .SelectMany(HoursJournalBatch.Expand)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }

    private HashSet<Guid> ExistingEventIds() =>
        repository.All()
            .Select(item => item.Event)
            .SelectMany(HoursJournalBatch.Expand)
            .Select(entry => entry.Id)
            .ToHashSet();
}
