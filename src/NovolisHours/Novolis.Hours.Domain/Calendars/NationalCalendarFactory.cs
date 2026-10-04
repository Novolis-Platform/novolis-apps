using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Builds sparse national and holiday-observance layers from generated data.</summary>
public static class NationalCalendarFactory
{
    /// <summary>Creates the national weekend, weekday, and public-holiday tag layer.</summary>
    public static WorkCalendarLayer CreateNationalLayer(
        string countryCode,
        string version,
        bool weekdaysAreWorkingDays = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var jurisdiction = countryCode.ToUpperInvariant();
        var holidays = NationalHolidayCatalog.For(jurisdiction);
        IWorkCalendarRule[] rules =
        [
            new WeekdayCalendarRule(
                $"{jurisdiction.ToLowerInvariant()}.national.weekdays",
                [
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                ],
                [new WorkingDayRule(weekdaysAreWorkingDays)]),
            new WeekdayCalendarRule(
                $"{jurisdiction.ToLowerInvariant()}.national.weekend",
                [DayOfWeek.Saturday, DayOfWeek.Sunday],
                [new WorkingDayRule(false)]),
            new PublicHolidayCalendarRule(
                $"{jurisdiction.ToLowerInvariant()}.public-holidays",
                jurisdiction,
                NationalHolidayCatalog.SourcePackage,
                NationalHolidayCatalog.SourcePackageVersion,
                NationalHolidayCatalog.GeneratorVersion,
                holidays),
        ];

        return new WorkCalendarLayer(
            $"{jurisdiction.ToLowerInvariant()}.national",
            version,
            CalendarLayerOrders.National,
            rules,
            RuleSource.Generated,
            kind: CalendarLayerKind.National);
    }

    /// <summary>Creates the office opinion that generated holidays are not worked.</summary>
    public static WorkCalendarLayer CreateHolidayObservanceLayer(
        string countryCode,
        string version,
        TimeSpan paidEntitlement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var jurisdiction = countryCode.ToUpperInvariant();
        return new WorkCalendarLayer(
            $"{jurisdiction.ToLowerInvariant()}.holiday-observance",
            version,
            CalendarLayerOrders.Organisation,
            [
                new PublicHolidayObservanceCalendarRule(
                    $"{jurisdiction.ToLowerInvariant()}.observe-public-holidays",
                    NationalHolidayCatalog.For(jurisdiction),
                    paidEntitlement),
            ],
            RuleSource.Manual,
            kind: CalendarLayerKind.Organisation);
    }
}
