using ReadAloud.Services;

namespace ReadAloud.Unit;

public sealed class SpeechUsageLedgerTests
{
    [Test]
    public async Task Ledger_counts_calls_replays_and_failures_across_restarts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readaloud-usage-{Guid.NewGuid():N}.json");
        try
        {
            var ledger = new SpeechUsageLedger(path);
            ledger.RecordAzureCall(1200);
            ledger.RecordCacheReplay();
            ledger.RecordFailure("InvalidOperationException");

            var reloaded = new SpeechUsageLedger(path);
            var text = reloaded.Format();
            await Assert.That(text).Contains(1200.ToString("N0"));
            await Assert.That(text).Contains("Azure calls");
            await Assert.That(text).Contains("cache replays");
            await Assert.That(text).Contains("InvalidOperationException");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
