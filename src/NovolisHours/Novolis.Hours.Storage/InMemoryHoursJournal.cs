using System.Collections.Immutable;
using Novolis.Hours.Domain;

namespace Novolis.Hours.Storage;

/// <summary>Thread-safe in-memory append-only journal used by full-application feature tests.</summary>
public sealed class InMemoryHoursJournal : IHoursJournal, IHoursJournalBatch
{
    private readonly object gate = new();
    private ImmutableArray<HoursEvent> entries = [];
    private readonly HoursChangeFeed? changeFeed;

    /// <summary>Initializes the journal and optionally emits every durable append into the shared change feed.</summary>
    public InMemoryHoursJournal(HoursChangeFeed? changeFeed = null)
    {
        this.changeFeed = changeFeed;
    }

    /// <inheritdoc />
    public ValueTask AppendAsync(HoursEvent entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (ExistingEventIds().Contains(entry.Id))
            {
                throw new InvalidOperationException($"Journal event '{entry.Id}' already exists.");
            }

            entries = entries.Add(entry);
        }

        foreach (var expanded in HoursJournalBatch.Expand(entry))
        {
            changeFeed?.Publish(expanded);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask AppendBatchAsync(
        IReadOnlyCollection<HoursEvent> batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        if (batch.Count == 0)
        {
            throw new ArgumentException("A journal batch cannot be empty.", nameof(batch));
        }

        var materialized = batch.ToImmutableArray();
        var envelope = HoursJournalBatch.Create(materialized);
        lock (gate)
        {
            if (materialized.Any(entry => ExistingEventIds().Contains(entry.Id)))
            {
                throw new InvalidOperationException(
                    "A journal batch contains an event identity that already exists.");
            }

            entries = entries.Add(envelope);
        }

        foreach (var entry in materialized)
        {
            changeFeed?.Publish(entry);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(entries
                .SelectMany(HoursJournalBatch.Expand)
                .Where(item => string.Equals(item.EmployeeId, employeeId, StringComparison.Ordinal))
                .ToImmutableArray());
        }
    }

    /// <inheritdoc />
    public ValueTask<ImmutableArray<HoursEvent>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(entries
                .SelectMany(HoursJournalBatch.Expand)
                .ToImmutableArray());
        }
    }

    private HashSet<Guid> ExistingEventIds() =>
        entries
            .SelectMany(HoursJournalBatch.Expand)
            .Select(entry => entry.Id)
            .ToHashSet();
}
