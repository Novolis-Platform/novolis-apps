namespace PresenceLedger.Storage;

/// <summary>
/// Copies a raw Download/PresenceLedger-style folder into the private ledger root.
/// Skips MediaStore collision copies and abandoned pending files.
/// </summary>
public static class LedgerFolderImport
{
    static readonly string[] RootFiles =
    [
        "locations.ndjson",
        "presence.ndjson",
        "states.ndjson",
        "cumulative.ndjson",
        "observation-settings.json",
    ];

    /// <summary>Result of a folder import.</summary>
    public sealed record Result(
        int FilesCopied,
        int FilesSkipped,
        bool IncludedDiagnostics);

    /// <summary>
    /// Imports ledger files from <paramref name="sourceRoot"/> into
    /// <paramref name="destinationRoot"/>. Existing destination files with the
    /// same relative path are overwritten.
    /// </summary>
    public static Result Import(
        string sourceRoot,
        string destinationRoot,
        bool includeDiagnostics = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException($"Ledger dump folder was not found: {sourceRoot}");

        Directory.CreateDirectory(destinationRoot);
        var copied = 0;
        var skipped = 0;

        foreach (var name in RootFiles)
        {
            var source = Path.Combine(sourceRoot, name);
            if (!File.Exists(source) || IsJunkName(name))
            {
                if (File.Exists(source) && IsJunkName(name))
                    skipped++;
                continue;
            }

            CopyFile(source, Path.Combine(destinationRoot, name));
            copied++;
        }

        var sourceObservations = Path.Combine(sourceRoot, "observations");
        var destinationObservations = Path.Combine(destinationRoot, "observations");
        if (Directory.Exists(sourceObservations))
        {
            Directory.CreateDirectory(destinationObservations);
            foreach (var file in Directory.EnumerateFiles(sourceObservations))
            {
                var leaf = Path.GetFileName(file);
                if (IsJunkName(leaf)
                    || !leaf.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsJunkName(leaf)
                        || leaf.Contains(".ndjson", StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                    }

                    continue;
                }

                CopyFile(file, Path.Combine(destinationObservations, leaf));
                copied++;
            }
        }

        if (includeDiagnostics)
        {
            var diagnostics = Path.Combine(sourceRoot, "diagnostics.ndjson");
            if (File.Exists(diagnostics) && !IsJunkName("diagnostics.ndjson"))
            {
                CopyFile(diagnostics, Path.Combine(destinationRoot, "diagnostics.ndjson"));
                copied++;
            }
        }
        else if (File.Exists(Path.Combine(sourceRoot, "diagnostics.ndjson")))
        {
            skipped++;
        }

        return new Result(copied, skipped, includeDiagnostics);
    }

    /// <summary>True for MediaStore collision or pending leftover names.</summary>
    public static bool IsJunkName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return true;

        var leaf = Path.GetFileName(fileName);
        if (leaf.StartsWith(".pending-", StringComparison.OrdinalIgnoreCase))
            return true;

        // Android Download Manager style: "locations.ndjson (12)"
        var open = leaf.LastIndexOf(" (", StringComparison.Ordinal);
        return open > 0 && leaf.EndsWith(')');
    }

    static void CopyFile(string source, string destination)
    {
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        File.Copy(source, destination, overwrite: true);
    }
}
