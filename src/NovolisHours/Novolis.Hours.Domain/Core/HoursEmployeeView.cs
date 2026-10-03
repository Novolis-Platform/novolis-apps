using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Immutable query projection for one employee's worktime journal.</summary>
public sealed record HoursEmployeeView(
    string EmployeeId,
    ImmutableArray<HoursEntry> Entries,
    ImmutableArray<HoursAdjustment> Adjustments,
    ImmutableArray<HoursApprovalPeriod> ApprovalPeriods,
    ImmutableArray<HoursBalancePosting> LedgerPostings,
    ImmutableArray<HoursAnomaly> Anomalies,
    TimeSpan FlexSaldo)
{
    /// <summary>Creates an empty employee projection.</summary>
    public static HoursEmployeeView Empty(string employeeId) =>
        new(employeeId, [], [], [], [], [], TimeSpan.Zero);
}
