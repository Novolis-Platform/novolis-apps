using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>One semantic rule contribution with enough provenance to explain a DayShape.</summary>
public sealed record AppliedDayRule(
    string RuleId,
    string CalendarId,
    string CalendarVersion,
    int Order,
    DayRule Rule,
    RuleSource Source)
{
    /// <summary>Optional source-package metadata for generated rules.</summary>
    public CalendarRuleProvenance? Provenance { get; init; }
}
