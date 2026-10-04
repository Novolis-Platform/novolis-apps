namespace Novolis.Hours.Domain.Calendars;

/// <summary>Additive semantic label retained on a resolved DayShape.</summary>
public sealed record DayTag
{
    /// <summary>Initializes a tag with non-empty key and value.</summary>
    public DayTag(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Key = key;
        Value = value;
    }

    /// <summary>Tag key.</summary>
    public string Key { get; }

    /// <summary>Tag value.</summary>
    public string Value { get; }
}
