using System.Collections.Immutable;

namespace Novolis.Hours.Client.Presentation;

/// <summary>One ISO week that intersects a civil month. The week number is first-class, not a label on a day.</summary>
public sealed record HoursMonthWeek(
    int WeekYear,
    int WeekNumber,
    DateOnly Monday,
    ImmutableArray<HoursMonthDay> Days)
{
    /// <summary>Sunday of this ISO week.</summary>
    public DateOnly Sunday => Monday.AddDays(6);

    /// <summary>Week number shown in the month gutter, for example W40.</summary>
    public string NumberLabel => $"W{WeekNumber}";
}
