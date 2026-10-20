using Microsoft.Extensions.Logging;

namespace Novolis.Reach.Host.Server;

internal static class ReachHostDeviceIdentity
{
    private const string IdentityFileName = "device-id";

    internal static ReachDeviceStatus Load(ILogger log)
    {
        var deviceId = ReadOrCreateDeviceId(log);
        var version = typeof(ReachHostDeviceIdentity).Assembly
            .GetName()
            .Version?
            .ToString()
            ?? ReachProtocol.Version;

        return new ReachDeviceStatus(
            deviceId,
            Environment.MachineName,
            "Windows",
            version);
    }

    private static string ReadOrCreateDeviceId(ILogger log)
    {
        var deviceIdPath = GetDeviceIdPath();
        try
        {
            var existing = File.Exists(deviceIdPath)
                ? File.ReadAllText(deviceIdPath).Trim()
                : string.Empty;
            if (Guid.TryParse(existing, out var parsed))
                return parsed.ToString("D");

            var directory = Path.GetDirectoryName(deviceIdPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var generated = Guid.NewGuid().ToString("D");
            var temporaryPath = deviceIdPath + ".tmp";
            File.WriteAllText(temporaryPath, generated);
            File.Move(temporaryPath, deviceIdPath, overwrite: true);
            return generated;
        }
        catch (Exception exception)
        {
            log.LogWarning(
                exception,
                "Reach device identity could not be persisted; using an ephemeral identity.");
            return Guid.NewGuid().ToString("D");
        }
    }

    private static string GetDeviceIdPath()
    {
        var commonData = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonData))
            commonData = Environment.GetEnvironmentVariable("ProgramData");
        if (string.IsNullOrWhiteSpace(commonData))
            commonData = AppContext.BaseDirectory;

        return Path.Combine(
            commonData,
            "Novolis",
            "Reach",
            IdentityFileName);
    }
}
