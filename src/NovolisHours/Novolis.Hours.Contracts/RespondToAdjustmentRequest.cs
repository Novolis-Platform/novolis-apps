using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Employee adjustment response request.</summary>
public sealed record RespondToAdjustmentRequest(HoursAdjustmentResponse Response, string Comment);
