using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Pure replay of the append-only journal into an employee worktime view.</summary>
public static class HoursProjector
{
    /// <summary>Builds the current view without mutating or deleting a journal fact.</summary>
    public static HoursEmployeeView Replay(string employeeId, IEnumerable<HoursEvent> journal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentNullException.ThrowIfNull(journal);

        var entries = new List<HoursEntry>();
        var adjustments = new Dictionary<Guid, HoursAdjustment>();
        var periods = new Dictionary<Guid, HoursApprovalPeriod>();

        foreach (var entry in journal
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id))
        {
            switch (entry.Type)
            {
                case HoursEventType.WorkRegistered:
                    entries.Add(entry.ReadPayload<HoursEntry>());
                    break;

                case HoursEventType.AdjustmentProposed:
                case HoursEventType.AdjustmentUpdated:
                    var adjustment = entry.ReadPayload<HoursAdjustment>();
                    adjustments[adjustment.Id] = adjustment;
                    break;

                case HoursEventType.ApprovalPeriodOpened:
                case HoursEventType.ApprovalPeriodUpdated:
                    var period = entry.ReadPayload<HoursApprovalPeriod>();
                    periods[period.Id] = period;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown Hours event type '{entry.Type}'.");
            }
        }

        var orderedEntries = entries
            .OrderBy(item => item.Day)
            .ThenBy(item => item.RegisteredAtUtc)
            .ToImmutableArray();
        var orderedAdjustments = adjustments.Values
            .OrderBy(item => item.EffectiveDay)
            .ThenBy(item => item.ProposedAtUtc)
            .ToImmutableArray();
        var orderedPeriods = periods.Values
            .OrderBy(item => item.StartsOn)
            .ToImmutableArray();
        var postings = orderedEntries
            .SelectMany(entry => HoursLedger.ForEntry(entry))
            .Concat(orderedAdjustments
                .Where(item => item.State is HoursAdjustmentState.Accepted or HoursAdjustmentState.ResolvedAccepted)
                .SelectMany(adjustment => HoursLedger.ForAdjustment(adjustment)))
            .ToImmutableArray();
        var anomalies = orderedEntries
            .SelectMany(item => item.LegalNotices.Select(notice => new HoursAnomaly(
                $"legal.{notice.RuleId}",
                notice.Message,
                item.RegisteredAtUtc,
                item.Id)))
            .Concat(orderedAdjustments
                .Where(item => item.State is HoursAdjustmentState.Escalated
                    or HoursAdjustmentState.ResolvedAccepted
                    or HoursAdjustmentState.ResolvedRejected)
                .Select(item => new HoursAnomaly(
                    "adjustment.disputed",
                    AdjustmentDisputeMessage(item),
                    item.ResolvedAtUtc ?? item.RespondedAtUtc ?? item.ProposedAtUtc,
                    item.Id)))
            .Concat(orderedPeriods.SelectMany(item => item.Anomalies))
            .OrderBy(item => item.ObservedAtUtc)
            .ToImmutableArray();
        var flexSaldo = postings
            .Where(item => item.Account == HoursLedgerAccount.EmployeeFlexSaldo)
            .Aggregate(TimeSpan.Zero, (total, item) => total + item.SignedDuration);

        return new HoursEmployeeView(
            employeeId,
            orderedEntries,
            orderedAdjustments,
            orderedPeriods,
            postings,
            anomalies,
            flexSaldo);
    }

    private static string AdjustmentDisputeMessage(HoursAdjustment adjustment) =>
        adjustment.State switch
        {
            HoursAdjustmentState.Escalated =>
                $"Adjustment '{adjustment.Id}' is escalated to {adjustment.EscalatedTo?.Role}.",
            HoursAdjustmentState.ResolvedAccepted =>
                $"Adjustment '{adjustment.Id}' was disputed and resolved as committed by {adjustment.ResolvedBy?.Role}.",
            HoursAdjustmentState.ResolvedRejected =>
                $"Adjustment '{adjustment.Id}' was disputed and resolved as rejected by {adjustment.ResolvedBy?.Role}.",
            _ => throw new ArgumentOutOfRangeException(nameof(adjustment), adjustment.State, "Adjustment is not a dispute state."),
        };
}
