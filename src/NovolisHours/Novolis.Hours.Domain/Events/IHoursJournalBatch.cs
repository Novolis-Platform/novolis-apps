namespace Novolis.Hours.Domain;

/// <summary>Optional journal capability for committing related authoritative facts together.</summary>
public interface IHoursJournalBatch
{
    /// <summary>Appends all events as one storage operation.</summary>
    ValueTask AppendBatchAsync(
        IReadOnlyCollection<HoursEvent> entries,
        CancellationToken cancellationToken = default);
}
