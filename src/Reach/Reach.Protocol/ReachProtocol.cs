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

    /// <summary>UDP discovery probe token.</summary>
    public const string DiscoveryProbe = "NOVOLIS-REACH-WHO";

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

/// <summary>Logical channels in a Reach session.</summary>
public enum ReachChannel
{
    Control,
    Input,
    Clipboard,
    File,
    Video,
    Audio,
}

/// <summary>Capabilities that can be negotiated by a host and client.</summary>
[Flags]
public enum ReachCapability
{
    None = 0,
    H264 = 1 << 0,
    Av1 = 1 << 1,
    Touch = 1 << 2,
    Mouse = 1 << 3,
    Keyboard = 1 << 4,
    ClipboardText = 1 << 5,
    ClipboardFiles = 1 << 6,
    ClipboardImages = 1 << 7,
    SingleDisplay = 1 << 8,
    MultiMonitor = 1 << 9,
    Audio = 1 << 10,
    FileTransfer = 1 << 11,
    AdaptiveQuality = 1 << 12,
}

/// <summary>Operating system family of a Reach client or host.</summary>
public enum ReachPlatform
{
    Windows,
    Linux,
    Android,
}

/// <summary>Message kinds carried by the Reach control channel.</summary>
public enum ReachMessageType
{
    HostHello,
    ClientHello,
    HostCapabilities,
    ClientCapabilities,
    SessionOpen,
    SessionResume,
    SessionClose,
    DisplayTopology,
    DisplaySelect,
    DisplayResize,
    PointerMove,
    PointerButton,
    PointerWheel,
    KeyDown,
    KeyUp,
    TextInput,
    ClipboardChanged,
    ClipboardContent,
    VideoStreamStart,
    VideoStreamConfiguration,
    VideoStreamReset,
    VideoFrame,
    RequestKeyFrame,
    AudioStreamStart,
    AudioStreamConfiguration,
    AudioFrame,
    FileOffer,
    FileChunk,
    FileComplete,
    HostStatus,
}

/// <summary>Lifecycle state of a Reach session.</summary>
public enum ReachSessionState
{
    New,
    HelloExchanged,
    Opening,
    Open,
    Resuming,
    Closing,
    Closed,
    Failed,
}
