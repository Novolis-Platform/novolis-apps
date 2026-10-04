using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>One day on the week studio.</summary>
public sealed record WeekTile(
    DateOnly Date,
    string Weekday,
    string Expected,
    bool IsWorkingDay,
    bool HasRegistration,
    bool IsToday,
    string Chip,
    WorkDayResponse Day);
