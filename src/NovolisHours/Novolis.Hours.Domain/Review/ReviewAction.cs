using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Review;

/// <summary>Immutable human or system action in a review history.</summary>
public sealed record ReviewAction
{
    /// <summary>Initializes an action.</summary>
    public ReviewAction(
        Guid id,
        Guid periodId,
        ReviewActionKind kind,
        ActorRef actor,
        int? approvalLevel,
        string? comment,
        DateTimeOffset recordedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A review action requires an identity.", nameof(id));
        }

        if (periodId == Guid.Empty)
        {
            throw new ArgumentException(
                "A review action requires a period identity.",
                nameof(periodId));
        }

        ArgumentNullException.ThrowIfNull(actor);
        if (kind is (ReviewActionKind.Dispute or ReviewActionKind.Resolve) &&
            string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException(
                "Dispute and resolution actions require a comment.",
                nameof(comment));
        }

        Id = id;
        PeriodId = periodId;
        Kind = kind;
        Actor = actor;
        ApprovalLevel = approvalLevel;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        RecordedAt = recordedAt;
    }

    /// <summary>Action identity.</summary>
    public Guid Id { get; }

    /// <summary>Period receiving the action.</summary>
    public Guid PeriodId { get; }

    /// <summary>Action kind.</summary>
    public ReviewActionKind Kind { get; }

    /// <summary>Actor captured at append time.</summary>
    public ActorRef Actor { get; }

    /// <summary>Manager/higher approval level when applicable.</summary>
    public int? ApprovalLevel { get; }

    /// <summary>Optional explanation or context.</summary>
    public string? Comment { get; }

    /// <summary>Action timestamp.</summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>Creates an action with a new identity.</summary>
    public static ReviewAction Create(
        Guid periodId,
        ReviewActionKind kind,
        ActorRef actor,
        int? approvalLevel,
        string? comment,
        DateTimeOffset recordedAt) =>
        new(
            Guid.CreateVersion7(),
            periodId,
            kind,
            actor,
            approvalLevel,
            comment,
            recordedAt);
}
