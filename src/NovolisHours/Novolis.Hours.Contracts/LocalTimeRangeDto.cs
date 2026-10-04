namespace Novolis.Hours.Contracts;

/// <summary>Structured local clock range for day-strip clients.</summary>
public sealed record LocalTimeRangeDto(TimeOnly Start, TimeOnly End);
