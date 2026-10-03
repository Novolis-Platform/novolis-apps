namespace Novolis.Hours.Domain;

/// <summary>One side of an immutable, duration-only double-entry posting.</summary>
public sealed record HoursBalancePosting(
    Guid Id,
    Guid SourceId,
    string EmployeeId,
    HoursLedgerAccount Account,
    TimeSpan SignedDuration,
    HoursPostingReason Reason,
    string Narrative);
