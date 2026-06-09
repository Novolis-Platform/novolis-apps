namespace ManuscriptStudio.Core;

internal static class ManuscriptAppContext
{
    public static string ResolveDataRoot()
    {
        var primary = Path.Combine(AppContext.BaseDirectory, "ManuscriptStudio");
        if (TryEnsureWritable(primary))
            return primary;

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "ManuscriptStudio");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool TryEnsureWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
