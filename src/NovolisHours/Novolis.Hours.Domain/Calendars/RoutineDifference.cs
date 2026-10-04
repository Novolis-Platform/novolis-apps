namespace Novolis.Hours.Domain.Calendars;

/// <summary>One explainable difference between a resolved routine and observed work.</summary>
public sealed record RoutineDifference(
    RoutineDifferenceKind Kind,
    TimeSpan Duration,
    string Description);
