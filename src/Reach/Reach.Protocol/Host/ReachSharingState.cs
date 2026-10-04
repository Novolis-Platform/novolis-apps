using System.Text.Json;

namespace Novolis.Reach.Protocol.Host;

/// <summary>Truthful host sharing state for the remote client surface.</summary>
public sealed record ReachSharingState(bool IsPaused, string Reason);
