namespace Novolis.Hours.Domain.Review;

/// <summary>Scoped responsibility used by review and report authorization.</summary>
public sealed record Responsibility
{
    /// <summary>Initializes a responsibility.</summary>
    public Responsibility(
        string userId,
        ResponsibilityRole role,
        string scopeId,
        int? approvalLevel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        if (approvalLevel is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(approvalLevel));
        }

        UserId = userId;
        Role = role;
        ScopeId = scopeId;
        ApprovalLevel = approvalLevel;
    }

    /// <summary>Responsible user identity.</summary>
    public string UserId { get; }

    /// <summary>Responsibility category.</summary>
    public ResponsibilityRole Role { get; }

    /// <summary>Team, division, or other scope identity.</summary>
    public string ScopeId { get; }

    /// <summary>Optional approval level in a manager hierarchy.</summary>
    public int? ApprovalLevel { get; }
}
