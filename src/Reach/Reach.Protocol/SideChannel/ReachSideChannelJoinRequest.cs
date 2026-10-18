namespace Novolis.Reach.Protocol.SideChannel;

/// <summary>Identifies a primary Reach client to the side channel.</summary>
public sealed record ReachSideChannelJoinRequest(
    string AppId,
    string ProtocolVersion,
    string ClientName);
