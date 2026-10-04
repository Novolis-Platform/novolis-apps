using Novolis.Time.Worktime;

namespace Novolis.Hours.Contracts;

/// <summary>Effective immutable profile details.</summary>
public sealed record ProfileResponse(
    string Id,
    string Name,
    TimeOnly WorkingDayStart,
    TimeOnly WorkingDayEnd,
    TimeOnly CoreStart,
    TimeOnly CoreEnd,
    TimeOnly LunchStart,
    TimeOnly LunchEnd,
    TimeSpan WeekHours,
    TimeSpan DayHours,
    PresenceClassification UnmarkedSurplusClassification);
