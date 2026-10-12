using Android.Content;
using PresenceLedger.Storage;

namespace PresenceLedger.Android;

/// <summary>Copies share payloads into an app-owned FileProvider directory.</summary>
internal static class AndroidLedgerShareStaging
{
    public static string StageLedger(Context context, string sourceRoot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        var stagingRoot = Prepare(context);

        foreach (var file in LedgerFiles.List(sourceRoot))
        {
            var target = Path.Combine(
                stagingRoot,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            File.Copy(file.FullPath, target, overwrite: true);
        }

        return stagingRoot;
    }

    public static string StageText(Context context, string fileName, string content)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        var stagingRoot = Prepare(context);
        var path = Path.Combine(stagingRoot, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    static string Prepare(Context context)
    {
        var files = context.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Android files directory is unavailable.");
        var root = Path.Combine(files, "PresenceLedger", "share");
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);
        return root;
    }
}
