using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Atomic journal envelope for related authoritative events.</summary>
public sealed record HoursJournalBatch(ImmutableArray<HoursEvent> Entries)
{
    /// <summary>Creates one storage event containing a non-empty batch.</summary>
    public static HoursEvent Create(IReadOnlyCollection<HoursEvent> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("A journal batch cannot be empty.", nameof(entries));
        }

        var materialized = entries.ToImmutableArray();
        if (materialized.Any(entry => entry is null) ||
            materialized.GroupBy(entry => entry.Id).Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "A journal batch must contain distinct non-null events.",
                nameof(entries));
        }

        var first = materialized[0];
        return HoursEvent.Create(
            first.EmployeeId,
            HoursEventType.JournalBatchCommitted,
            new HoursJournalBatch(materialized),
            first.Actor,
            materialized.Min(entry => entry.OccurredAtUtc));
    }

    /// <summary>Expands a stored batch or returns a normal event unchanged.</summary>
    public static IEnumerable<HoursEvent> Expand(HoursEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Type == HoursEventType.JournalBatchCommitted
            ? entry.ReadPayload<HoursJournalBatch>().Entries
            : [entry];
    }
}
