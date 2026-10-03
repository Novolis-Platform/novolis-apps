using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Append-only fact describing when an employee worked relative to expected time.</summary>
public sealed record HoursEntry(
    Guid Id,
    string EmployeeId,
    DateOnly Day,
    TimeOnly StartedAt,
    TimeOnly EndedAt,
    TimeOnly? BreakStartedAt,
    TimeOnly? BreakEndedAt,
    ImmutableArray<FinancialCompensationSlice> FinancialCompensationSlices,
    TimeSpan ExpectedDuration,
    TimeSpan ActualDuration,
    TimeSpan FlexDelta,
    TimeSpan FinancialCompensationDuration,
    string Comment,
    bool ManagerAgreementRecorded,
    HoursWorktimeSnapshot WorktimeSnapshot,
    ImmutableArray<HoursLegalNotice> LegalNotices,
    HoursActor RegisteredBy,
    DateTimeOffset RegisteredAtUtc);
