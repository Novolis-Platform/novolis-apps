using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Reports recorded work on a weekend as a neutral fact.</summary>
public sealed class WeekendWorkComplianceRule : IComplianceRule
{
    /// <summary>Initializes the rule.</summary>
    public WeekendWorkComplianceRule(RuleRef provenance)
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
        if (context.WorkDay.Shape.Date.DayOfWeek is not
            (DayOfWeek.Saturday or DayOfWeek.Sunday) ||
            context.WorkDay.ActualWorked <= TimeSpan.Zero)
        {
            return [];
        }

        return
        [
            new ComplianceIndicator(
                "work.weekend",
                "WeekendWork",
                "Recorded work occurs on a weekend date.",
                context.WorkDay.Key,
                null,
                Provenance),
        ];
    }
}
