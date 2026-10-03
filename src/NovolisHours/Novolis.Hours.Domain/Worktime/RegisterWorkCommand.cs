namespace Novolis.Hours.Domain;

/// <summary>Registers actual presence for one date; legal notices annotate it but never reject it.</summary>
public sealed record RegisterWorkCommand(
    string EmployeeId,
    DateOnly Day,
    TimeOnly StartedAt,
    TimeOnly EndedAt,
    TimeOnly? BreakStartedAt,
    TimeOnly? BreakEndedAt,
    IReadOnlyCollection<FinancialCompensationSlice> FinancialCompensationSlices,
    string Comment,
    bool ManagerAgreementRecorded,
    HoursActor Actor);
