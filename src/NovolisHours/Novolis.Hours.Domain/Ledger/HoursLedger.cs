using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Pure double-entry duration projection; it never models pay, leave, or exchange rates.</summary>
public static class HoursLedger
{
    /// <summary>Creates the two balanced postings for a recorded flex movement.</summary>
    public static ImmutableArray<HoursBalancePosting> ForEntry(HoursEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return ForMovement(
            entry.Id,
            entry.EmployeeId,
            entry.FlexDelta,
            HoursPostingReason.RecordedFlexDifference,
            entry.Comment);
    }

    /// <summary>Creates the two balanced postings for an approved adjustment.</summary>
    public static ImmutableArray<HoursBalancePosting> ForAdjustment(HoursAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        return ForMovement(
            adjustment.Id,
            adjustment.EmployeeId,
            adjustment.DurationDelta,
            adjustment.Reason == HoursAdjustmentReason.FlexNormalization
                ? HoursPostingReason.FlexNormalization
                : HoursPostingReason.EmployeeApprovedAdjustment,
            adjustment.Comment);
    }

    /// <summary>Creates no postings for a zero movement and two opposite postings otherwise.</summary>
    public static ImmutableArray<HoursBalancePosting> ForMovement(
        Guid sourceId,
        string employeeId,
        TimeSpan duration,
        HoursPostingReason reason,
        string narrative)
    {
        if (duration == TimeSpan.Zero)
        {
            return [];
        }

        return
        [
            new HoursBalancePosting(
                Guid.CreateVersion7(),
                sourceId,
                employeeId,
                HoursLedgerAccount.EmployeeFlexSaldo,
                duration,
                reason,
                narrative),
            new HoursBalancePosting(
                Guid.CreateVersion7(),
                sourceId,
                employeeId,
                HoursLedgerAccount.OrganisationControl,
                -duration,
                reason,
                narrative),
        ];
    }
}
