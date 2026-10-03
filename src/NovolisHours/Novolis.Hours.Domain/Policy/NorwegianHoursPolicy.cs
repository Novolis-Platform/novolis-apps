using Novolis.Time;
using Novolis.Time.Calendar.PublicHoliday;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Domain;

/// <summary>Builds the documented Norwegian 37.5-hour weekday profile and starter legal presets.</summary>
public static class NorwegianHoursPolicy
{
    /// <summary>Builds the standard private-agreement profile for one public-holiday calendar year.</summary>
    public static HoursPolicy Create(int year) => CreatePrivateFlex(year);

    /// <summary>Builds a private Norwegian flex-time profile with an 08:00–16:00 expected template.</summary>
    public static HoursPolicy CreatePrivateFlex(int year) =>
        Create(year, "norway.private.standard", WorktimeLegalPresets.NorwayPrivate);

    /// <summary>Builds the Norwegian government-handbook starter policy with its separate carry rule.</summary>
    public static HoursPolicy CreateStateHandbook(int year) =>
        Create(year, "norway.state.standard", WorktimeLegalPresets.NorwayState);

    private static HoursPolicy Create(int year, string id, WorktimeLegalPreset legalPreset)
    {
        var calendar = new PublicHolidayWorkdayCalendar($"{id}.calendar", "NO");
        var profile = new WorktimeProfile(
            $"{id}.profile",
            "Norway standard 37.5-hour week",
            new ClockInterval(new TimeOnly(7, 0), new TimeOnly(17, 0)),
            new ClockInterval(new TimeOnly(9, 0), new TimeOnly(15, 0)),
            new ClockInterval(new TimeOnly(11, 30), new TimeOnly(12, 0)),
            TimeSpan.FromHours(37.5),
            TimeSpan.FromHours(7.5),
            PresenceClassification.Flex);
        var template = new ExpectedDayTemplate(
            $"{id}.template",
            "Default 08:00–16:00",
            [
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Monday, new ClockInterval(new TimeOnly(8, 0), new TimeOnly(16, 0))),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Tuesday, new ClockInterval(new TimeOnly(8, 0), new TimeOnly(16, 0))),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Wednesday, new ClockInterval(new TimeOnly(8, 0), new TimeOnly(16, 0))),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Thursday, new ClockInterval(new TimeOnly(8, 0), new TimeOnly(16, 0))),
                new KeyValuePair<DayOfWeek, ClockInterval>(DayOfWeek.Friday, new ClockInterval(new TimeOnly(8, 0), new TimeOnly(16, 0))),
            ]);

        return new HoursPolicy(
            id,
            new EmploymentSettings(id, profile, calendar, template, 1m),
            legalPreset,
            calendar,
            new FlexSettlementPolicy(
                $"{id}.quarterly-flex-settlement",
                FlexSettlementCadence.Quarterly,
                RequiresEmployeeAcceptance: true));
    }
}
