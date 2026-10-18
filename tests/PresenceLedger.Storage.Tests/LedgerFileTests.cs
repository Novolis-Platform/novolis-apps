using PresenceLedger.Core;
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

    [Test]
    public async Task Catalog_includes_cumulative_and_diagnostics_exports()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(
                Path.Combine(root, "cumulative.ndjson"),
                "{}\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "diagnostics.ndjson"),
                "{}\n");

            var files = LedgerFiles.List(root);

            await Assert.That(files.Select(file => file.RelativePath).ToArray())
                .IsEquivalentTo(new[] { "cumulative.ndjson", "diagnostics.ndjson" });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Catalog_ignores_legacy_share_snapshots()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "share"));
            await File.WriteAllTextAsync(
                Path.Combine(root, "share", "cumulative.ndjson"),
                "{}\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "cumulative.ndjson"),
                "{}\n");

            var files = LedgerFiles.List(root);

            await Assert.That(files.Select(file => file.RelativePath).ToArray())
                .IsEquivalentTo(new[] { "cumulative.ndjson" });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Copy_removes_stale_ndjson_files_from_the_previous_snapshot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        var published = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(published);
            await File.WriteAllTextAsync(
                Path.Combine(root, "locations.ndjson"),
                "{}\n");
            await File.WriteAllTextAsync(
                Path.Combine(published, "old.ndjson"),
                "{}\n");

            LedgerFiles.CopyTo(root, published);

            await Assert.That(File.Exists(Path.Combine(published, "old.ndjson")))
                .IsFalse();
            await Assert.That(File.Exists(Path.Combine(published, "locations.ndjson")))
                .IsTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            if (Directory.Exists(published))
                Directory.Delete(published, recursive: true);
        }
    }

    [Test]
    public async Task Bundle_round_trip_replaces_stale_files_and_preserves_history_files()
    {
        var sourceRoot = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        var targetRoot = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(targetRoot);
            await File.WriteAllTextAsync(
                Path.Combine(sourceRoot, "locations.ndjson"),
                "{\"id\":\"place-1\",\"displayName\":\"Home\"}\n");
            await File.WriteAllTextAsync(
                Path.Combine(sourceRoot, "presence.ndjson"),
                "{\"locationId\":\"place-1\",\"transition\":\"arrived\"}\n");

            var source = new NdjsonPresenceStorage(sourceRoot);
            await source.Observations.AppendAsync(
                new PresenceObservationRecord(
                    DateTimeOffset.Parse("2026-10-01T12:00:00Z"),
                    null,
                    null,
                    RecordedWifiStatus.Unavailable,
                    null));
            var archive = new MemoryStream();
            var exported = await new LedgerBundleService(source).CreateAsync(archive);
            archive.Position = 0;

            await File.WriteAllTextAsync(
                Path.Combine(targetRoot, "stale.ndjson"),
                "{}\n");
            var imported = await new LedgerBundleService(
                new NdjsonPresenceStorage(targetRoot)).ImportAsync(archive);

            await Assert.That(exported.FileCount).IsEqualTo(imported.FileCount);
            await Assert.That(File.Exists(Path.Combine(targetRoot, "stale.ndjson")))
                .IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(targetRoot, "locations.ndjson")))
                .Contains("Home");
            await Assert.That(File.Exists(Path.Combine(
                    targetRoot,
                    "observations",
                    "2026-10-01.ndjson")))
                .IsTrue();
            await Assert.That(File.Exists(Path.Combine(targetRoot, "cumulative.ndjson")))
                .IsTrue();
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, recursive: true);
            if (Directory.Exists(targetRoot))
                Directory.Delete(targetRoot, recursive: true);
        }
    }
}
