namespace ReadAloud.Reading;

/// <summary>One listen, without the spoken text.</summary>
public sealed record SpeechOperationReport(
    string Phase,
    string Voice,
    int Characters,
    int SegmentsCompleted,
    int SegmentCount,
    long ElapsedMilliseconds,
    int AzureCalls,
    int CacheReplays,
    string? Failure)
{
    public bool IncludesSecret(string secret) =>
        !string.IsNullOrEmpty(secret) &&
        (Phase.Contains(secret, StringComparison.Ordinal) ||
         Voice.Contains(secret, StringComparison.Ordinal) ||
         (Failure?.Contains(secret, StringComparison.Ordinal) ?? false));
}
