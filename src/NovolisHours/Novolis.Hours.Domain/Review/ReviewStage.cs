namespace Novolis.Hours.Domain.Review;

/// <summary>One ordered review stage and its relative business-day allowance.</summary>
public sealed record ReviewStage
{
    /// <summary>Initializes a stage.</summary>
    public ReviewStage(
        string id,
        ReviewActionKind requiredAction,
        ResponsibilityRole role,
        int? approvalLevel,
        int businessDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (businessDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(businessDays));
        }

        if (approvalLevel is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(approvalLevel));
        }

        Id = id;
        RequiredAction = requiredAction;
        Role = role;
        ApprovalLevel = approvalLevel;
        BusinessDays = businessDays;
    }

    /// <summary>Stable stage identity.</summary>
    public string Id { get; }

    /// <summary>Action satisfying the stage.</summary>
    public ReviewActionKind RequiredAction { get; }

    /// <summary>Actor responsibility expected to perform it.</summary>
    public ResponsibilityRole Role { get; }

    /// <summary>Optional hierarchy level.</summary>
    public int? ApprovalLevel { get; }

    /// <summary>Business days granted after the previous stage deadline.</summary>
    public int BusinessDays { get; }
}
