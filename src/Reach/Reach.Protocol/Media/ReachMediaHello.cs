using System.Text.Json;

namespace Novolis.Reach.Protocol.Media;

/// <summary>Identifies the optional media connection for an open session.</summary>
public sealed record ReachMediaHello(
    Guid SessionId,
    string AppId,
    string ProtocolVersion);
