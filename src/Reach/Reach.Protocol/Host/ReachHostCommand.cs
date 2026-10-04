using System.Text.Json;

namespace Novolis.Reach.Protocol.Host;

/// <summary>Command sent by the operator host app to the host service.</summary>
public enum ReachHostCommand
{
    GetStatus,
    SetSharingPaused,
    GetLogs,
    ReconnectHelper,
    StopHosting,
}
