namespace Novolis.Hours.Contracts;

/// <summary>One review period as it appears on the workplace inbox.</summary>
public sealed record ReviewInboxItemResponse(
    Guid PeriodId,
    string EmployeeId,
    DateOnly From,
    DateOnly Through,
    string PolicyId,
    string State);
