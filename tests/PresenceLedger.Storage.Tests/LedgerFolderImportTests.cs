using PresenceLedger.Storage;

namespace PresenceLedger.Storage.Tests;

public sealed class LedgerFolderImportTests
{
    [Test]
    public async Task Import_copies_real_files_and_skips_junk()
    {
        var source = Path.Combine(Path.GetTempPath(), "presence-dump-src-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(Path.GetTempPath(), "presence-dump-dst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(source, "observations"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "locations.ndjson"), "{\"ok\":true}\n");
            await File.WriteAllTextAsync(Path.Combine(source, "presence.ndjson"), "{\"event\":1}\n");
            await File.WriteAllTextAsync(Path.Combine(source, "states.ndjson"), "{\"state\":1}\n");
            await File.WriteAllTextAsync(Path.Combine(source, "diagnostics.ndjson"), "{\"noise\":1}\n");
            await File.WriteAllTextAsync(
                Path.Combine(source, "locations.ndjson (12)"),
                "junk");
            await File.WriteAllTextAsync(
                Path.Combine(source, ".pending-1-locations.ndjson"),
                "junk");
            await File.WriteAllTextAsync(
                Path.Combine(source, "observations", "2026-11-08.ndjson"),
                "{\"at\":\"2026-11-08T00:00:00Z\"}\n");
            await File.WriteAllTextAsync(
                Path.Combine(source, "observations", "locations.ndjson (3)"),
                "junk");

            var result = LedgerFolderImport.Import(source, destination, includeDiagnostics: false);

            await Assert.That(result.FilesCopied).IsEqualTo(4);
            await Assert.That(result.FilesSkipped).IsGreaterThanOrEqualTo(2);
            await Assert.That(File.Exists(Path.Combine(destination, "locations.ndjson"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(destination, "observations", "2026-11-08.ndjson")))
                .IsTrue();
            await Assert.That(File.Exists(Path.Combine(destination, "diagnostics.ndjson"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(destination, "locations.ndjson (12)"))).IsFalse();
        }
        finally
        {
            if (Directory.Exists(source))
                Directory.Delete(source, recursive: true);
            if (Directory.Exists(destination))
                Directory.Delete(destination, recursive: true);
        }
    }

    [Test]
    public async Task IsJunkName_detects_pending_and_collision_copies()
    {
        await Assert.That(LedgerFolderImport.IsJunkName(".pending-179-locations.ndjson")).IsTrue();
        await Assert.That(LedgerFolderImport.IsJunkName("locations.ndjson (12)")).IsTrue();
        await Assert.That(LedgerFolderImport.IsJunkName("locations.ndjson")).IsFalse();
        await Assert.That(LedgerFolderImport.IsJunkName("2026-11-08.ndjson")).IsFalse();
    }
}
