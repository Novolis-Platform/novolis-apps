using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Reports a short rest interval between adjacent resolved workdays.</summary>
public sealed class InsufficientDailyRestComplianceRule : IComplianceRule
{
    /// <summary>Initializes a minimum-rest rule.</summary>
    public InsufficientDailyRestComplianceRule(
        TimeSpan minimumRest,
        RuleRef provenance)
    {
        if (minimumRest < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRest));
        }

        ArgumentNullException.ThrowIfNull(provenance);
        MinimumRest = minimumRest;
        Provenance = provenance;
    }

    /// <summary>Configured minimum rest duration.</summary>
    public TimeSpan MinimumRest { get; }

    /// <summary>Rule provenance.</summary>
    public RuleRef Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<ComplianceIndicator> Evaluate(ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = context.WorkDay.WorkedIntervals;
        if (current.IsEmpty)
        {
            return [];
        }

        var currentStart = current[0].Start;
        var previousEnd = context.SurroundingWorkDays
            .Where(day => day.Key.EmployeeId == context.WorkDay.Key.EmployeeId)
            .SelectMany(day => day.WorkedIntervals)
            .Where(interval => interval.End <= currentStart)
            .Select(interval => (DateTimeOffset?)interval.End)
            .Max();
        if (!previousEnd.HasValue ||
            currentStart - previousEnd.Value >= MinimumRest)
        {
            return [];
        }

        return
        [
            new ComplianceIndicator(
                "rest.daily-below-threshold",
                "DailyRest",
                $"The interval between recorded workdays is {currentStart - previousEnd.Value}, below the configured threshold of {MinimumRest}.",
                context.WorkDay.Key,
                new Novolis.Hours.Domain.Work.WorkInterval(
                    previousEnd.Value,
                    currentStart),
                Provenance),
        ];
    }
}
