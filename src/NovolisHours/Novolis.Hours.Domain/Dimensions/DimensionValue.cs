namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Stable configured value on a Dimension.</summary>
public sealed record DimensionValue
{
    /// <summary>Initializes a value.</summary>
    public DimensionValue(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    /// <summary>Stable value identifier.</summary>
    public string Id { get; }

    /// <summary>Human-readable value name.</summary>
    public string Name { get; }
}
