using System.Text.Json;

namespace Novolis.Reach.Protocol.Session;

/// <summary>Closes a client session.</summary>
public sealed record ReachSessionClose(Guid SessionId, string Reason);
