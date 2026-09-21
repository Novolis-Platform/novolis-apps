using System.Text.Json;

namespace CursorRemote.Protocol;

public static class RemoteProtocol
{
    public const string Version = "1.1";
    public const int DefaultPort = 18790;
    public const string TokenHeader = "X-Cursor-Remote-Token";

    public static JsonSerializerOptions JsonOptions { get; } =
        new(JsonSerializerDefaults.Web);
}

public sealed record RemoteConnectionInfo(
    string Endpoint,
    string Token,
    string? TailscaleAddress,
    string ProtocolVersion);

public sealed record RemoteStatusDto(
    string ProtocolVersion,
    string HostName,
    string[] Endpoints,
    int ScreenWidth,
    int ScreenHeight,
    int OriginX,
    int OriginY,
    bool CursorRunning);

public sealed record RemoteClickRequest(
    double X,
    double Y,
    string Button = "left",
    int ClickCount = 1);

public sealed record RemoteTextRequest(string Text);

public sealed record RemoteKeyRequest(string Key);

public sealed record RemoteScreenFrame(
    byte[] Png,
    int Width,
    int Height,
    int OriginX = 0,
    int OriginY = 0);

public sealed record RemoteOperationResponse(
    bool Ok,
    string Message);
