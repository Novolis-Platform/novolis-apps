using System.Net.Sockets;
using Novolis.Testing.Appium;

namespace NovolisPdfReader.UiTests;

internal static class PdfReaderUiHarness
{
    public const string AppEnvironmentVariable = "NOVOLIS_PDFREADER_UI_APP";
    public const string PlatformEnvironmentVariable = "NOVOLIS_PDFREADER_UI_PLATFORM";

    public static bool AppiumIsListening()
    {
        try
        {
            var uri = AppiumServerAddress.Resolve();
            using var client = new TcpClient();
            var result = client.BeginConnect(uri.Host, uri.Port, null, null);
            var connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
            if (!connected)
                return false;
            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? TryResolveWindowsExe()
    {
        foreach (var candidate in EnumerateWindowsCandidates())
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static string? TryResolveAndroidApk()
    {
        var fromEnv = Environment.GetEnvironmentVariable(AppEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(fromEnv))
            return null;
        var fullPath = Path.GetFullPath(fromEnv);
        return fullPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)
            ? fullPath
            : null;
    }

    public static bool IsAndroidRequested() =>
        string.Equals(
            Environment.GetEnvironmentVariable(PlatformEnvironmentVariable),
            "android",
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateWindowsCandidates()
    {
        var fromEnv = Environment.GetEnvironmentVariable(AppEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            yield return fromEnv;

        yield return Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "artifacts",
            "bin",
            "NovolisPdfReader",
            "debug_net10.0-windows10.0.19041.0_win-x64",
            "NovolisPdfReader.exe");
        yield return @"d:\novolis\novolis-apps\artifacts\bin\NovolisPdfReader\debug_net10.0-windows10.0.19041.0_win-x64\NovolisPdfReader.exe";
    }
}
