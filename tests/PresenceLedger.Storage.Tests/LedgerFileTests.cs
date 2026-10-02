using PresenceLedger.Storage;

namespace PresenceLedger.Storage.Tests;

public sealed class LedgerFileTests
{
    [Test]
    public async Task Catalog_includes_files_already_stored_and_files_added_later()
    {
        var root = Path.Combine(Path.GetTempPath(), "presence-ledger-tests", Guid.NewGuid().ToString("N"));
        var published = Path.Combine(Path.GetTempPath(), "presence-ledger-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "observations"));
            var existing = Path.Combine(root, "observations", "2026-10-01.ndjson");
            await File.WriteAllTextAsync(existing, "{\"at\":\"2026-10-01T20:00:00Z\"}\n");
            await File.WriteAllTextAsync(Path.Combine(root, "locations.ndjson"), "{}\n");

            var first = LedgerFiles.List(root);
            await Assert.That(first.Select(file => file.RelativePath).ToArray())
                .IsEquivalentTo(new[] { "locations.ndjson", "observations/2026-10-01.ndjson" });

            var added = Path.Combine(root, "observations", "2026-10-02.ndjson");
            await File.WriteAllTextAsync(added, "{\"at\":\"2026-10-02T00:10:00Z\"}\n");
            await File.AppendAllTextAsync(existing, "{\"at\":\"2026-10-01T22:00:00Z\"}\n");

            var copied = LedgerFiles.CopyTo(root, published);

            await Assert.That(copied).IsEqualTo(3);
            await Assert.That(File.ReadAllText(Path.Combine(published, "observations", "2026-10-02.ndjson")))
                .Contains("2026-10-02");
            await Assert.That(File.ReadAllText(Path.Combine(published, "observations", "2026-10-01.ndjson")))
                .Contains("22:00:00Z");
            await Assert.That(File.Exists(Path.Combine(published, "locations.ndjson"))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            if (Directory.Exists(published))
                Directory.Delete(published, recursive: true);
        }
    }
}
