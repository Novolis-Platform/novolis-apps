using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Typed rule for a signed duration meaning without inventing an interval.</summary>
public sealed class SignedDurationDimensionRule : IDimensionRule
{
    /// <summary>Initializes a fixed signed-duration rule.</summary>
    public SignedDurationDimensionRule(
        string dimensionId,
        string valueId,
        TimeSpan duration,
        RuleRef? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueId);
        DimensionId = dimensionId;
        ValueId = valueId;
        Duration = duration;
        Provenance = provenance;
    }

    /// <summary>Dimension identity.</summary>
    public string DimensionId { get; }

    /// <summary>Value identity.</summary>
    public string ValueId { get; }

    /// <summary>Signed output duration.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Optional provenance.</summary>
    public RuleRef? Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<DimensionMeasure> Evaluate(DimensionContext context) =>
    [
        new DimensionMeasure(
            DimensionId,
            ValueId,
            Duration,
            null,
            DimensionMeasureSource.Derived,
            Provenance),
    ];
}
