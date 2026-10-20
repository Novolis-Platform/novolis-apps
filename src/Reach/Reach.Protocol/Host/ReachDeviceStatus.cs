namespace Novolis.Reach.Protocol.Host;

/// <summary>
/// Stable identity and platform information for one Reach host device.
/// </summary>
public sealed record ReachDeviceStatus(
    string DeviceId,
    string DeviceName,
    string Platform,
    string SoftwareVersion);
