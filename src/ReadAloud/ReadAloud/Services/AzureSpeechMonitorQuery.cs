using System.Text.Json;

namespace ReadAloud.Services;

/// <summary>
/// Builds and reads Azure Monitor usage queries for a Speech resource.
/// Character metrics and call metrics use different dimensions, so they are
/// separate requests. <c>interval=FULL</c> returns one total for the window.
/// </summary>
public static class AzureSpeechMonitorQuery
{
    public const string MetricsApiVersion = "2023-10-01";
    public const string MetricNamespace = "Microsoft.CognitiveServices/accounts";

    public static readonly IReadOnlyList<string[]> MetricGroups =
    [
        ["SynthesizedCharacters"],
        ["TotalCalls", "SuccessfulCalls", "ClientErrors", "ServerErrors"],
    ];

    public static string FormatTimespan(DateTimeOffset start, DateTimeOffset end) =>
        $"{start.UtcDateTime:yyyy-MM-ddTHH:mm:ss}Z/{end.UtcDateTime:yyyy-MM-ddTHH:mm:ss}Z";

    public static string BuildMetricsUrl(
        string resourceId,
        IReadOnlyList<string> metricNames,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentNullException.ThrowIfNull(metricNames);
        if (metricNames.Count == 0)
            throw new ArgumentException("At least one metric is required.", nameof(metricNames));

        var names = string.Join(',', metricNames.Select(Uri.EscapeDataString));
        var timespan = Uri.EscapeDataString(FormatTimespan(start, end));
        return "https://management.azure.com" +
            resourceId +
            "/providers/Microsoft.Insights/metrics" +
            $"?metricnames={names}" +
            "&aggregation=Total" +
            "&interval=FULL" +
            $"&timespan={timespan}" +
            $"&metricnamespace={Uri.EscapeDataString(MetricNamespace)}" +
            "&AutoAdjustTimegrain=true" +
            $"&api-version={MetricsApiVersion}";
    }

    public static void ReadMetrics(
        JsonElement root,
        IDictionary<string, long?> totals,
        ICollection<string> notices)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentNullException.ThrowIfNull(notices);
        if (!root.TryGetProperty("value", out var metrics) ||
            metrics.ValueKind != JsonValueKind.Array)
        {
            notices.Add("Azure Monitor returned no metric list.");
            return;
        }

        foreach (var metric in metrics.EnumerateArray())
        {
            var name = MetricName(metric);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (metric.TryGetProperty("errorCode", out var errorCode) &&
                errorCode.ValueKind == JsonValueKind.String &&
                !string.Equals(errorCode.GetString(), "Success", StringComparison.OrdinalIgnoreCase))
            {
                var message = StringProperty(metric, "errorMessage");
                notices.Add(string.IsNullOrWhiteSpace(message)
                    ? $"{name} was not returned ({errorCode.GetString()})."
                    : $"{name}: {message}");
                continue;
            }

            totals[name] = SumTotals(metric);
        }
    }

    static long SumTotals(JsonElement metric)
    {
        double total = 0;
        var foundValue = false;
        if (metric.TryGetProperty("timeseries", out var series) &&
            series.ValueKind == JsonValueKind.Array)
        {
            foreach (var timeSeries in series.EnumerateArray())
            {
                if (!timeSeries.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var point in data.EnumerateArray())
                {
                    if (point.TryGetProperty("total", out var pointTotal) &&
                        pointTotal.ValueKind == JsonValueKind.Number &&
                        pointTotal.TryGetDouble(out var number))
                    {
                        total += number;
                        foundValue = true;
                    }
                }
            }
        }

        if (!foundValue)
            return 0;
        if (total >= long.MaxValue)
            return long.MaxValue;
        if (total <= long.MinValue)
            return long.MinValue;
        return checked((long)Math.Round(total, MidpointRounding.AwayFromZero));
    }

    static string? MetricName(JsonElement metric)
    {
        if (!metric.TryGetProperty("name", out var name))
            return null;
        if (name.ValueKind == JsonValueKind.String)
            return name.GetString();
        return name.ValueKind == JsonValueKind.Object
            ? StringProperty(name, "value")
            : null;
    }

    static string? StringProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
