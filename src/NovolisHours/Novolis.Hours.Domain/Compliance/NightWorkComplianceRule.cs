using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Reports intervals that overlap a configured local-clock night window.</summary>
public sealed class NightWorkComplianceRule : IComplianceRule
{
    /// <summary>Initializes a night-window rule.</summary>
    public NightWorkComplianceRule(
        TimeOnly nightStart,
        TimeOnly nightEnd,
        RuleRef provenance)
    {
        if (nightStart == nightEnd)
        {
            throw new ArgumentException("A night window must not be empty.", nameof(nightEnd));
        }

        ArgumentNullException.ThrowIfNull(provenance);
        NightStart = nightStart;
        NightEnd = nightEnd;
        Provenance = provenance;
    }

    /// <summary>Window start.</summary>
    public TimeOnly NightStart { get; }

    /// <summary>Window end.</summary>
    public TimeOnly NightEnd { get; }

    /// <summary>Rule provenance.</summary>
    public RuleRef Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<ComplianceIndicator> Evaluate(ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var affected = context.WorkDay.WorkedIntervals
            .FirstOrDefault(interval =>
                IsNight(interval.Start.TimeOfDay) ||
                IsNight((interval.End - TimeSpan.FromTicks(1)).TimeOfDay));
        if (affected is null)
        {
            return [];
        }

        return
        [
            new ComplianceIndicator(
                "work.night",
                "NightWork",
                "Recorded work overlaps the configured night window.",
                context.WorkDay.Key,
                affected,
                Provenance),
        ];
    }

    private bool IsNight(TimeSpan time)
    {
        var start = NightStart.ToTimeSpan();
        var end = NightEnd.ToTimeSpan();
        return start < end
            ? time >= start && time < end
            : time >= start || time < end;
    }
}
