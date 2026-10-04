using System.Collections.Immutable;
using Novolis.Time.Calendar.PublicHoliday;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>
/// Explicit holiday rule generated from the offline holiday package.
/// Runtime lookup uses the frozen generated input and never calls the package.
/// </summary>
public sealed class PublicHolidayCalendarRule : ICalendarRule
{
    private readonly ImmutableDictionary<DateOnly, GeneratedPublicHoliday> holidays;

    /// <summary>Initializes a rule from already generated holiday facts.</summary>
    public PublicHolidayCalendarRule(
        string id,
        string jurisdiction,
        string sourcePackage,
        string sourcePackageVersion,
        string generatorVersion,
        IEnumerable<GeneratedPublicHoliday> holidays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(jurisdiction);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePackageVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorVersion);
        ArgumentNullException.ThrowIfNull(holidays);

        Id = id;
        Jurisdiction = jurisdiction;
        SourcePackage = sourcePackage;
        SourcePackageVersion = sourcePackageVersion;
        GeneratorVersion = generatorVersion;
        this.holidays = holidays.ToImmutableDictionary(holiday => holiday.Date);
    }

    /// <summary>
    /// Generates explicit rules for one year. This method is the generation boundary;
    /// the returned rule is self-contained.
    /// </summary>
    public static PublicHolidayCalendarRule Generate(
        string id,
        int year,
        string countryCode,
        IEnumerable<DayOfWeek>? workdays = null,
        string generatorVersion = "novolis-hours-calendar-generator-1")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        var selectedWorkdays = (workdays ??
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
            ]).ToImmutableHashSet();
        var sourceCalendar = PublicHolidayWorkdayCalendarFactory.Create(
            $"{id}.{year}",
            year,
            countryCode,
            selectedWorkdays);
        var packageVersion = typeof(PublicHolidayWorkdayCalendarFactory).Assembly
            .GetName()
            .Version?
            .ToString() ?? "unknown";
        var jurisdiction = countryCode.ToUpperInvariant();
        var generated = ImmutableArray.CreateBuilder<GeneratedPublicHoliday>();

        for (var day = new DateOnly(year, 1, 1);
             day.Year == year;
             day = day.AddDays(1))
        {
            if (!selectedWorkdays.Contains(day.DayOfWeek) ||
                sourceCalendar.IsWorkday(day))
            {
                continue;
            }

            var name = sourceCalendar.GetNonWorkdayReason(day);
            if (string.IsNullOrWhiteSpace(name) ||
                name.Equals("Weekend", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var holidayId = CreateHolidayId(name);
            generated.Add(new GeneratedPublicHoliday(
                day,
                holidayId,
                name,
                new CalendarRuleProvenance(
                    jurisdiction,
                    holidayId,
                    "PublicHoliday",
                    packageVersion,
                    generatorVersion)));
        }

        return new PublicHolidayCalendarRule(
            id,
            jurisdiction,
            "PublicHoliday",
            packageVersion,
            generatorVersion,
            generated);
    }

    /// <summary>Stable selector identifier.</summary>
    public string Id { get; }

    /// <summary>Country or jurisdiction code used during generation.</summary>
    public string Jurisdiction { get; }

    /// <summary>Package that supplied the source data.</summary>
    public string SourcePackage { get; }

    /// <summary>Version of the source package.</summary>
    public string SourcePackageVersion { get; }

    /// <summary>Version of the generator that froze the input.</summary>
    public string GeneratorVersion { get; }

    /// <summary>Generated public holidays retained for inspection and replay.</summary>
    public ImmutableArray<GeneratedPublicHoliday> Holidays =>
        holidays.Values.OrderBy(holiday => holiday.Date).ToImmutableArray();

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date)
    {
        if (!holidays.TryGetValue(date, out var holiday))
        {
            return [];
        }

        return
        [
            new WorkingDayRule(false),
            new DayTagRule("holiday", holiday.Name),
        ];
    }

    /// <summary>Gets generated provenance for a date, if it is a holiday.</summary>
    public CalendarRuleProvenance? GetProvenance(DateOnly date) =>
        holidays.TryGetValue(date, out var holiday) ? holiday.Provenance : null;

    private static string CreateHolidayId(string name)
    {
        var characters = name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return characters.Length == 0 ? "holiday" : new string(characters);
    }
}
