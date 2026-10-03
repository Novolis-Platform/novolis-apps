using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Immutable monthly review clock with non-blocking deadline anomalies.</summary>
public sealed record HoursApprovalPeriod(
    Guid Id,
    string EmployeeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateOnly EmployeeSubmitDueOn,
    DateOnly ManagerReviewDueOn,
    DateOnly HrResolutionDueOn,
    HoursApprovalState State,
    ImmutableArray<HoursAnomaly> Anomalies,
    HoursActor OpenedBy,
    DateTimeOffset OpenedAtUtc,
    HoursActor? LastActionBy = null,
    DateTimeOffset? LastActionAtUtc = null,
    string? LastComment = null);
