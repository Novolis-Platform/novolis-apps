namespace Novolis.Hours.Domain;

/// <summary>An auditable proposed change to a flex saldo that requires employee acknowledgement.</summary>
public sealed record HoursAdjustment(
    Guid Id,
    string EmployeeId,
    DateOnly EffectiveDay,
    TimeSpan DurationDelta,
    HoursAdjustmentReason Reason,
    string Comment,
    HoursActor ProposedBy,
    DateTimeOffset ProposedAtUtc,
    HoursAdjustmentState State,
    string? EmployeeResponseComment,
    HoursActor? RespondedBy,
    DateTimeOffset? RespondedAtUtc,
    HoursActor? EscalatedTo,
    HoursActor? ResolvedBy = null,
    DateTimeOffset? ResolvedAtUtc = null,
    string? ResolutionComment = null);
