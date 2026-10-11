using System.Net.Sockets;
using Novolis.Testing.Appium;

namespace Reach.UiTests;

internal static class ReachUiTestHarness
{
    internal const string AppEnvironmentVariable = "NOVOLIS_REACH_UI_APP";
    internal const string AndroidAppEnvironmentVariable =
        "NOVOLIS_REACH_UI_ANDROID_APK";
    internal const string EndpointEnvironmentVariable = "NOVOLIS_REACH_UI_ENDPOINT";
    internal const string AndroidEndpointEnvironmentVariable =
        "NOVOLIS_REACH_UI_ANDROID_ENDPOINT";
    internal const string TailscaleEndpointEnvironmentVariable =
        "NOVOLIS_REACH_UI_TAILSCALE_ENDPOINT";

    internal static bool AppiumIsListening()
    {
        try
        {
            var address = AppiumServerAddress.Resolve();
            using var client = new TcpClient();
            var result = client.BeginConnect(address.Host, address.Port, null, null);
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

    internal static string? TryResolveWindowsClient()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(
            AppEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            var fullPath = Path.GetFullPath(fromEnvironment);
            if (File.Exists(fullPath))
                return fullPath;
        }

        var candidates = new[]
        {
            Path.Combine(
                @"d:\novolis\novolis-apps\artifacts\bin\Reach.Client.Windows",
                "debug_net10.0-windows10.0.19041.0_win-x64",
                "Novolis.Reach.Client.Windows.exe"),
            Path.Combine(
                @"d:\novolis\novolis-apps\artifacts\publish\Reach.Client.Windows",
                "debug",
                "Novolis.Reach.Client.Windows.exe"),
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "artifacts",
                "bin",
                "Reach.Client.Windows",
                "debug_net10.0-windows10.0.19041.0_win-x64",
                "Novolis.Reach.Client.Windows.exe"),
        };
        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    internal static string? TryResolveAndroidApk()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(
            AndroidAppEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(fromEnvironment))
            return null;

        var fullPath = Path.GetFullPath(fromEnvironment);
        return fullPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
            && File.Exists(fullPath)
            ? fullPath
            : null;
    }

    internal static string? TryResolveEndpoint() =>
        Environment.GetEnvironmentVariable(EndpointEnvironmentVariable)
            is { Length: > 0 } endpoint
            ? endpoint
            : null;

    internal static string? TryResolveTailscaleEndpoint() =>
        Environment.GetEnvironmentVariable(TailscaleEndpointEnvironmentVariable)
            is { Length: > 0 } endpoint
            ? endpoint
            : null;

    internal static string? TryResolveAnyEndpoint() =>
        TryResolveEndpoint() ?? TryResolveTailscaleEndpoint();

    internal static string? TryResolveAndroidEndpoint() =>
        Environment.GetEnvironmentVariable(AndroidEndpointEnvironmentVariable)
            is { Length: > 0 } endpoint
            ? endpoint
            : TryResolveAnyEndpoint();
}
