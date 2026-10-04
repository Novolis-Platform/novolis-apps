using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>One civil day inside a month week. Days outside the month stay visible so the week is whole.</summary>
public sealed record HoursMonthDay(
    DateOnly Date,
    bool InMonth,
    bool IsToday,
    string Weekday,
    string Chip,
    string Detail,
    WorkDayResponse? Day);
