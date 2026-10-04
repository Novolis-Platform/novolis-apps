using System.Collections.Immutable;
using Novolis.Hours.Domain.Ledger;
using Novolis.Hours.Domain.Work;
using Novolis.Time;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Domain;

/// <summary>Pure replay of the append-only journal into an employee worktime view.</summary>
public static class HoursProjector
{
    /// <summary>Builds the current view without mutating or deleting a journal fact.</summary>
    public static HoursEmployeeView Replay(
        string employeeId,
        IEnumerable<HoursEvent> journal,
        HoursPolicy? policy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentNullException.ThrowIfNull(journal);

        var entries = new List<HoursEntry>();
        var ledgerTransactions = new List<LedgerTransaction>();
        var compatibilityRegistrations = new Dictionary<Guid, WorkRegistration>();
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

                case HoursEventType.WorkRegistrationRecorded:
                    if (policy is not null)
                    {
                        var registration = entry.ReadPayload<WorkRegistration>();
                        compatibilityRegistrations[registration.Id] = registration;
                        entries.Add(ToCompatibilityEntry(registration, policy));
                    }

                    break;

                case HoursEventType.LedgerTransactionRecorded:
                    ledgerTransactions.Add(entry.ReadPayload<LedgerTransaction>());
                    break;

                case HoursEventType.ConfigurationSnapshotRecorded:
                case HoursEventType.ConfigurationPublished:
                case HoursEventType.DimensionAssignmentRecorded:
                case HoursEventType.ReviewPeriodCreated:
                case HoursEventType.ReviewActionRecorded:
                    // These v2 facts belong to the acceptance projections. The
                    // legacy employee summary intentionally ignores them.
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
        var ledgerSourceRegistrationIds = ledgerTransactions
            .Select(item => item.SourceRegistrationId)
            .ToHashSet();
        var postings = orderedEntries
            .Where(entry =>
                !compatibilityRegistrations.ContainsKey(entry.Id) &&
                !ledgerSourceRegistrationIds.Contains(entry.Id))
            .SelectMany(entry => HoursLedger.ForEntry(entry))
            .Concat(orderedAdjustments
                .Where(item => item.State is HoursAdjustmentState.Accepted or HoursAdjustmentState.ResolvedAccepted)
                .SelectMany(adjustment => HoursLedger.ForAdjustment(adjustment)))
            .Concat(compatibilityRegistrations.Values
                .OrderBy(registration => registration.RecordedAt)
                .ThenBy(registration => registration.Id)
                .SelectMany(registration => HoursLedger.ForMovement(
                    registration.Id,
                    registration.WorkDay.EmployeeId,
                    CompatibilityFlexDelta(registration, compatibilityRegistrations, policy!),
                    HoursPostingReason.RecordedFlexDifference,
                    registration.Note ?? "Work registration compatibility projection.")))
            .Concat(ledgerTransactions
                .Where(transaction => !compatibilityRegistrations.ContainsKey(transaction.SourceRegistrationId))
                .SelectMany(ToCompatibilityPostings))
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

    private static IEnumerable<HoursBalancePosting> ToCompatibilityPostings(
        LedgerTransaction transaction) =>
        transaction.Postings.Select(posting => new HoursBalancePosting(
            transaction.Id,
            transaction.SourceRegistrationId,
            transaction.WorkDay.EmployeeId,
            posting.Account == DurationAccount.EmployeeFlex
                ? HoursLedgerAccount.EmployeeFlexSaldo
                : HoursLedgerAccount.OrganisationControl,
            posting.SignedDuration,
            HoursPostingReason.RecordedFlexDifference,
            transaction.Reason));

    private static TimeSpan CompatibilityFlexDelta(
        WorkRegistration registration,
        IReadOnlyDictionary<Guid, WorkRegistration> registrations,
        HoursPolicy policy)
    {
        var current = ToCompatibilityEntry(registration, policy).FlexDelta;
        if (registration.CorrectsRegistrationId is { } correctedId &&
            registrations.TryGetValue(correctedId, out var corrected))
        {
            return current - ToCompatibilityEntry(corrected, policy).FlexDelta;
        }

        return current;
    }

    private static HoursEntry ToCompatibilityEntry(
        WorkRegistration registration,
        HoursPolicy policy)
    {
        var expected = WorktimeCalculator.CreateExpectedSnapshot(
            registration.WorkDay.NominalDate,
            policy.EmploymentSettings);
        var intervals = registration.Intent == WorkRecordIntent.WorkedAsScheduled
            ? []
            : registration.Intervals
                .OrderBy(interval => interval.Start)
                .ThenBy(interval => interval.End)
                .ToArray();
        var actualDuration = registration.Intent == WorkRecordIntent.WorkedAsScheduled
            ? expected.ExpectedDuration
            : intervals.Aggregate(
                TimeSpan.Zero,
                (total, interval) => total + interval.Duration);
        var presence = CreateCompatibilityPresence(intervals, expected);
        var takenBreak = CreateCompatibilityBreak(intervals, expected, presence);
        var comment = string.IsNullOrWhiteSpace(registration.Note)
            ? "Work registration compatibility projection."
            : registration.Note;
        var legalNotices = ImmutableArray<HoursLegalNotice>.Empty;

        if (presence is { } actualPresence)
        {
            var actual = new ActualWorkRecord(
                registration.Id,
                registration.WorkDay.NominalDate,
                actualPresence,
                takenBreak,
                [],
                comment,
                hasManagerAgreement: false);
            var balance = WorktimeCalculator.Calculate(actual, expected);
            actualDuration = balance.Actual;
            legalNotices = WorktimeLegalEvaluator.Evaluate(
                    actual,
                    balance,
                    policy.EmploymentSettings.Profile,
                    policy.LegalPreset)
                .Select(firing => new HoursLegalNotice(
                    firing.RuleId,
                    firing.Message,
                    firing.Citation,
                    firing.PresetId,
                    firing.PresetVersion))
                .ToImmutableArray();
        }

        var displayInterval = presence ?? expected.ExpectedInterval;
        var startedAt = displayInterval?.Start ?? new TimeOnly(0, 0);
        var endedAt = displayInterval?.End ?? new TimeOnly(0, 1);
        return new HoursEntry(
            registration.Id,
            registration.WorkDay.EmployeeId,
            registration.WorkDay.NominalDate,
            startedAt,
            endedAt,
            takenBreak?.Start,
            takenBreak?.End,
            [],
            expected.ExpectedDuration,
            actualDuration,
            actualDuration - expected.ExpectedDuration,
            TimeSpan.Zero,
            comment,
            false,
            new HoursWorktimeSnapshot(
                expected.ProfileId,
                expected.TemplateId,
                expected.CalendarId,
                expected.CalendarSource.Source,
                policy.LegalPreset.Id,
                policy.LegalPreset.Version),
            legalNotices,
            registration.RecordedBy.ToHoursActor(),
            registration.RecordedAt);
    }

    private static ClockInterval? CreateCompatibilityPresence(
        IReadOnlyList<WorkInterval> intervals,
        ExpectedDaySnapshot expected)
    {
        if (intervals.Count == 0)
        {
            return expected.ExpectedInterval;
        }

        var start = ToLocalTime(intervals[0].Start);
        var end = ToLocalTime(intervals[^1].End);
        return end > start ? new ClockInterval(start, end) : null;
    }

    private static ClockInterval? CreateCompatibilityBreak(
        IReadOnlyList<WorkInterval> intervals,
        ExpectedDaySnapshot expected,
        ClockInterval? presence)
    {
        if (presence is not { } actualPresence)
        {
            return null;
        }

        if (intervals.Count == 2)
        {
            var start = ToLocalTime(intervals[0].End);
            var end = ToLocalTime(intervals[1].Start);
            if (end > start)
            {
                var observedBreak = new ClockInterval(start, end);
                if (actualPresence.Contains(observedBreak))
                {
                    return observedBreak;
                }
            }
        }

        if (expected.ExpectedInterval is { } expectedInterval &&
            expectedInterval.Contains(expected.Lunch) &&
            actualPresence.Contains(expected.Lunch))
        {
            return expected.Lunch;
        }

        return null;
    }

    private static TimeOnly ToLocalTime(DateTimeOffset value) =>
        TimeOnly.FromDateTime(value.DateTime);

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
