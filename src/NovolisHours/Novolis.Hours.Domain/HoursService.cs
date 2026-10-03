using System.Collections.Immutable;
using Novolis.Time;
using Novolis.Time.Calendar;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Domain;

/// <summary>Application service that appends worktime facts and derives transparent, non-blocking notices.</summary>
public sealed class HoursService
{
    private readonly IHoursJournal journal;
    private readonly IHoursPolicyProvider policyProvider;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes the service with a reusable immutable policy baseline.</summary>
    public HoursService(IHoursJournal journal, HoursPolicy policy, TimeProvider? timeProvider = null)
        : this(journal, new FixedHoursPolicyProvider(policy), timeProvider)
    {
    }

    /// <summary>Initializes the service with an employee-aware policy provider.</summary>
    public HoursService(IHoursJournal journal, IHoursPolicyProvider policyProvider, TimeProvider? timeProvider = null)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Registers actual presence and stores rule firings as notices instead of rejecting the record.</summary>
    public async ValueTask<HoursEntry> RegisterWorkAsync(
        RegisterWorkCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureEmployeeAction(command.EmployeeId, command.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Comment);
        var policy = policyProvider.GetPolicy(command.EmployeeId);

        var presence = new ClockInterval(command.StartedAt, command.EndedAt);
        var takenBreak = CreateBreak(command);
        var compensation = command.FinancialCompensationSlices
            .Select(item => new FinancialCompensationMark(
                new ClockInterval(item.Start, item.End),
                item.Reason))
            .ToImmutableArray();
        var managerAgreementRecorded = command.ManagerAgreementRecorded &&
            command.Actor.Role is HoursActorRole.Manager
                or HoursActorRole.HumanResources
                or HoursActorRole.Higher
                or HoursActorRole.Administrator;
        var actual = new ActualWorkRecord(
            Guid.CreateVersion7(),
            command.Day,
            presence,
            takenBreak,
            compensation,
            command.Comment,
            managerAgreementRecorded);
        var expected = WorktimeCalculator.CreateExpectedSnapshot(command.Day, policy.EmploymentSettings);
        var balance = WorktimeCalculator.Calculate(actual, expected);
        var notices = WorktimeLegalEvaluator.Evaluate(
                actual,
                balance,
                policy.EmploymentSettings.Profile,
                policy.LegalPreset)
            .Select(item => new HoursLegalNotice(
                item.RuleId,
                item.Message,
                item.Citation,
                item.PresetId,
                item.PresetVersion))
            .ToImmutableArray();
        var now = timeProvider.GetUtcNow();
        var entry = new HoursEntry(
            actual.Id,
            command.EmployeeId,
            command.Day,
            command.StartedAt,
            command.EndedAt,
            command.BreakStartedAt,
            command.BreakEndedAt,
            command.FinancialCompensationSlices.ToImmutableArray(),
            balance.Expected,
            balance.Actual,
            balance.FlexDelta,
            balance.FinanciallyCompensated,
            command.Comment,
            managerAgreementRecorded,
            new HoursWorktimeSnapshot(
                expected.ProfileId,
                expected.TemplateId,
                expected.CalendarId,
                expected.CalendarSource.Source,
                policy.LegalPreset.Id,
                policy.LegalPreset.Version),
            notices,
            command.Actor,
            now);

        await journal.AppendAsync(
            HoursEvent.Create(command.EmployeeId, HoursEventType.WorkRegistered, entry, command.Actor, now),
            cancellationToken);
        return entry;
    }

    /// <summary>Creates a proposed adjustment that has no saldo impact until employee acceptance.</summary>
    public async ValueTask<HoursAdjustment> ProposeAdjustmentAsync(
        ProposeAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureReviewer(command.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Comment);
        if (command.DurationDelta == TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "An adjustment must have a non-zero duration.");
        }

