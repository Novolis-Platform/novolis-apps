namespace Novolis.Hours.Domain.Calendars;

/// <summary>Generator metadata retained with a generated calendar contribution.</summary>
public sealed record CalendarRuleProvenance(
    string Jurisdiction,
    string HolidayId,
    string SourcePackage,
    string SourcePackageVersion,
    string GeneratorVersion);
