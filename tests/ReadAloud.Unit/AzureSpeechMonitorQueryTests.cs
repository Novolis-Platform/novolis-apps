using System.Text.Json;
using ReadAloud.Services;

namespace ReadAloud.Unit;

public sealed class AzureSpeechMonitorQueryTests
{
    [Test]
    public async Task Usage_queries_keep_character_and_call_metrics_apart()
    {
        await Assert.That(AzureSpeechMonitorQuery.MetricGroups).Count().IsEqualTo(2);
        await Assert.That(AzureSpeechMonitorQuery.MetricGroups[0]).Contains("SynthesizedCharacters");
        await Assert.That(AzureSpeechMonitorQuery.MetricGroups[0]).DoesNotContain("TotalCalls");
        await Assert.That(AzureSpeechMonitorQuery.MetricGroups[1]).Contains("TotalCalls");
        await Assert.That(AzureSpeechMonitorQuery.MetricGroups[1]).DoesNotContain("SynthesizedCharacters");
    }

    [Test]
    public async Task Metrics_url_requests_one_total_for_the_window()
    {
        var start = new DateTimeOffset(2026, 8, 31, 11, 23, 45, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 30, 11, 23, 45, TimeSpan.Zero);
        var url = AzureSpeechMonitorQuery.BuildMetricsUrl(
            "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/speech",
            ["SynthesizedCharacters"],
            start,
            end);

        await Assert.That(url).Contains("interval=FULL");
        await Assert.That(url).Contains("aggregation=Total");
        await Assert.That(url).Contains("AutoAdjustTimegrain=true");
        await Assert.That(url).Contains("2026-08-31T11%3A23%3A45Z");
        await Assert.That(url).DoesNotContain("PT1H");
        await Assert.That(url).DoesNotContain("TotalCalls");
    }

    [Test]
    public async Task Reader_sums_totals_and_keeps_metric_errors()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "value": [
                {
                  "name": { "value": "SynthesizedCharacters" },
                  "timeseries": [ { "data": [ { "total": 1500 }, { "total": 20 } ] } ],
                  "errorCode": "Success"
                },
                {
                  "name": { "value": "TotalCalls" },
                  "timeseries": [],
                  "errorCode": "BadRequest",
                  "errorMessage": "Incompatible dimensions."
                }
              ]
            }
            """);
        var totals = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        var notices = new List<string>();

        AzureSpeechMonitorQuery.ReadMetrics(document.RootElement, totals, notices);

        await Assert.That(totals["SynthesizedCharacters"]).IsEqualTo(1520);
        await Assert.That(totals.ContainsKey("TotalCalls")).IsFalse();
        await Assert.That(string.Join(" ", notices)).Contains("Incompatible dimensions.");
    }
}
