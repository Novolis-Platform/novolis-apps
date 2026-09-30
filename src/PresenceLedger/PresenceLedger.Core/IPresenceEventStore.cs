namespace PresenceLedger.Core;

/// <summary>Appends and reads semantic presence events.</summary>
public interface IPresenceEventStore
{
    /// <summary>Reads historical events in ledger order.</summary>
    IAsyncEnumerable<PresenceEvent> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Appends one semantic event.</summary>
    ValueTask AppendAsync(
        PresenceEvent presenceEvent,
        CancellationToken cancellationToken = default);
}
