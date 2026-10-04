using System.Text.Json;

namespace CadStudio;

/// <summary>
/// Preserves data from the retired CAD hosts without converting or deleting user
/// documents. The original directory structure is copied below <c>migrations</c>
/// so users can open the original <c>.cadjson</c> and <c>.shipjson</c> files
/// explicitly.
/// </summary>
internal static class CadStudioDataMigration
{
    private const string ManifestName = "legacy-hosts-v1.json";

    public static string DefaultRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "CAD Studio");

    public static Report Run(string consolidatedRoot)
    {
        var legacyRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis");
        return Run(
            consolidatedRoot,
            [
                Path.Combine(legacyRoot, "Draft Studio"),
                Path.Combine(legacyRoot, "CAD Studio 3D"),
                Path.Combine(legacyRoot, "Ship Designer"),
            ]);
    }

    internal static Report Run(
        string consolidatedRoot,
        IReadOnlyList<string> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consolidatedRoot);
        ArgumentNullException.ThrowIfNull(sources);

        var root = Path.GetFullPath(consolidatedRoot);
        var migrationRoot = Path.Combine(root, "migrations");
        var manifestPath = Path.Combine(migrationRoot, ManifestName);
        if (File.Exists(manifestPath))
            return Report.FromManifest(manifestPath);

        Directory.CreateDirectory(migrationRoot);
        var copied = 0;
        var conflicts = 0;
        var importedSources = new List<string>();
        var failures = new List<string>();
        foreach (var source in sources)
        {
            if (!Directory.Exists(source) || PathsEqual(source, root))
                continue;

            var destination = Path.Combine(
                migrationRoot,
                Path.GetFileName(source));
            try
            {
                var sourceFiles = CopyTree(source, destination, ref copied, ref conflicts);
                if (sourceFiles > 0)
                    importedSources.Add(source);
            }
            catch (Exception ex)
            {
                failures.Add($"{source}: {ex.Message}");
            }
        }

        var report = new Report
        {
            ManifestPath = manifestPath,
            ImportedSources = importedSources,
            FilesCopied = copied,
            Conflicts = conflicts,
            Failures = failures,
            Completed = failures.Count == 0,
        };

        if (report.Completed)
        {
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }

        return report;
    }

    private static int CopyTree(
        string source,
        string destination,
        ref int copied,
        ref int conflicts)
    {
        var filesSeen = 0;
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            filesSeen++;
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                conflicts++;
                continue;
            }

            File.Copy(file, target, overwrite: false);
            copied++;
        }

        return filesSeen;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    internal sealed record Report
    {
        public bool Completed { get; init; }

        public string ManifestPath { get; init; } = "";

        public IReadOnlyList<string> ImportedSources { get; init; } = [];

        public int FilesCopied { get; init; }

        public int Conflicts { get; init; }

        public IReadOnlyList<string> Failures { get; init; } = [];

        public bool AlreadyComplete => File.Exists(ManifestPath);

        public static Report FromManifest(string manifestPath) => new()
        {
            Completed = true,
            ManifestPath = manifestPath,
        };
    }
}
