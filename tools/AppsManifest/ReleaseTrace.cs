using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Apps.Manifest;

internal static class ReleaseTrace
{
    internal const string FileName = "Novolis.Release.json";
    internal const string Repository = "https://github.com/Novolis-Platform/novolis-apps";

    private static readonly JsonSerializerOptions StampJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    internal static string? ResolveCommit(string repoRoot)
    {
        var fromEnv = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var code = ProcessRunner.RunCapture("git", repoRoot, ["rev-parse", "HEAD"], echoToConsole: false, out var stdout, out _);
        if (code != 0)
            return null;

        var sha = stdout.Trim();
        return sha.Length >= 7 ? sha : null;
    }

    internal static List<string> WithInformationalVersion(IReadOnlyList<string> versionArgs, string packageVersion, string? commit)
    {
        var informational = string.IsNullOrWhiteSpace(commit) ? packageVersion : $"{packageVersion}+{commit}";
        var replaced = false;
        var args = new List<string>(versionArgs.Count);
        foreach (var arg in versionArgs)
        {
            if (arg.StartsWith("-p:InformationalVersion=", StringComparison.Ordinal))
            {
                args.Add($"-p:InformationalVersion={informational}");
                replaced = true;
            }
            else
            {
                args.Add(arg);
            }
        }

        if (!replaced)
            args.Add($"-p:InformationalVersion={informational}");
        return args;
    }

    internal static void WriteStamp(string publishDir, string name, string appId, string version, string? commit)
    {
        var stamp = new ReleaseStamp
        {
            Name = name,
            AppId = appId,
            Version = version,
            Commit = string.IsNullOrWhiteSpace(commit) ? null : commit,
            Channel = "windows-inno",
            Repository = Repository,
            Release = $"{Repository}/releases/tag/v{version}",
            BuiltAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };
        var json = JsonSerializer.Serialize(stamp, StampJson) + "\n";
        File.WriteAllText(Path.Combine(publishDir, FileName), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    internal static void AddInstallerComments(string scriptPath, string name, string version, string? commit)
    {
        var contents = File.ReadAllText(scriptPath);
        if (contents.Contains("AppComments=", StringComparison.Ordinal))
            return;

        var marker = $"AppVersion={version}";
        var index = contents.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            throw new InvalidOperationException($"Installer script is missing AppVersion: {scriptPath}");

        var insertAt = index + marker.Length;
        if (insertAt < contents.Length && contents[insertAt] == '\r')
            insertAt++;
        if (insertAt < contents.Length && contents[insertAt] == '\n')
            insertAt++;

        var newLine = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var shortCommit = commit is { Length: >= 12 } ? commit[..12] : commit;
        var comment = string.IsNullOrWhiteSpace(shortCommit)
            ? $"{name} {version} windows-inno"
            : $"{name} {version} windows-inno {shortCommit}";
        var productText = string.IsNullOrWhiteSpace(commit) ? version : $"{version}+{commit}";
        var block = $"AppComments={comment}{newLine}VersionInfoProductTextVersion={productText}{newLine}";
        contents = contents.Insert(insertAt, block);
        File.WriteAllText(scriptPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private sealed class ReleaseStamp
    {
        public required string Name { get; init; }
        public required string AppId { get; init; }
        public required string Version { get; init; }
        public string? Commit { get; init; }
        public required string Channel { get; init; }
        public required string Repository { get; init; }
        public required string Release { get; init; }
        public required string BuiltAt { get; init; }
    }
}
