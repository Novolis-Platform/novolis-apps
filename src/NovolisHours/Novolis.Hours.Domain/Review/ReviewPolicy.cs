using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Review;

/// <summary>Ordered review policy with transparent deadlines and escalation choice.</summary>
public sealed class ReviewPolicy
{
    /// <summary>Initializes a policy.</summary>
    public ReviewPolicy(
        string id,
        string version,
        IEnumerable<ReviewStage> stages,
        ReviewEscalationTarget disputeTarget = ReviewEscalationTarget.HumanResources,
        bool reopenAfterCorrection = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(stages);
        var materialized = stages.ToImmutableArray();
        if (materialized.IsEmpty)
        {
            throw new ArgumentException("A review policy requires one stage.", nameof(stages));
        }

        if (materialized.Select(stage => stage.Id)
            .Distinct(StringComparer.Ordinal)
            .Count() != materialized.Length)
        {
            throw new ArgumentException(
                "Review stage identifiers must be unique.",
                nameof(stages));
        }

        Id = id;
        Version = version;
        Stages = materialized;
        DisputeTarget = disputeTarget;
        ReopenAfterCorrection = reopenAfterCorrection;
    }

    /// <summary>Stable policy identity.</summary>
    public string Id { get; }

    /// <summary>Policy version captured by a review projection.</summary>
    public string Version { get; }

    /// <summary>Ordered stages.</summary>
    public ImmutableArray<ReviewStage> Stages { get; }

    /// <summary>Where a human dispute is directed.</summary>
    public ReviewEscalationTarget DisputeTarget { get; }

    /// <summary>Whether a later fact makes prior completion outstanding again.</summary>
    public bool ReopenAfterCorrection { get; }
}
