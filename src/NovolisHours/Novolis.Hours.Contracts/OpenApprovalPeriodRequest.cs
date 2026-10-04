using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Approval-period opening request.</summary>
public sealed record OpenApprovalPeriodRequest(string EmployeeId, DateOnly StartsOn, DateOnly EndsOn);
