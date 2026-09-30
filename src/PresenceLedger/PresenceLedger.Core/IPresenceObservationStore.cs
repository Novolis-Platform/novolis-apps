namespace PresenceLedger.Core;

/// <summary>Persists point-in-time evidence for local diagnostics and replay.</summary>
public interface IPresenceObservationStore
{
    /// <summary>Appends one retained observation to its UTC daily file.</summary>
    ValueTask AppendAsync(
        PresenceObservationRecord observation,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one UTC date without scanning unrelated dates.</summary>
    IAsyncEnumerable<PresenceObservationRecord> ReadAsync(
        DateOnly utcDate,
        CancellationToken cancellationToken = default);
}
