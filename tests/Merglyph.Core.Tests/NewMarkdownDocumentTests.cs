namespace Merglyph.Core.Tests;

public sealed class NewMarkdownDocumentTests
{
    [Test]
    public async Task CreateUniquePath_UsesUntitledThenNumberedSuffix()
    {
        var root = Path.Combine(Path.GetTempPath(), "merglyph-new-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = NewMarkdownDocument.CreateUniquePath(root);
            await File.WriteAllTextAsync(first, NewMarkdownDocument.DefaultContent);
            var second = NewMarkdownDocument.CreateUniquePath(root);

            await Assert.That(Path.GetFileName(first)).IsEqualTo("Untitled.md");
            await Assert.That(Path.GetFileName(second)).IsEqualTo("Untitled 2.md");
            await Assert.That(Path.IsPathRooted(first)).IsTrue();
            await Assert.That(Path.GetFullPath(first).StartsWith(
                    Path.GetFullPath(root),
                    StringComparison.OrdinalIgnoreCase))
                .IsTrue();
            await Assert.That(Path.GetExtension(first)).IsEqualTo(".md");
            await Assert.That(NewMarkdownDocument.DefaultContent).Contains("# Untitled");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Resolve_UsesMyDocumentsWhenAvailable()
    {
        // MyDocuments and Personal share the same enum value on Windows.
        var documents = Path.Combine("Users", "demo", "Documents");
        var path = UserDocumentsPaths.Resolve(folder =>
            folder is Environment.SpecialFolder.MyDocuments
                or Environment.SpecialFolder.Personal
                ? documents
                : string.Empty);

        await Assert.That(path).IsEqualTo(documents);
    }

    [Test]
    public async Task Resolve_FallsBackToProfileDocuments()
    {
        var profile = Path.Combine("Users", "demo");
        var path = UserDocumentsPaths.Resolve(folder =>
            folder == Environment.SpecialFolder.UserProfile
                ? profile
                : string.Empty);

        await Assert.That(path).IsEqualTo(Path.Combine(profile, "Documents"));
    }

    [Test]
    public async Task EnsureWritable_FallsBackWhenPreferredIsUnusable()
    {
        var preferred = Path.Combine(
            Path.GetTempPath(),
            "merglyph-missing-" + Guid.NewGuid().ToString("N"),
            "nested");
        var fallback = Path.Combine(Path.GetTempPath(), "merglyph-fallback-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Preferred parent is a file, so directory create under it fails.
            var blocker = Path.GetDirectoryName(preferred)!;
            Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
            await File.WriteAllTextAsync(blocker, "not-a-directory");

            var claimed = UserDocumentsPaths.EnsureWritable(preferred, fallback);
            await Assert.That(claimed).IsEqualTo(fallback);
            await Assert.That(Directory.Exists(fallback)).IsTrue();
        }
        finally
        {
            var blocker = Path.GetDirectoryName(preferred)!;
            if (File.Exists(blocker))
                File.Delete(blocker);
            if (Directory.Exists(fallback))
                Directory.Delete(fallback, recursive: true);
        }
    }
}
