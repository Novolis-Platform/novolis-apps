namespace PresenceLedger.Storage;

/// <summary>Lists and copies the private ledger so a host can publish it.</summary>
public static class LedgerFiles
{
    /// <summary>
    /// Returns every ledger file under <paramref name="rootDirectory"/>.
    /// Files created after the last call are included the next time this is called.
    /// </summary>
    public static IReadOnlyList<LedgerFile> List(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
            return [];

        return Directory
            .EnumerateFiles(rootDirectory, "*.ndjson", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(rootDirectory, path)
                    .Replace('\\', '/');
                return new LedgerFile(relative, path, new FileInfo(path).Length);
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Copies the current ledger tree, overwriting previous copies of the same files.
    /// </summary>
    public static int CopyTo(string rootDirectory, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        Directory.CreateDirectory(destinationDirectory);

        var files = List(rootDirectory);
        foreach (var file in files)
        {
            var destination = Path.Combine(
                destinationDirectory,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            File.Copy(file.FullPath, destination, overwrite: true);
        }

        return files.Count;
    }
}
