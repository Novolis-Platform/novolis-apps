namespace Novolis.Hours.Domain.Calendars;

/// <summary>Answers the semantic contributions a selector has for a local date.</summary>
public interface ICalendarRule
{
    /// <summary>Returns zero or more sparse contributions for the date.</summary>
    IReadOnlyList<DayRule> GetRules(DateOnly date);
}
