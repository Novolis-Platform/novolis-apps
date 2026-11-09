using System.Globalization;

namespace Merglyph.Core;

/// <summary>Creates empty Markdown files with unique Untitled names.</summary>
public static class NewMarkdownDocument
{
    /// <summary>Default file stem before the extension.</summary>
    public const string DefaultStem = "Untitled";

    /// <summary>Starter body written into a new document.</summary>
    public static string DefaultContent { get; } =
        "# Untitled" + Environment.NewLine + Environment.NewLine;

    /// <summary>
    /// Picks <c>Untitled.md</c>, then <c>Untitled 2.md</c>, … under
    /// <paramref name="directory"/> without colliding with existing files.
    /// </summary>
    public static string CreateUniquePath(
        string directory,
        string stem = DefaultStem,
        string extension = ".md")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        if (!extension.StartsWith('.'))
            extension = "." + extension;

        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(directory, stem + extension);
        if (!File.Exists(candidate))
            return candidate;

        for (var index = 2; index < 10_000; index++)
        {
            candidate = Path.Combine(
                directory,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{stem} {index}{extension}"));
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(
            directory,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}"));
    }
}
