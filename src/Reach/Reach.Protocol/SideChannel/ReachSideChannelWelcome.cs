namespace Novolis.Reach.Protocol.SideChannel;

/// <summary>Confirms that a client joined the Reach side channel.</summary>
public sealed record ReachSideChannelWelcome(
    string AppId,
    string ProtocolVersion,
    string HostName,
    string[] Features);
