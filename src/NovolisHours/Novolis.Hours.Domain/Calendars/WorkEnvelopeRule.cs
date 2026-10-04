namespace Novolis.Hours.Domain.Calendars;

/// <summary>Exclusive local-clock envelope used for comparison and compliance reporting.</summary>
public sealed record WorkEnvelopeRule(LocalTimeRange Range) : DayRule;
