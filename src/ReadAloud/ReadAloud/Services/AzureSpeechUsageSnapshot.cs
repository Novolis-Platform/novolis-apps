namespace ReadAloud.Services;

/// <summary>Azure Monitor usage totals for one Speech resource and time window.</summary>
public sealed record AzureSpeechUsageSnapshot(
    DateTimeOffset Start,
    DateTimeOffset End,
    long? SynthesizedCharacters,
    long? TotalCalls,
    long? SuccessfulCalls,
    long? ClientErrors,
    long? ServerErrors,
    string? Notice = null);
