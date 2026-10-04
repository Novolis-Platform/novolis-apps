namespace Novolis.Hours.Domain.Review;

/// <summary>Projected review state; never a gate on recording work.</summary>
public enum ReviewState
{
    Registered,
    Submitted,
    Approved,
    Disputed,
}
