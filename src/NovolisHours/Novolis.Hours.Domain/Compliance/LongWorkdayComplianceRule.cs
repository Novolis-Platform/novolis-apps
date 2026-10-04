using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Reports a factual worked-duration threshold observation.</summary>
public sealed class LongWorkdayComplianceRule : IComplianceRule
{
    /// <summary>Initializes a threshold rule.</summary>
    public LongWorkdayComplianceRule(TimeSpan maximumDuration, RuleRef provenance)
    {
        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        ArgumentNullException.ThrowIfNull(provenance);
        MaximumDuration = maximumDuration;
        Provenance = provenance;
    }

    /// <summary>Configured duration threshold.</summary>
    public TimeSpan MaximumDuration { get; }

    /// <summary>Rule provenance.</summary>
    public RuleRef Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<ComplianceIndicator> Evaluate(ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.WorkDay.ActualWorked <= MaximumDuration)
        {
            return [];
        }

        var affected = context.WorkDay.WorkedIntervals.IsEmpty
            ? null
            : new Novolis.Hours.Domain.Work.WorkInterval(
                context.WorkDay.WorkedIntervals[0].Start,
                context.WorkDay.WorkedIntervals[^1].End);
        return
        [
            new ComplianceIndicator(
                "worked-duration.above-threshold",
                "WorkedDuration",
                $"Recorded worked duration {context.WorkDay.ActualWorked} exceeds the configured threshold of {MaximumDuration}.",
                context.WorkDay.Key,
                affected,
                Provenance),
        ];
    }
}
