using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>
/// Typed rule mapping neutral routine differences to configured Dimension values.
/// It does not decide whether a difference is overtime, flex, or misconduct.
/// </summary>
public sealed class RoutineDifferenceDimensionRule : IDimensionRule
{
    /// <summary>Initializes a routine-difference mapping.</summary>
    public RoutineDifferenceDimensionRule(
        string dimensionId,
        string outsideRoutineValueId,
        string? missingRoutineValueId,
        RuleRef? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outsideRoutineValueId);
        DimensionId = dimensionId;
        OutsideRoutineValueId = outsideRoutineValueId;
        MissingRoutineValueId = missingRoutineValueId;
        Provenance = provenance;
    }

    /// <summary>Dimension identity.</summary>
    public string DimensionId { get; }

    /// <summary>Value used for observed outside-routine work.</summary>
    public string OutsideRoutineValueId { get; }

    /// <summary>Optional value used for an unobserved routine duration.</summary>
    public string? MissingRoutineValueId { get; }

    /// <summary>Optional provenance.</summary>
    public RuleRef? Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<DimensionMeasure> Evaluate(DimensionContext context)
    {
        var result = new List<DimensionMeasure>();
        if (context.WorkDay.RoutineComparison.OutsideRoutine > TimeSpan.Zero)
        {
            result.Add(new DimensionMeasure(
                DimensionId,
                OutsideRoutineValueId,
                context.WorkDay.RoutineComparison.OutsideRoutine,
                null,
                DimensionMeasureSource.Derived,
                Provenance));
        }

        if (MissingRoutineValueId is not null &&
            context.WorkDay.RoutineComparison.MissingRoutine > TimeSpan.Zero)
        {
            result.Add(new DimensionMeasure(
                DimensionId,
                MissingRoutineValueId,
                -context.WorkDay.RoutineComparison.MissingRoutine,
                null,
                DimensionMeasureSource.Derived,
                Provenance));
        }

        return result;
    }
}
