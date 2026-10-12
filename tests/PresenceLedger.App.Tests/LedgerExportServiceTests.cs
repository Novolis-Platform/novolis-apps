using PresenceLedger.App;
using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.App.Tests;

public sealed class LedgerExportServiceTests
{
    [Test]
    public async Task Build_creates_the_cumulative_file_and_records_the_export()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Novolis-PresenceLedger-ExportTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new NdjsonPresenceStorage(root);
            await storage.Observations.AppendAsync(
                new PresenceObservationRecord(
                    DateTimeOffset.Parse("2026-10-03T12:00:00Z"),
                    null,
                    null,
                    RecordedWifiStatus.Unavailable,
                    null));
            var service = new LedgerExportService(storage, storage.Diagnostics);

            var count = await service.BuildAsync();
            var diagnostics = await ReadAll(storage.Diagnostics.ReadAsync());

            await Assert.That(count).IsEqualTo(1);
            await Assert.That(File.Exists(storage.CumulativeFilePath)).IsTrue();
            await Assert.That(diagnostics.Any(item => item.Code == "export-built"))
                .IsTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    static async Task<List<T>> ReadAll<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
            values.Add(value);
        return values;
    }
}
