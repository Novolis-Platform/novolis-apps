namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Whether values on a Dimension may overlap.</summary>
public enum DimensionCardinality
{
    /// <summary>One effective value may cover a temporal slice.</summary>
    Exclusive,

    /// <summary>Multiple values may annotate the same temporal slice.</summary>
    Additive,
}
