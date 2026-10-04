using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Immutable definition of one configurable business meaning axis.</summary>
public sealed record DimensionDefinition
{
    /// <summary>Initializes a Dimension definition.</summary>
    public DimensionDefinition(
        string id,
        string name,
        DimensionAssignmentMode assignmentMode,
        DimensionCardinality cardinality,
        bool requireFullCoverage,
        IEnumerable<DimensionValue> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        Id = id;
        Name = name;
        AssignmentMode = assignmentMode;
        Cardinality = cardinality;
        RequireFullCoverage = requireFullCoverage;
        Values = values
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToImmutableArray();
    }

    /// <summary>Stable Dimension identifier.</summary>
    public string Id { get; }

    /// <summary>Human-readable Dimension name.</summary>
    public string Name { get; }

    /// <summary>Allowed assignment source.</summary>
    public DimensionAssignmentMode AssignmentMode { get; }

    /// <summary>Overlap behavior for measures.</summary>
    public DimensionCardinality Cardinality { get; }

    /// <summary>Whether reporting should identify uncovered resolved work.</summary>
    public bool RequireFullCoverage { get; }

    /// <summary>Configured values.</summary>
    public ImmutableArray<DimensionValue> Values { get; }

    /// <summary>Returns whether a value belongs to the definition.</summary>
    public bool HasValue(string valueId) =>
        Values.Any(value => value.Id.Equals(valueId, StringComparison.Ordinal));
}
