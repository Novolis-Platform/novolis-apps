namespace Novolis.Hours.Contracts;

/// <summary>Actual-presence registration request.</summary>
public sealed record RegisterWorkRequest(
    string EmployeeId,
    DateOnly Day,
    TimeOnly StartedAt,
    TimeOnly EndedAt,
    TimeOnly? BreakStartedAt,
    TimeOnly? BreakEndedAt,
    IReadOnlyCollection<FinancialCompensationSlice>? FinancialCompensationSlices,
    string Comment,
    bool ManagerAgreementRecorded);
