using Novolis.Time;
using Novolis.Time.Calendar.PublicHoliday;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Domain;

/// <summary>Builds transparent starter policies for supported locales; draft legal presets remain visibly draft.</summary>
public static class HoursPolicyCatalog
{
    /// <summary>Creates the configured policy by stable legal preset identifier.</summary>
    public static HoursPolicy Create(string presetId, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(year);

        return presetId switch
        {
            "norway.private.flex" => NorwegianHoursPolicy.CreatePrivateFlex(year),
            "norway.state.flex" => NorwegianHoursPolicy.CreateStateHandbook(year),
            "belgium.office.flex" => CreateOfficeStarter(
                "belgium.office.flex",
                "Belgium office flex starter",
                WorktimeLegalPresets.Belgium,
                new TimeOnly(8, 0),
                new TimeOnly(16, 6),
                new TimeOnly(9, 0),
                new TimeOnly(15, 0),
                new TimeOnly(12, 0),
                new TimeOnly(12, 30),
                TimeSpan.FromHours(38),
                TimeSpan.FromHours(7.6),
                FlexSettlementCadence.Quarterly),
            "england.office.flex" => CreateOfficeStarter(
                "england.office.flex",
                "England office flex starter",
                WorktimeLegalPresets.England,
                new TimeOnly(8, 0),
                new TimeOnly(16, 0),
                new TimeOnly(9, 30),
                new TimeOnly(15, 30),
                new TimeOnly(12, 30),
                new TimeOnly(13, 0),
                TimeSpan.FromHours(37.5),
                TimeSpan.FromHours(7.5),
                FlexSettlementCadence.Monthly),
            "france.annualisation" => CreateOfficeStarter(
                "france.annualisation",
                "France annualisation starter",
                WorktimeLegalPresets.France,
                new TimeOnly(9, 0),
                new TimeOnly(17, 0),
                new TimeOnly(10, 0),
                new TimeOnly(16, 0),
                new TimeOnly(12, 0),
                new TimeOnly(13, 0),
                TimeSpan.FromHours(35),
                TimeSpan.FromHours(7),
                FlexSettlementCadence.Annual),
            "poland.okres-rozliczeniowy" => CreateOfficeStarter(
                "poland.okres-rozliczeniowy",
                "Poland settlement-period starter",
                WorktimeLegalPresets.Poland,
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new TimeOnly(9, 0),
                new TimeOnly(15, 0),
                new TimeOnly(12, 0),
                new TimeOnly(12, 30),
                TimeSpan.FromHours(40),
                TimeSpan.FromHours(8),
                FlexSettlementCadence.Monthly),
            "finland.liukuva-tyoaika" => CreateOfficeStarter(
                "finland.liukuva-tyoaika",
                "Finland flexible-work starter",
                WorktimeLegalPresets.Finland,
                new TimeOnly(8, 0),
                new TimeOnly(16, 0),
                new TimeOnly(9, 0),
                new TimeOnly(15, 0),
                new TimeOnly(11, 30),
                new TimeOnly(12, 0),
                TimeSpan.FromHours(37.5),
                TimeSpan.FromHours(7.5),
                FlexSettlementCadence.Monthly),
            _ => throw new ArgumentOutOfRangeException(
                nameof(presetId),
                presetId,
                "Unknown Hours legal preset identifier."),
        };
    }

    /// <summary>Lists stable identifiers available to the tenant configuration surface.</summary>
    public static IReadOnlyList<string> PresetIds { get; } =
    [
        WorktimeLegalPresets.NorwayPrivate.Id,
        WorktimeLegalPresets.NorwayState.Id,
        WorktimeLegalPresets.Belgium.Id,
        WorktimeLegalPresets.England.Id,
        WorktimeLegalPresets.France.Id,
        WorktimeLegalPresets.Poland.Id,
        WorktimeLegalPresets.Finland.Id,
    ];

    private static HoursPolicy CreateOfficeStarter(
        string id,
        string profileName,
        WorktimeLegalPreset legalPreset,
        TimeOnly expectedStart,
        TimeOnly expectedEnd,
        TimeOnly coreStart,
        TimeOnly coreEnd,
        TimeOnly lunchStart,
        TimeOnly lunchEnd,
        TimeSpan weekHours,
        TimeSpan dayHours,
        FlexSettlementCadence cadence)
    {
        var calendar = new PublicHolidayWorkdayCalendar($"{id}.calendar", legalPreset.CountryCode);
        var expected = new ClockInterval(expectedStart, expectedEnd);
        var profile = new WorktimeProfile(
            $"{id}.profile",
            profileName,
            new ClockInterval(new TimeOnly(7, 0), new TimeOnly(19, 0)),
            new ClockInterval(coreStart, coreEnd),
            new ClockInterval(lunchStart, lunchEnd),
            weekHours,
            dayHours,
            PresenceClassification.Flex);
        var template = new ExpectedDayTemplate(
            $"{id}.template",
            $"{profileName} expected weekday",
            [
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Monday, expected),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Tuesday, expected),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Wednesday, expected),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Thursday, expected),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Friday, expected),
            ]);

        return new HoursPolicy(
            id,
            new EmploymentSettings(id, profile, calendar, template, 1m),
            legalPreset,
            calendar,
            new FlexSettlementPolicy($"{id}.settlement", cadence, RequiresEmployeeAcceptance: true));
    }
}
