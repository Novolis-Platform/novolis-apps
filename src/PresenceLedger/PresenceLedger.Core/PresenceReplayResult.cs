namespace PresenceLedger.Core;

/// <summary>Events and operational state produced by replaying retained samples.</summary>
public sealed record PresenceReplayResult(
    IReadOnlyList<PresenceEvent> Events,
    IReadOnlyList<LocationPresenceState> States);
