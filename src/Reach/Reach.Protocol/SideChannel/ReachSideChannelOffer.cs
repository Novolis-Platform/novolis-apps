namespace Novolis.Reach.Protocol.SideChannel;

/// <summary>
/// Describes the optional authenticated side channel offered after the
/// primary Reach connection is established.
/// </summary>
public sealed record ReachSideChannelOffer(
    string Scheme,
    int Port,
    string HubPath,
    string AccessToken,
    string? CertificatePin = null);
