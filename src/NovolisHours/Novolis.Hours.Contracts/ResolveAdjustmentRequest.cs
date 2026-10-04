namespace Novolis.Hours.Contracts;

/// <summary>Escalated adjustment resolution request.</summary>
public sealed record ResolveAdjustmentRequest(HoursAdjustmentResolution Resolution, string Comment);
