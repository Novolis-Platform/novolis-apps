namespace Novolis.Hours.Contracts;

/// <summary>Employee adjustment response request.</summary>
public sealed record RespondToAdjustmentRequest(HoursAdjustmentResponse Response, string Comment);
