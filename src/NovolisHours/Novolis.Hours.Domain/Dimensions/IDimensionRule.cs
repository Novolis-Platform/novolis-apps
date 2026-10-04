namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Typed deterministic rule for derived Dimension measures.</summary>
public interface IDimensionRule
{
    /// <summary>Evaluates against resolved work and earlier derived output.</summary>
    IReadOnlyList<DimensionMeasure> Evaluate(DimensionContext context);
}
