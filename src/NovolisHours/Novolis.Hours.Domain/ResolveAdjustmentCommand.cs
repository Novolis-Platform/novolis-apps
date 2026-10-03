namespace Novolis.Hours.Domain;

/// <summary>Records HR or Higher's auditable outcome for an escalated employee dispute.</summary>
public sealed record ResolveAdjustmentCommand(
    Guid AdjustmentId,
    HoursAdjustmentResolution Resolution,
    string Comment,
    HoursActor Actor);
