using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Reports out-of-routine work without labelling its motive or treatment.</summary>
public sealed class RoutineDifferenceComplianceRule : IComplianceRule
{
    /// <summary>Initializes the rule.</summary>
    public RoutineDifferenceComplianceRule(RuleRef provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        Provenance = provenance;
    }

    /// <summary>Rule provenance.</summary>
    public RuleRef Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<ComplianceIndicator> Evaluate(ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.WorkDay.RoutineComparison.OutsideRoutine <= TimeSpan.Zero)
        {
            return [];
        }

        return
        [
            new ComplianceIndicator(
                "work.outside-routine",
                "RoutineDifference",
                $"Recorded work includes {context.WorkDay.RoutineComparison.OutsideRoutine} outside the configured routine.",
                context.WorkDay.Key,
                null,
                Provenance),
        ];
    }
}
