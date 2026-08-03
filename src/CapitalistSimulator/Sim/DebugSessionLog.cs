using System.Globalization;
using System.Text.Json;

namespace CapitalistSimulator.Sim;

internal static class DebugSessionLog
{
    private const string SessionId = "e2a885";
    private static readonly string LogPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "debug-e2a885.log"));

    // Workspace root fallback when BaseDirectory layout differs
    private static readonly string[] CandidatePaths =
    [
        Path.Combine(@"d:\novolis", "debug-e2a885.log"),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "debug-e2a885.log")),
        LogPath,
    ];

    public static void Write(string hypothesisId, string location, string message, object data, string runId = "playtest")
    {
        // #region agent log
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["runId"] = runId,
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            var line = JsonSerializer.Serialize(payload);
            foreach (var path in CandidatePaths.Distinct())
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.AppendAllText(path, line + "\n");
                    break;
                }
                catch
                {
                    // try next path
                }
            }
        }
        catch
        {
            // never throw from debug logging
        }
        // #endregion
    }

    public static string DescribeMoney(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
