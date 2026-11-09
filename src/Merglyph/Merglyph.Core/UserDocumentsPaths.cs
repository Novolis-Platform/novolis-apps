namespace Merglyph.Core;

/// <summary>
/// Resolves a user documents folder that works across desktop and mobile hosts.
/// Prefers My Documents / Personal, then a caller-supplied writable fallback.
/// </summary>
public static class UserDocumentsPaths
{
    /// <summary>
    /// Returns the best available user documents directory without creating it.
    /// </summary>
    public static string Resolve(Func<Environment.SpecialFolder, string>? getFolderPath = null)
    {
        getFolderPath ??= static folder => Environment.GetFolderPath(folder);

        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.Personal,
                 })
        {
            var path = getFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
                return path;
        }

        var profile = getFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
            return Path.Combine(profile, "Documents");

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Ensures <paramref name="preferred"/> is writable; otherwise uses
    /// <paramref name="fallback"/>. Both paths are created when needed.
    /// </summary>
    public static string EnsureWritable(string preferred, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferred);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        if (TryClaim(preferred))
            return preferred;

        if (TryClaim(fallback))
            return fallback;

        throw new IOException(
            $"Could not create a writable documents folder at '{preferred}' or '{fallback}'.");
    }

    static bool TryClaim(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".merglyph-write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or NotSupportedException)
        {
            return false;
        }
    }
}
