namespace Novolis.Hours.Domain;

/// <summary>Opens a monthly approval clock without restricting ordinary worktime registration.</summary>
public sealed record OpenApprovalPeriodCommand(
    string EmployeeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    HoursActor Actor);
