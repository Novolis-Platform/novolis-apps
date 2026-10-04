using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>
/// Explicit holiday rule over frozen generated facts.
/// Runtime lookup never calls a third-party holiday package.
/// </summary>
public sealed class PublicHolidayCalendarRule : IWorkCalendarRule
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

        return [new DayTagRule("PublicHoliday", holiday.Name)];
    }

    /// <summary>Gets generated provenance for a date, if it is a holiday.</summary>
    public CalendarRuleProvenance? GetProvenance(DateOnly date) =>
        holidays.TryGetValue(date, out var holiday) ? holiday.Provenance : null;
}
