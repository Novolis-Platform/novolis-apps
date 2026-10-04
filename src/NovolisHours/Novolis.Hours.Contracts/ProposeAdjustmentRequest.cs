namespace Novolis.Hours.Contracts;

/// <summary>Proposed immutable adjustment request.</summary>
public sealed record ProposeAdjustmentRequest(
    string EmployeeId,
    DateOnly EffectiveDay,
    TimeSpan DurationDelta,
    HoursAdjustmentReason Reason,
    string Comment);
