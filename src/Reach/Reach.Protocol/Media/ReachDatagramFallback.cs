namespace Novolis.Reach.Protocol.Media;

/// <summary>Requests that the host stop using a stalled UDP media path.</summary>
public sealed record ReachDatagramFallback(string Reason);
