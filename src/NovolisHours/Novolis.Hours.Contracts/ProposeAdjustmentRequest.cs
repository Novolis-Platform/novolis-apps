using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Proposed immutable adjustment request.</summary>
public sealed record ProposeAdjustmentRequest(
    string EmployeeId,
    DateOnly EffectiveDay,
    TimeSpan DurationDelta,
    HoursAdjustmentReason Reason,
    string Comment);
