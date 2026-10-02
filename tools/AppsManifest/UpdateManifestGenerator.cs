using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Apps.Manifest;

internal static class UpdateManifestGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    internal static int Generate(string[] args)
    {
        var metadataPath = Required(args, "--metadata");
        var assetsDirectory = Required(args, "--assets-dir");
        var version = Required(args, "--version");
        var tag = Required(args, "--tag");
        var releaseUrl = Required(args, "--release-url");
        var output = Required(args, "--output");
        var builtAt = Get(args, "--built-at")
            ?? DateTimeOffset.UtcNow.ToString("O");
        var repository = Get(args, "--repository")
            ?? ReleaseTrace.Repository;

        var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var rows = metadata.RootElement.TryGetProperty("include", out var include)
            ? include.EnumerateArray().ToList()
            : metadata.RootElement.ValueKind == JsonValueKind.Array
                ? metadata.RootElement.EnumerateArray().ToList()
                : throw new InvalidOperationException("Update metadata must contain an include array.");
        if (rows.Count == 0)
            throw new InvalidOperationException("Update metadata contains no release rows.");
        if (!Directory.Exists(assetsDirectory))
            throw new DirectoryNotFoundException(assetsDirectory);

        var assets = Directory.GetFiles(assetsDirectory, "*", SearchOption.AllDirectories)
            .Where(IsReleaseArtifact)
            .Select(path => new AssetFile(path, Path.GetFileName(path)))
            .ToList();
        if (assets.Count == 0)
            throw new InvalidOperationException("No release artifacts were found for the update catalog.");

        var updates = new List<ManifestFile>();
        foreach (var group in rows
                     .GroupBy(row => (
                         AppId: RequiredProperty(row, "update_app_id"),
                         Channel: RequiredProperty(row, "update_channel"))))
        {
            var first = group.First();
            var appId = RequiredProperty(first, "update_app_id");
            var displayName = RequiredProperty(first, "display_name");
            var channel = RequiredProperty(first, "update_channel");
            var distribution = RequiredProperty(first, "update_distribution");
            if (!string.Equals(distribution, "direct-github", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"'{appId}' is not eligible for direct GitHub updates.");

            var repositoryValue = OptionalProperty(first, "update_repository") ?? repository;
            var releaseUri = new Uri(releaseUrl, UriKind.Absolute);
            var artifacts = new List<ArtifactFile>();
            foreach (var row in group)
            {
                var declared = row.TryGetProperty("declared_update_artifacts", out var declaredValue)
                    && declaredValue.ValueKind == JsonValueKind.Array
                    ? declaredValue.EnumerateArray()
                        .Select(value => value.GetString() ?? "")
                        .Where(value => value.Length != 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : [];
                foreach (var kind in declared)
                {
                    var artifact = FindAsset(assets, row, kind);
                    if (artifact is null)
                        throw new InvalidOperationException(
                            $"No staged artifact for '{appId}' ({kind}).");
                    if (artifacts.Any(existing =>
                            string.Equals(existing.Name, artifact.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    artifacts.Add(CreateArtifact(
                        artifact,
                        kind,
                        version,
                        tag,
                        releaseUrl));
                }
            }

            if (artifacts.Count == 0)
                throw new InvalidOperationException($"No update artifacts were declared for '{appId}'.");

            updates.Add(new ManifestFile
            {
                SchemaVersion = 1,
                AppId = appId,
                DisplayName = displayName,
                DistributionMode = "directGithub",
                Channel = ToManifestChannel(channel),
                Version = version,
                PublishedAt = builtAt,
                Provenance = new ProvenanceFile
                {
                    AppId = appId,
                    Repository = repositoryValue,
                    Tag = tag,
                    ReleaseUri = releaseUrl,
                    Commit = Environment.GetEnvironmentVariable("GITHUB_SHA"),
                },
                Artifacts = artifacts
                    .OrderBy(artifact => artifact.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            });
        }

        var catalog = new CatalogFile
        {
            SchemaVersion = 1,
            GeneratedAt = builtAt,
            Updates = updates.OrderBy(update => update.AppId, StringComparer.OrdinalIgnoreCase).ToList(),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(
            output,
            JsonSerializer.Serialize(catalog, JsonOptions) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.Error.WriteLine($"Update manifest: {output}");
        return 0;
    }

    private static ArtifactFile CreateArtifact(
        AssetFile asset,
        string kind,
        string version,
        string tag,
        string releaseUrl)
    {
        var (artifactKind, platform, runtime, architecture, contentType) = kind.ToLowerInvariant() switch
        {
            "windowsinstaller" => ("windowsInstaller", "windows", "win-x64", "x64", "application/vnd.microsoft.portable-executable"),
            "windowsportable" => ("windowsPortable", "windows", "win-x64", "x64", "application/zip"),
            "linuxtargz" => ("linuxTarGz", "linux", "linux-x64", "x64", "application/gzip"),
            "androidapk" => ("androidApk", "android", null, null, "application/vnd.android.package-archive"),
            _ => throw new InvalidOperationException($"Unsupported update artifact kind '{kind}'."),
        };
        var fileInfo = new FileInfo(asset.Path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(asset.Path)))
            .ToLowerInvariant();
        var escapedName = Uri.EscapeDataString(asset.Name);
        return new ArtifactFile
        {
            Name = asset.Name,
            Kind = artifactKind,
            Target = new TargetFile
            {
                Platform = platform,
                RuntimeIdentifier = runtime,
                Architecture = architecture,
            },
            DownloadUri = $"{releaseUrl.TrimEnd('/')}/{escapedName}",
            Length = fileInfo.Length,
            Sha256 = hash,
            ContentType = contentType,
        };
    }

    private static AssetFile? FindAsset(
        IReadOnlyList<AssetFile> assets,
        JsonElement row,
        string kind)
    {
        var prefix = OptionalProperty(row, "artifact_prefix")
            ?? RequiredProperty(row, "key");
        var extension = kind.ToLowerInvariant() switch
        {
            "windowsinstaller" => ".exe",
            "windowsportable" => ".zip",
            "linuxtargz" => ".tar.gz",
            "androidapk" => ".apk",
            _ => throw new InvalidOperationException($"Unsupported update artifact kind '{kind}'."),
        };
        return assets
            .Where(asset => asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .Where(asset => asset.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || asset.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsReleaseArtifact(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToManifestChannel(string channel) =>
        channel.ToLowerInvariant() switch
        {
            "stable" => "stable",
            "preview" => "preview",
            "nightly" => "nightly",
            _ => throw new InvalidOperationException($"Unsupported update channel '{channel}'."),
        };

    private static string Required(string[] args, string name) =>
        Get(args, name) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Missing required option {name}.");

    private static string? Get(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static string RequiredProperty(JsonElement element, string name) =>
        OptionalProperty(element, name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Update metadata is missing '{name}'.");

    private static string? OptionalProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : null
            : null;

    private sealed record AssetFile(string Path, string Name);

    private sealed class CatalogFile
    {
        public int SchemaVersion { get; init; }
        public string GeneratedAt { get; init; } = "";
        public IReadOnlyList<ManifestFile> Updates { get; init; } = [];
    }

    private sealed class ManifestFile
    {
        public int SchemaVersion { get; init; }
        public string AppId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string DistributionMode { get; init; } = "";
        public string Channel { get; init; } = "";
        public string Version { get; init; } = "";
        public string PublishedAt { get; init; } = "";
        public ProvenanceFile Provenance { get; init; } = new();
        public IReadOnlyList<ArtifactFile> Artifacts { get; init; } = [];
    }

    private sealed class ProvenanceFile
    {
        public string AppId { get; init; } = "";
        public string Repository { get; init; } = "";
        public string Tag { get; init; } = "";
        public string ReleaseUri { get; init; } = "";
        public string? Commit { get; init; }
    }

    private sealed class ArtifactFile
    {
        public string Name { get; init; } = "";
        public string Kind { get; init; } = "";
        public TargetFile Target { get; init; } = new();
        public string DownloadUri { get; init; } = "";
        public long Length { get; init; }
        public string Sha256 { get; init; } = "";
        public string? ContentType { get; init; }
    }

    private sealed class TargetFile
    {
        public string Platform { get; init; } = "";
        public string? RuntimeIdentifier { get; init; }
        public string? Architecture { get; init; }
    }
}
