namespace Novolis.Hours.Domain;

/// <summary>Proposes a salary-neutral duration adjustment for employee acknowledgement.</summary>
public sealed record ProposeAdjustmentCommand(
    string EmployeeId,
    DateOnly EffectiveDay,
    TimeSpan DurationDelta,
    HoursAdjustmentReason Reason,
    string Comment,
    HoursActor Actor);
