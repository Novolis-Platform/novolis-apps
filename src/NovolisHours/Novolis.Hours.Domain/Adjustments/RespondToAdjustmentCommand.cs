namespace Novolis.Hours.Domain;

/// <summary>Records employee acceptance or dispute of a proposed duration adjustment.</summary>
public sealed record RespondToAdjustmentCommand(
    Guid AdjustmentId,
    HoursAdjustmentResponse Response,
    string Comment,
    HoursActor Actor);
