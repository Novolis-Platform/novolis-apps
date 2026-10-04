using System.Collections.Immutable;
using Novolis.Time.Workday;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Hours facade over frozen Workday public-holiday facts.</summary>
public static class NationalHolidayCatalog
{
    /// <summary>Source package recorded by the Workday generator.</summary>
    public static string SourcePackage => GeneratedHolidayCatalog.Current.SourcePackage;

    /// <summary>Source package version recorded by the Workday generator.</summary>
    public static string SourcePackageVersion => GeneratedHolidayCatalog.Current.SourcePackageVersion;

    /// <summary>Generator version recorded with the frozen catalog.</summary>
    public static string GeneratorVersion => GeneratedHolidayCatalog.Current.GeneratorVersion;

    /// <summary>Returns generated holidays for one jurisdiction.</summary>
    public static ImmutableArray<GeneratedPublicHoliday> For(string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        var jurisdiction = countryCode.ToUpperInvariant();
        var catalog = GeneratedHolidayCatalog.Current;
        return GeneratedHolidayCatalog.GetYears(jurisdiction)
            .SelectMany(year => GeneratedHolidayCatalog.GetHolidays(jurisdiction, year))
            .Select(fact => ToGenerated(jurisdiction, fact, catalog))
            .ToImmutableArray();
    }

    /// <summary>Returns generated holiday dates for one jurisdiction.</summary>
    public static ImmutableArray<DateOnly> Dates(string countryCode) =>
        For(countryCode).Select(holiday => holiday.Date).ToImmutableArray();

    private static GeneratedPublicHoliday ToGenerated(
        string jurisdiction,
        PublicHolidayFact fact,
        GeneratedHolidayDocument catalog)
    {
        var holidayId = CreateHolidayId(fact.Name);
        return new GeneratedPublicHoliday(
            fact.Date,
            holidayId,
            fact.Name,
            new CalendarRuleProvenance(
                jurisdiction,
                holidayId,
                catalog.SourcePackage,
                catalog.SourcePackageVersion,
                catalog.GeneratorVersion));
    }

    private static string CreateHolidayId(string name)
    {
        var characters = name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return characters.Length == 0 ? "holiday" : new string(characters);
    }
}
