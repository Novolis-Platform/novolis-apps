using Novolis.Hours.Domain.Calendars;

namespace Novolis.Hours.Application;

/// <summary>One named customer system with its national and employment defaults.</summary>
public sealed record HoursCustomer(
    string Id,
    string CountryCode,
    string TimeZoneId,
    string LegalPresetId,
    TimeSpan ExpectedWork,
    LocalTimeRange Envelope,
    LocalTimeRange CoreHours,
    LocalTimeRange RoutineMorning,
    LocalTimeRange RoutineAfternoon,
    bool ObservesPublicHolidays,
    bool SevenDayOperation);
