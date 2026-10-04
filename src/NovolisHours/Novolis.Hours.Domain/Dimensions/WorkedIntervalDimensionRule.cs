using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Typed rule that paints each resolved work interval with one value.</summary>
public sealed class WorkedIntervalDimensionRule : IDimensionRule
{
    /// <summary>Initializes an interval-painting rule.</summary>
    public WorkedIntervalDimensionRule(
        string dimensionId,
        string valueId,
        DimensionMeasureSource source = DimensionMeasureSource.Derived,
        RuleRef? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueId);
        if (source == DimensionMeasureSource.Manual)
        {
            throw new ArgumentException(
                "An interval rule cannot produce manual measures.",
                nameof(source));
        }

        DimensionId = dimensionId;
        ValueId = valueId;
        Source = source;
        Provenance = provenance;
    }

    /// <summary>Dimension identity.</summary>
    public string DimensionId { get; }

    /// <summary>Value identity.</summary>
    public string ValueId { get; }

    /// <summary>Output source, normally Derived.</summary>
    public DimensionMeasureSource Source { get; }

    /// <summary>Optional rule provenance; configuration fills it when omitted.</summary>
    public RuleRef? Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<DimensionMeasure> Evaluate(DimensionContext context) =>
        context.WorkDay.WorkedIntervals
            .Select(interval => new DimensionMeasure(
                DimensionId,
                ValueId,
                interval.Duration,
                interval,
                Source,
                Provenance))
            .ToArray();
}
