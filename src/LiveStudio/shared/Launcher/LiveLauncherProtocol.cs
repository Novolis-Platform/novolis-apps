using System.Text.Json;

namespace LiveStudio.Shared.Launcher;

public static class LiveLauncherProtocol
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(LiveLauncherStatus status) =>
        JsonSerializer.Serialize(status, Options);

    public static LiveLauncherStatus Deserialize(string json) =>
        JsonSerializer.Deserialize<LiveLauncherStatus>(json, Options)
        ?? throw new InvalidOperationException("Launcher status payload was empty.");
}
