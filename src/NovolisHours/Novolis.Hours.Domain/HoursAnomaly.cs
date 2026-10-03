namespace Novolis.Hours.Domain;

/// <summary>An informational compliance or workflow exception that never locks out later registration.</summary>
public sealed record HoursAnomaly(
    string Code,
    string Message,
    DateTimeOffset ObservedAtUtc,
    Guid? RelatedRecordId = null);
