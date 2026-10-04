namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Origin of a derived Dimension measure.</summary>
public enum DimensionMeasureSource
{
    /// <summary>Produced by a typed configured rule.</summary>
    Derived,

    /// <summary>Recorded by a human after work was resolved.</summary>
    Manual,

    /// <summary>Produced by a system projector or imported fact.</summary>
    System,
}
