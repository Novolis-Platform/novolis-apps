using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Reach.Protocol;

/// <summary>Reach product protocol constants and JSON options.</summary>
public static class ReachProtocol
{
    /// <summary>Protocol application identity.</summary>
    public const string AppId = "Reach";

    /// <summary>Current wire protocol version.</summary>
    public const string Version = "1.0";

    /// <summary>Reliable control/input port.</summary>
    public const int ControlPort = 19800;

    /// <summary>Loss-tolerant media port.</summary>
    public const int MediaPort = 19801;

    /// <summary>Discovery port.</summary>
    public const int DiscoveryPort = 19802;

    /// <summary>HTTPS SignalR side-channel port.</summary>
    public const int SideChannelPort = 19803;

    /// <summary>SignalR hub path for status and diagnostics.</summary>
    public const string SideChannelHubPath = "/reach-side-channel";

    /// <summary>SignalR side-channel transport scheme.</summary>
    public const string SideChannelScheme = "https";

    /// <summary>UDP discovery probe token.</summary>
    public const string DiscoveryProbe = "NOVOLIS-REACH-WHO";

    /// <summary>Shared service-discovery cohort for Reach hosts and clients.</summary>
    public const string DiscoveryCohort = "reach";

    /// <summary>Service-discovery name for a Reach host.</summary>
    public const string DiscoveryServiceName = "host";

    /// <summary>Serializer options shared by protocol messages.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    /// <summary>Returns true when two versions share the same major line.</summary>
    public static bool IsCompatible(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var remoteMajor = version.Split('.', 2)[0];
        return string.Equals(remoteMajor, Version.Split('.', 2)[0], StringComparison.Ordinal);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
