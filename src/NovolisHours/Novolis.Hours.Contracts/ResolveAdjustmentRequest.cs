using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Escalated adjustment resolution request.</summary>
public sealed record ResolveAdjustmentRequest(HoursAdjustmentResolution Resolution, string Comment);
