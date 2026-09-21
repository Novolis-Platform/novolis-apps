using System.Text.Json;

namespace Novolis.Reach.Protocol;

/// <summary>Initial host identity and endpoint message.</summary>
public sealed record ReachHostHello(
    string AppId,
    string ProtocolVersion,
    string HostName,
    string[] Endpoints);

/// <summary>Initial client identity and platform message.</summary>
public sealed record ReachClientHello(
    string AppId,
    string ProtocolVersion,
    ReachPlatform Platform,
    string ClientName);

/// <summary>Capability offer or negotiated capability set.</summary>
public sealed record ReachCapabilitiesMessage(ReachCapabilities Capabilities);

/// <summary>Opens a client session.</summary>
public sealed record ReachSessionOpen(Guid SessionId, string RequestedDisplayId);

/// <summary>Resumes a previous client session.</summary>
public sealed record ReachSessionResume(Guid SessionId, long LastVideoSequence);

/// <summary>Closes a client session.</summary>
public sealed record ReachSessionClose(Guid SessionId, string Reason);

/// <summary>Monitor topology announcement.</summary>
public sealed record ReachDisplayTopology(IReadOnlyList<ReachDisplay> Displays);

/// <summary>One display in a topology announcement.</summary>
public sealed record ReachDisplay(
    string Id,
    int Left,
    int Top,
    int Width,
    int Height,
    uint Dpi);

/// <summary>Selects a display for a client.</summary>
public sealed record ReachDisplaySelect(string DisplayId);

/// <summary>Requests a stream size and quality.</summary>
public sealed record ReachDisplayResize(int Width, int Height, int FramesPerSecond);

/// <summary>Pointer move in selected-display coordinates.</summary>
public sealed record ReachPointerMove(double X, double Y);

/// <summary>Pointer button event.</summary>
public sealed record ReachPointerButton(string Button, bool IsDown, int ClickCount = 1);

/// <summary>Pointer wheel event.</summary>
public sealed record ReachPointerWheel(int Delta);

/// <summary>Virtual-key event.</summary>
public sealed record ReachKeyEvent(ushort VirtualKey);

/// <summary>Unicode text event.</summary>
public sealed record ReachTextInput(string Text);

/// <summary>Clipboard notification.</summary>
public sealed record ReachClipboardChanged(string Format);

/// <summary>Clipboard content response.</summary>
public sealed record ReachClipboardContent(
    string Format,
    string? Text,
    string[]? Files = null,
    byte[]? Data = null);

/// <summary>Starts a video stream.</summary>
public sealed record ReachVideoStreamStart(string Codec, int Width, int Height, int FramesPerSecond);

/// <summary>Changes video stream quality.</summary>
public sealed record ReachVideoStreamConfiguration(
    string Codec,
    int Width,
    int Height,
    int FramesPerSecond,
    int TargetBitrate);

/// <summary>Resets a video stream decoder.</summary>
public sealed record ReachVideoStreamReset(long Sequence);

/// <summary>One encoded video access unit on the media channel.</summary>
public sealed record ReachVideoFrame(
    long Sequence,
    int Width,
    int Height,
    long Timestamp,
    string Codec,
    bool IsKeyFrame,
    byte[] AccessUnit);

/// <summary>Requests an intra frame.</summary>
public sealed record ReachRequestKeyFrame(long LastSequence);

/// <summary>Starts an audio stream.</summary>
public sealed record ReachAudioStreamStart(
    string Codec,
    int SampleRate,
    int Channels,
    int BitsPerSample = 16,
    bool IsFloat = false);

/// <summary>Changes audio stream configuration.</summary>
public sealed record ReachAudioStreamConfiguration(string Codec, int SampleRate, int Channels);

/// <summary>One PCM audio block on the media channel.</summary>
public sealed record ReachAudioFrame(
    long Sequence,
    long Timestamp,
    string Codec,
    int SampleRate,
    int Channels,
    byte[] Data,
    int BitsPerSample = 16,
    bool IsFloat = false);

/// <summary>Offers a file for a later file-transfer phase.</summary>
public sealed record ReachFileOffer(Guid TransferId, string Name, long Length, string Hash);

/// <summary>Carries one file-transfer chunk.</summary>
public sealed record ReachFileChunk(Guid TransferId, long Offset, byte[] Data);

/// <summary>Completes a file transfer.</summary>
public sealed record ReachFileComplete(Guid TransferId, bool Succeeded, string? Error);

/// <summary>Operator-facing host status.</summary>
public sealed record ReachHostStatus(
    string State,
    string[] Endpoints,
    int ConnectedClients,
    bool SharingPaused,
    string? InteractiveUser,
    string[] RecentMessages);

/// <summary>Command sent by the operator Console to the host service.</summary>
public enum ReachHostCommand
{
    GetStatus,
    SetSharingPaused,
    GetLogs,
}

/// <summary>Local IPC request from the operator Console.</summary>
public sealed record ReachHostControlRequest(
    ReachHostCommand Command,
    bool? Enabled = null);

/// <summary>Local IPC response from the host service.</summary>
public sealed record ReachHostControlResponse(
    bool Ok,
    string Message,
    ReachHostStatus? Status = null);

/// <summary>One typed message envelope on a Reach control stream.</summary>
public sealed record ReachMessageEnvelope(
    ReachMessageType Type,
    long Sequence,
    JsonElement Body);