        var now = timeProvider.GetUtcNow();
        var adjustment = new HoursAdjustment(
            Guid.CreateVersion7(),
            command.EmployeeId,
            command.EffectiveDay,
            command.DurationDelta,
            command.Reason,
            command.Comment,
            command.Actor,
            now,
            HoursAdjustmentState.AwaitingEmployee,
            null,
            null,
            null,
            null);
        await journal.AppendAsync(
            HoursEvent.Create(command.EmployeeId, HoursEventType.AdjustmentProposed, adjustment, command.Actor, now),
            cancellationToken);
        return adjustment;
    }

    /// <summary>Proposes a carry-cap normalization as a non-payment adjustment awaiting employee acceptance.</summary>
    public async ValueTask<HoursAdjustment?> ProposeFlexNormalizationAsync(
        string employeeId,
        DateOnly assessedThrough,
        HoursActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        EnsureReviewerOrSystem(actor);
        var policy = policyProvider.GetPolicy(employeeId);
        var cutoff = policy.SettlementPolicy.ClosingDate(assessedThrough);
        var view = await GetEmployeeViewAsync(employeeId, cancellationToken);
        var existing = view.Adjustments.SingleOrDefault(item =>
            item.Reason == HoursAdjustmentReason.FlexNormalization &&
            item.EffectiveDay == cutoff);
        if (existing is not null)
        {
            return existing;
        }

        var normalization = FlexNormalization.Create(
            policy.LegalPreset.FlexCarryPolicy,
            view.FlexSaldo);
        if (normalization.NormalizedUnusedFlex == TimeSpan.Zero)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var adjustment = new HoursAdjustment(
            Guid.CreateVersion7(),
            employeeId,
            cutoff,
            -normalization.NormalizedUnusedFlex,
            HoursAdjustmentReason.FlexNormalization,
            $"Proposed {policy.SettlementPolicy.Cadence.ToString().ToLowerInvariant()} flex normalization to the configured carry cap. This changes only the flex saldo and does not imply payment.",
            actor,
            now,
            HoursAdjustmentState.AwaitingEmployee,
            null,
            null,
            null,
            null);
        await journal.AppendAsync(
            HoursEvent.Create(employeeId, HoursEventType.AdjustmentProposed, adjustment, actor, now),
            cancellationToken);
        return adjustment;
    }

    /// <summary>Records employee consent or routes a disagreement to HR without deleting any facts.</summary>
    public async ValueTask<HoursAdjustment> RespondToAdjustmentAsync(
        RespondToAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Comment);
        var existing = await FindAdjustmentAsync(command.AdjustmentId, cancellationToken);
        EnsureEmployeeAction(existing.EmployeeId, command.Actor);
        if (existing.State != HoursAdjustmentState.AwaitingEmployee)
        {
            throw new InvalidOperationException("Only an adjustment awaiting the employee can receive this response.");
        }

        var now = timeProvider.GetUtcNow();
        var updated = command.Response == HoursAdjustmentResponse.Accept
            ? existing with
            {
                State = HoursAdjustmentState.Accepted,
                EmployeeResponseComment = command.Comment,
                RespondedBy = command.Actor,
                RespondedAtUtc = now,
            }
            : existing with
            {
                State = HoursAdjustmentState.Escalated,
                EmployeeResponseComment = command.Comment,
                RespondedBy = command.Actor,
                RespondedAtUtc = now,
                EscalatedTo = HoursActor.HumanResources(),
            };
        await journal.AppendAsync(
            HoursEvent.Create(existing.EmployeeId, HoursEventType.AdjustmentUpdated, updated, command.Actor, now),
            cancellationToken);
        return updated;
    }

    /// <summary>Records HR or Higher's final outcome for an escalated adjustment without erasing the employee dispute.</summary>
    public async ValueTask<HoursAdjustment> ResolveAdjustmentAsync(
        ResolveAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Comment);
        EnsureEscalationReviewer(command.Actor);
        var existing = await FindAdjustmentAsync(command.AdjustmentId, cancellationToken);
        if (existing.State != HoursAdjustmentState.Escalated)
        {
            throw new InvalidOperationException("Only an escalated adjustment can be resolved by HR or Higher.");
        }

        var now = timeProvider.GetUtcNow();
        var updated = existing with
        {
            State = command.Resolution == HoursAdjustmentResolution.Accept
                ? HoursAdjustmentState.ResolvedAccepted
                : HoursAdjustmentState.ResolvedRejected,
            ResolvedBy = command.Actor,
            ResolvedAtUtc = now,
            ResolutionComment = command.Comment,
        };
        await journal.AppendAsync(
            HoursEvent.Create(existing.EmployeeId, HoursEventType.AdjustmentUpdated, updated, command.Actor, now),
            cancellationToken);
        return updated;
    }

    /// <summary>Opens an approval period with business-day deadlines; it cannot lock later registrations.</summary>
    public async ValueTask<HoursApprovalPeriod> OpenApprovalPeriodAsync(
        OpenApprovalPeriodCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureReviewerOrSystem(command.Actor);
        if (command.EndsOn < command.StartsOn)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "The period end must be on or after its start.");
        }

        var policy = policyProvider.GetPolicy(command.EmployeeId);
        var schedule = policy.LegalPreset.ApprovalSchedule;
        var employeeDue = BusinessDayCalculator.AddBusinessDays(
            command.EndsOn,
            schedule.EmployeeSubmitBusinessDays,
            policy.Calendar);
        var managerDue = BusinessDayCalculator.AddBusinessDays(
            employeeDue,
            schedule.ManagerReviewBusinessDays,
            policy.Calendar);
        var hrDue = BusinessDayCalculator.AddBusinessDays(
            managerDue,
            schedule.HrResolutionBusinessDays,
            policy.Calendar);
        var now = timeProvider.GetUtcNow();
        var period = new HoursApprovalPeriod(
            Guid.CreateVersion7(),
            command.EmployeeId,
            command.StartsOn,
            command.EndsOn,
            employeeDue,
            managerDue,
            hrDue,
            HoursApprovalState.Registered,
            [],
            command.Actor,
            now);
        await journal.AppendAsync(
            HoursEvent.Create(command.EmployeeId, HoursEventType.ApprovalPeriodOpened, period, command.Actor, now),
            cancellationToken);
        return period;
    }

    /// <summary>Records missed workflow clocks as anomalies without changing the ability to enter worktime.</summary>
    public async ValueTask<HoursApprovalPeriod> CheckApprovalDeadlinesAsync(
        Guid periodId,
        DateOnly asOf,
        HoursActor actor,
        CancellationToken cancellationToken = default)
    {
        EnsureReviewerOrSystem(actor);
        var existing = await FindApprovalPeriodAsync(periodId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var anomalies = existing.Anomalies.ToBuilder();
        AddOverdueAnomaly(
            anomalies,
            existing.State == HoursApprovalState.Registered && asOf > existing.EmployeeSubmitDueOn,
            "approval.employee-submit-overdue",
            "The employee submission deadline passed; registration remains available.",
            now,
            existing.Id);
        AddOverdueAnomaly(
            anomalies,
            existing.State == HoursApprovalState.EmployeeSubmitted && asOf > existing.ManagerReviewDueOn,
            "approval.manager-review-overdue",
            "The manager review deadline passed; registration remains available.",
            now,
            existing.Id);
        AddOverdueAnomaly(
            anomalies,
            existing.State == HoursApprovalState.Escalated && asOf > existing.HrResolutionDueOn,
            "approval.hr-resolution-overdue",
            "The HR or Higher resolution deadline passed; registration remains available.",
            now,
            existing.Id);
        if (anomalies.Count == existing.Anomalies.Length)
        {
            return existing;
        }

        var updated = existing with
        {
            Anomalies = anomalies.ToImmutable(),
            LastActionBy = actor,
            LastActionAtUtc = now,
            LastComment = "Deadline assessment",
        };
        await journal.AppendAsync(
            HoursEvent.Create(existing.EmployeeId, HoursEventType.ApprovalPeriodUpdated, updated, actor, now),
            cancellationToken);
        return updated;
    }

    /// <summary>Submits an approval period for review without making its contents immutable.</summary>
    public ValueTask<HoursApprovalPeriod> SubmitApprovalPeriodAsync(
        Guid periodId,
        string comment,
        HoursActor actor,
        CancellationToken cancellationToken = default) =>
        UpdateApprovalPeriodAsync(
            periodId,
            HoursApprovalState.EmployeeSubmitted,
            comment,
            actor,
            requireEmployee: true,
            cancellationToken);

    /// <summary>Records manager review without preventing subsequent corrective registrations.</summary>
    public ValueTask<HoursApprovalPeriod> ReviewApprovalPeriodAsync(
        Guid periodId,
        string comment,
        HoursActor actor,
        CancellationToken cancellationToken = default) =>
        UpdateApprovalPeriodAsync(
            periodId,
            HoursApprovalState.ManagerApproved,
            comment,
            actor,
            requireEmployee: false,
            cancellationToken);

    /// <summary>Escalates an approval period to HR or Higher while all records remain visible and editable by addition.</summary>
    public ValueTask<HoursApprovalPeriod> EscalateApprovalPeriodAsync(
        Guid periodId,
        string comment,
        HoursActor actor,
        CancellationToken cancellationToken = default) =>
        UpdateApprovalPeriodAsync(
            periodId,
            HoursApprovalState.Escalated,
            comment,
            actor,
            requireEmployee: false,
            cancellationToken);

    /// <summary>Returns a replayed worktime view and informational carry-boundary anomalies.</summary>
    public async ValueTask<HoursEmployeeView> GetEmployeeViewAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var view = HoursProjector.Replay(
            employeeId,
            await journal.ReadEmployeeAsync(employeeId, cancellationToken));
        return AddCarryBoundaryAnomalies(view, policyProvider.GetPolicy(employeeId));
    }

    private async ValueTask<HoursApprovalPeriod> UpdateApprovalPeriodAsync(
        Guid periodId,
        HoursApprovalState targetState,
        string comment,
        HoursActor actor,
        bool requireEmployee,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);
        var existing = await FindApprovalPeriodAsync(periodId, cancellationToken);
        if (requireEmployee)
        {
            EnsureEmployeeAction(existing.EmployeeId, actor);
        }
        else if (targetState == HoursApprovalState.ManagerApproved)
        {
            EnsureReviewer(actor);
        }
        else
        {
            EnsureReviewerOrSystem(actor);
        }

        var now = timeProvider.GetUtcNow();
        var updated = existing with
        {
            State = targetState,
            LastActionBy = actor,
            LastActionAtUtc = now,
            LastComment = comment,
        };
        await journal.AppendAsync(
            HoursEvent.Create(existing.EmployeeId, HoursEventType.ApprovalPeriodUpdated, updated, actor, now),
            cancellationToken);
        return updated;
    }

    private async ValueTask<HoursAdjustment> FindAdjustmentAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        var eventMatch = (await journal.ReadAllAsync(cancellationToken))
            .Where(item => item.Type is HoursEventType.AdjustmentProposed or HoursEventType.AdjustmentUpdated)
            .Select(item => item.ReadPayload<HoursAdjustment>())
            .Where(item => item.Id == adjustmentId)
            .OrderBy(item => item.ResolvedAtUtc ?? item.RespondedAtUtc ?? item.ProposedAtUtc)
            .LastOrDefault();
        return eventMatch ?? throw new KeyNotFoundException($"Hours adjustment '{adjustmentId}' was not found.");
    }

    private async ValueTask<HoursApprovalPeriod> FindApprovalPeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var eventMatch = (await journal.ReadAllAsync(cancellationToken))
            .Where(item => item.Type is HoursEventType.ApprovalPeriodOpened or HoursEventType.ApprovalPeriodUpdated)
            .Select(item => item.ReadPayload<HoursApprovalPeriod>())
            .Where(item => item.Id == periodId)
            .OrderBy(item => item.LastActionAtUtc ?? item.OpenedAtUtc)
            .LastOrDefault();
        return eventMatch ?? throw new KeyNotFoundException($"Approval period '{periodId}' was not found.");
    }

    private HoursEmployeeView AddCarryBoundaryAnomalies(HoursEmployeeView view, HoursPolicy policy)
    {
        var anomalies = view.Anomalies.ToBuilder();
        var carry = policy.LegalPreset.FlexCarryPolicy;
        if (view.FlexSaldo > carry.PositiveCarryCap)
        {
            anomalies.Add(new HoursAnomaly(
                "flex.positive-carry-cap-exceeded",
                $"Flex saldo exceeds the configured carry cap of {carry.PositiveCarryCap.TotalHours:0.##} hours; no normalization has been committed.",
                timeProvider.GetUtcNow()));
        }

        if (view.FlexSaldo < carry.NegativeCarryFloor)
        {
            anomalies.Add(new HoursAnomaly(
                "flex.negative-carry-floor-exceeded",
                $"Flex saldo is below the configured floor of {carry.NegativeCarryFloor.TotalHours:0.##} hours; the recorded hours remain unchanged.",
                timeProvider.GetUtcNow()));
        }

        return view with { Anomalies = anomalies.ToImmutable() };
    }

    private static ClockInterval? CreateBreak(RegisterWorkCommand command)
    {
        if (command.BreakStartedAt is null && command.BreakEndedAt is null)
        {
            return null;
        }

        if (command.BreakStartedAt is null || command.BreakEndedAt is null)
        {
            throw new ArgumentException("Break start and break end must both be supplied.");
        }

        return new ClockInterval(command.BreakStartedAt.Value, command.BreakEndedAt.Value);
    }

    private static void AddOverdueAnomaly(
        ImmutableArray<HoursAnomaly>.Builder anomalies,
        bool isOverdue,
        string code,
        string message,
        DateTimeOffset observedAtUtc,
        Guid relatedRecordId)
    {
        if (isOverdue && !anomalies.Any(item => item.Code == code))
        {
            anomalies.Add(new HoursAnomaly(code, message, observedAtUtc, relatedRecordId));
        }
    }

    private static void EnsureEmployeeAction(string employeeId, HoursActor actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Role == HoursActorRole.Employee && !string.Equals(actor.Id, employeeId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("An employee can only act on their own worktime.");
        }

        if (actor.Role is not (HoursActorRole.Employee or HoursActorRole.Administrator))
        {
            throw new UnauthorizedAccessException("Only the employee or an administrator can register employee worktime.");
        }
    }

    private static void EnsureReviewer(HoursActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Role is not (HoursActorRole.Manager or HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Administrator))
        {
            throw new UnauthorizedAccessException("A manager, HR, Higher, or administrator must perform this review action.");
        }
    }

    private static void EnsureReviewerOrSystem(HoursActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Role != HoursActorRole.System)
        {
            EnsureReviewer(actor);
        }
    }

    private static void EnsureEscalationReviewer(HoursActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Role is not (HoursActorRole.HumanResources or HoursActorRole.Higher or HoursActorRole.Administrator))
        {
            throw new UnauthorizedAccessException("Only HR, Higher, or an administrator can resolve an escalated adjustment.");
        }
    }
}
