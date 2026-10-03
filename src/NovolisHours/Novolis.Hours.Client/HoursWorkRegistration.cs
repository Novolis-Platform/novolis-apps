namespace Novolis.Hours.Client;

/// <summary>Actual presence to submit through the protected Hours API.</summary>
public sealed record HoursWorkRegistration(
    string EmployeeId,
    DateOnly Day,
    TimeOnly StartedAt,
    TimeOnly EndedAt,
    TimeOnly? BreakStartedAt,
    TimeOnly? BreakEndedAt,
    IReadOnlyCollection<HoursFinancialCompensationSlice> FinancialCompensationSlices,
    string Comment,
    bool ManagerAgreementRecorded);
