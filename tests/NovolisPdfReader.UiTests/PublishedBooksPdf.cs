namespace NovolisPdfReader.UiTests;

/// <summary>
/// Resolves PDFs produced by the frankhaugen/books print pipeline
/// (<c>manuscript-cli print</c> → <c>out/&lt;series&gt;/&lt;book&gt;/&lt;book&gt;.pdf</c>,
/// then <c>release-staging/&lt;series&gt;-&lt;book&gt;.pdf</c> on GitHub Release).
/// </summary>
internal static class PublishedBooksPdf
{
    internal const string CalypsoSeries = "the-calypso-cycle";
    internal const string CalypsoBook = "calypso";

    internal static string? TryResolveCalypso()
    {
        foreach (var candidate in EnumerateCalypsoCandidates())
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCalypsoCandidates()
    {
        var overridePath = FirstNonEmpty(
            Environment.GetEnvironmentVariable("NOVOLIS_BOOKS_PDF"),
            Environment.GetEnvironmentVariable("NOVOLIS_PDFREADER_UI_PDF"));
        if (overridePath is not null)
            yield return overridePath;

        foreach (var root in EnumerateBooksRoots())
        {
            yield return Path.Combine(root, "out", CalypsoSeries, CalypsoBook, CalypsoBook + ".pdf");
            yield return Path.Combine(root, "release-staging", $"{CalypsoSeries}-{CalypsoBook}.pdf");
        }
    }

    private static IEnumerable<string> EnumerateBooksRoots()
    {
        var overrideRoot = FirstNonEmpty(Environment.GetEnvironmentVariable("NOVOLIS_BOOKS_ROOT"));
        if (overrideRoot is not null)
            yield return overrideRoot;

        yield return @"D:\repos\books";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
