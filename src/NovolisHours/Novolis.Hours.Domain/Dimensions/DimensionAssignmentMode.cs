namespace Novolis.Hours.Domain.Dimensions;

/// <summary>How a Dimension receives measures.</summary>
public enum DimensionAssignmentMode
{
    /// <summary>Only deterministic typed rules may assign values.</summary>
    Derived,

    /// <summary>Only post-registration human assignments may assign values.</summary>
    Manual,

    /// <summary>Both derived rules and manual assignments may assign values.</summary>
    DerivedAndManual,
}
