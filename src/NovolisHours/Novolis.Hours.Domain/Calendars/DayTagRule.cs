namespace Novolis.Hours.Domain.Calendars;

/// <summary>Additive contribution attaching a semantic label to a date.</summary>
public sealed record DayTagRule : DayRule
{
    /// <summary>Initializes a tag contribution.</summary>
    public DayTagRule(string key, string value)
    {
        Tag = new DayTag(key, value);
    }

    /// <summary>Tag carried by this rule.</summary>
    public DayTag Tag { get; }

    /// <summary>Gets the key without requiring callers to unwrap the tag.</summary>
    public string Key => Tag.Key;

    /// <summary>Gets the value without requiring callers to unwrap the tag.</summary>
    public string Value => Tag.Value;
}
