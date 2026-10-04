using System.Collections.Immutable;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Append-only Hours journal backed by Azure Table Storage or Azurite.</summary>
public sealed class AzureHoursJournal(
    IRepository<AzureHoursJournalDocument> repository,
    HoursChangeFeed changeFeed) : IHoursJournal, IHoursJournalBatch
{
    private readonly IRepository<AzureHoursJournalDocument> repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly HoursChangeFeed changeFeed =
        changeFeed ?? throw new ArgumentNullException(nameof(changeFeed));
    private readonly SemaphoreSlim writeGate = new(1, 1);

    /// <inheritdoc />
    public async ValueTask AppendAsync(
        HoursEvent entry,
        CancellationToken cancellationToken = default)
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
                AzureHoursJournalDocument.FromEvent(envelope),
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

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(repository.All()
            .Select(item => item.ToEvent())
            .SelectMany(HoursJournalBatch.Expand)
            .Where(item => string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(repository.All()
            .Select(item => item.ToEvent())
            .SelectMany(HoursJournalBatch.Expand)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToImmutableArray());
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
            AzureHoursJournalDocument.FromEvent(entry),
            cancellationToken).ConfigureAwait(false);
        foreach (var expanded in HoursJournalBatch.Expand(entry))
        {
            changeFeed.Publish(expanded);
        }
    }

    private HashSet<Guid> ExistingEventIds() =>
        repository.All()
            .Select(item => item.ToEvent())
            .SelectMany(HoursJournalBatch.Expand)
            .Select(entry => entry.Id)
            .ToHashSet();
}
