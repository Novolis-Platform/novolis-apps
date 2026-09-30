namespace ReadAloud.Reading;

/// <summary>Renders a listen report for the diagnostics surface.</summary>
public static class OperationReportText
{
    public static string Format(SpeechOperationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var failure = string.IsNullOrWhiteSpace(report.Failure)
            ? "none"
            : report.Failure;
        return string.Join(
            Environment.NewLine,
            $"Listen {report.Phase}",
            $"Voice {report.Voice}",
            $"Characters {report.Characters}",
            $"Segments {report.SegmentsCompleted} of {report.SegmentCount}",
            $"Elapsed {report.ElapsedMilliseconds} ms",
            $"Azure calls {report.AzureCalls}, cache replays {report.CacheReplays}",
            $"Failure {failure}");
    }
}
