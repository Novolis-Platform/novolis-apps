using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Review;

/// <summary>Rebuildable review projection over immutable period actions.</summary>
public sealed record ReviewProjection
{
    /// <summary>Initializes a review projection.</summary>
    public ReviewProjection(
        ReviewPeriod period,
        ReviewPolicy policy,
        IEnumerable<ReviewAction> actions,
        IEnumerable<ReviewStageResult> stages,
        IEnumerable<ReviewAnomaly> anomalies,
        IEnumerable<ReviewEscalation> escalations,
        ReviewState state,
        bool changedAfterApproval)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(anomalies);
        ArgumentNullException.ThrowIfNull(escalations);
        Period = period;
        Policy = policy;
        Actions = actions.ToImmutableArray();
        Stages = stages.ToImmutableArray();
        Anomalies = anomalies.ToImmutableArray();
        Escalations = escalations.ToImmutableArray();
        State = state;
        ChangedAfterApproval = changedAfterApproval;
    }

    /// <summary>Period projected.</summary>
    public ReviewPeriod Period { get; }

    /// <summary>Policy used for interpretation.</summary>
    public ReviewPolicy Policy { get; }

    /// <summary>Complete period action history.</summary>
    public ImmutableArray<ReviewAction> Actions { get; }

    /// <summary>All configured stage results.</summary>
    public ImmutableArray<ReviewStageResult> Stages { get; }

    /// <summary>Transparent deadline anomalies.</summary>
    public ImmutableArray<ReviewAnomaly> Anomalies { get; }

    /// <summary>Unresolved dispute destinations.</summary>
    public ImmutableArray<ReviewEscalation> Escalations { get; }

    /// <summary>Current projected review state.</summary>
    public ReviewState State { get; }

    /// <summary>Whether a later fact occurred after the latest approval.</summary>
    public bool ChangedAfterApproval { get; }

    /// <summary>Stages still requiring action, even when overdue.</summary>
    public ImmutableArray<ReviewStageResult> OutstandingStages =>
        Stages.Where(stage => !stage.IsComplete).ToImmutableArray();
}
