using System.Collections.Immutable;
using Novolis.Hours.Contracts;
using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Compliance;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Ledger;
using Novolis.Hours.Domain.Reporting;
using Novolis.Hours.Domain.Review;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Application;

/// <summary>Replays authoritative Hours facts through the existing domain projectors.</summary>
public sealed class HoursAcceptanceProjectionService
{
    private readonly IHoursJournal journal;
    private readonly HoursAcceptanceConfiguration configuration;

    /// <summary>Initializes the replay service.</summary>
    public HoursAcceptanceProjectionService(
        IHoursJournal journal,
        HoursAcceptanceConfiguration configuration)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>Rebuilds one WorkDay explanation from journal facts and effective configuration.</summary>
    public async ValueTask<WorkDayResponse> GetWorkDayAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var events = await journal.ReadEmployeeAsync(employeeId, cancellationToken);
        return BuildWorkDay(employeeId, date, events);
    }

    /// <summary>Rebuilds a contiguous range of WorkDay explanations from one journal read.</summary>
    public async ValueTask<ImmutableArray<WorkDayResponse>> GetWorkDaysAsync(
        string employeeId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        if (through < from)
        {
            throw new ArgumentException("The through date must not precede the from date.", nameof(through));
        }

        if (through.DayNumber - from.DayNumber + 1 > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(through), "A WorkDay range may cover at most 31 days.");
        }

        var events = await journal.ReadEmployeeAsync(employeeId, cancellationToken);
        var days = ImmutableArray.CreateBuilder<WorkDayResponse>();
        for (var date = from; date <= through; date = date.AddDays(1))
        {
            days.Add(BuildWorkDay(employeeId, date, events));
        }

        return days.ToImmutable();
    }

    /// <summary>Reads all v2 registrations for an employee in journal order.</summary>
    public async ValueTask<ImmutableArray<WorkRegistration>> GetRegistrationsAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        var events = await journal.ReadEmployeeAsync(employeeId, cancellationToken);
        return ReadRegistrations(events);
    }

    /// <summary>Rebuilds one review projection from a period and its append-only actions.</summary>
    public async ValueTask<ReviewProjectionResponse> GetReviewAsync(
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        if (periodId == Guid.Empty)
        {
            throw new ArgumentException("A review period identity is required.", nameof(periodId));
        }

        var events = await journal.ReadAllAsync(cancellationToken);
        var periodEvent = events
            .Where(entry => entry.Type == HoursEventType.ReviewPeriodCreated)
            .Select(entry => entry.ReadPayload<ReviewPeriod>())
            .SingleOrDefault(period => period.Id == periodId);
        if (periodEvent is null)
        {
            throw new KeyNotFoundException($"Review period '{periodId}' was not found.");
        }

        var actions = events
            .Where(entry => entry.Type == HoursEventType.ReviewActionRecorded)
            .Select(entry => entry.ReadPayload<ReviewAction>())
            .Where(action => action.PeriodId == periodId)
            .OrderBy(action => action.RecordedAt)
            .ThenBy(action => action.Id)
            .ToImmutableArray();
        var latestFactAt = events
            .Where(entry =>
                entry.EmployeeId == periodEvent.EmployeeId &&
                entry.Type == HoursEventType.WorkRegistrationRecorded)
            .Select(entry => (DateTimeOffset?)entry.OccurredAtUtc)
            .Max();
        var projection = new ReviewProjector().Project(
            periodEvent,
            configuration.GetReviewPolicy(periodEvent.EmployeeId),
            actions,
            DateOnly.FromDateTime(DateTime.UtcNow),
            latestFactAt);
        return new ReviewProjectionResponse(
            projection.Period.Id,
            projection.Period.EmployeeId,
            projection.Period.From,
            projection.Period.Through,
            projection.Policy.Id,
            projection.State.ToString(),
            projection.ChangedAfterApproval,
            projection.Actions.Select(ToResponse).ToImmutableArray(),
            projection.Stages
                .Select(stage => new ReviewStageResponse(
                    stage.Stage.Id,
                    stage.Stage.RequiredAction.ToString(),
                    stage.Stage.Role.ToString(),
                    stage.Stage.ApprovalLevel,
                    stage.DueDate,
                    stage.IsComplete,
                    stage.IsOverdue,
                    stage.SatisfiedBy?.Id))
                .ToImmutableArray(),
            projection.Anomalies.Select(anomaly => anomaly.Message).ToImmutableArray(),
            projection.Escalations
                .Select(escalation => $"{escalation.Target}: {escalation.Reason}")
                .ToImmutableArray(),
            GetJournalHead(events));
    }

    /// <summary>Returns the latest event identity used for optimistic review concurrency.</summary>
    public async ValueTask<Guid?> GetJournalHeadAsync(CancellationToken cancellationToken = default)
    {
        var events = await journal.ReadAllAsync(cancellationToken);
        return GetJournalHead(events);
    }

    private static Guid? GetJournalHead(IEnumerable<HoursEvent> events) =>
        events
            .OrderBy(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.Id)
            .Select(entry => (Guid?)entry.Id)
            .LastOrDefault();

    /// <summary>Builds the organisation health projection from all registered WorkDays.</summary>
    public async ValueTask<HealthConcernsReportResponse> GetHealthReportAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await journal.ReadAllAsync(cancellationToken);
        var workDays = ReadRegistrations(events)
            .GroupBy(registration => (registration.WorkDay.EmployeeId, registration.WorkDay.NominalDate))
            .Select(group => BuildWorkDay(
                group.Key.EmployeeId,
                group.Key.NominalDate,
                events.Where(entry => entry.EmployeeId == group.Key.EmployeeId).ToImmutableArray()))
            .ToArray();
        var indicators = workDays
            .SelectMany(day => day.ComplianceIndicators)
            .Select(indicator => new ComplianceIndicator(
                indicator.Code,
                indicator.Category,
                indicator.Message,
                new WorkDayKey(indicator.EmployeeId, indicator.NominalDate),
                indicator.AffectedInterval is null
                    ? null
                    : new WorkInterval(
                        indicator.AffectedInterval.Start,
                        indicator.AffectedInterval.End),
                new RuleRef(indicator.RuleId, "1", RuleSource.Manual)));
        var report = new HealthConcernsProjector().Project(indicators);
        return new HealthConcernsReportResponse(
            report.Concerns
                .Select(row => new HealthConcernRowResponse(
                    row.Code,
                    row.Category,
                    row.IndicatorCount,
                    row.AffectedDuration,
                    row.SourceWorkDays.Select(ToWorkDayKey).ToImmutableArray()))
                .ToImmutableArray(),
            "Aggregated working-pattern indicators for organisational understanding; rows are not employee rankings.");
    }

    /// <summary>Builds the business-pressure projection using explicit Dimension measures.</summary>
    public async ValueTask<BusinessPressureReportResponse> GetBusinessPressureReportAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await journal.ReadAllAsync(cancellationToken);
        var workDays = ReadRegistrations(events)
            .GroupBy(registration => (registration.WorkDay.EmployeeId, registration.WorkDay.NominalDate))
            .Select(group => BuildWorkDay(
                group.Key.EmployeeId,
                group.Key.NominalDate,
                events.Where(entry => entry.EmployeeId == group.Key.EmployeeId).ToImmutableArray()))
            .ToArray();
        var evaluations = workDays
            .Where(day => day.Registration is not null)
            .Select(day => ToEvaluation(day))
            .ToArray();
        var report = new BusinessPressureProjector().Project(
            evaluations,
            "time-type",
            "flex-credit",
            "customer");
        return new BusinessPressureReportResponse(
            report.Rows
                .Select(row => new BusinessPressureRowResponse(
                    row.DimensionId,
                    row.ValueId,
                    row.AssociatedDuration,
                    row.SourceWorkDays.Select(ToWorkDayKey).ToImmutableArray()))
                .ToImmutableArray(),
            "Rows describe temporal overlap between recorded extra-work measures and allocations; they do not establish causation.");
    }

    /// <summary>Maps a review period action into the wire contract.</summary>
    public static ReviewActionResponse ToResponse(ReviewAction action) =>
        new(
            action.Id,
            action.PeriodId,
            action.Kind.ToString(),
            action.Actor.Id,
            action.Actor.Role,
            action.ApprovalLevel,
            action.Comment,
            action.RecordedAt);

    private WorkDayResponse BuildWorkDay(
        string employeeId,
        DateOnly date,
        IEnumerable<HoursEvent> employeeEvents)
    {
        var retainedEvents = employeeEvents.ToImmutableArray();
        var publications = ReadPublications(retainedEvents);
        var snapshot = configuration.GetSnapshot(employeeId, date, publications);
        var retainedSnapshot = retainedEvents
            .Where(entry => entry.Type == HoursEventType.ConfigurationSnapshotRecorded)
            .Select(entry => entry.ReadPayload<ConfigurationSnapshot>())
            .FirstOrDefault(candidate => candidate.Id == snapshot.Id);
        snapshot = retainedSnapshot ?? snapshot;
        var shape = configuration.GetCalendar(employeeId, date, snapshot.CalendarVersion)
            .GetDayShape(date);
        var allRegistrations = ReadRegistrations(retainedEvents)
            .OrderBy(registration => registration.RecordedAt)
            .ThenBy(registration => registration.Id)
            .ToImmutableArray();
        var registrations = allRegistrations
            .Where(registration => registration.WorkDay.NominalDate == date)
            .ToImmutableArray();
        var registration = registrations.LastOrDefault();
        var appliedRules = shape.Rules.Select(ToResponse).ToImmutableArray();
        if (registration is null)
        {
            return new WorkDayResponse(
                employeeId,
                HoursCustomerCatalog.OrganisationId(employeeId),
                date,
                shape.IsWorkingDay,
                shape.ExpectedWork,
                shape.PaidEntitlement,
                FormatOptional(shape.WorkEnvelope),
                shape.CoreHours.Select(Format).ToImmutableArray(),
                shape.RoutineWork.Select(Format).ToImmutableArray(),
                shape.Tags.Select(tag => $"{tag.Key}: {tag.Value}").ToImmutableArray(),
                appliedRules,
                null,
                [],
                TimeSpan.Zero,
                TimeSpan.Zero,
                shape.ExpectedWork,
                [],
                [],
                [],
                [],
                [],
                ToSnapshotResponse(snapshot),
                ToDtoOptional(shape.WorkEnvelope),
                shape.CoreHours.Select(ToDto).ToImmutableArray(),
                shape.RoutineWork.Select(ToDto).ToImmutableArray(),
                ToBrushes(employeeId));
        }

        var resolved = new WorkDayResolver().Resolve(registration, shape);
        var resolver = new WorkDayResolver();
        var surrounding = allRegistrations
            .Where(candidate => candidate.WorkDay.NominalDate != date)
            .Select(candidate =>
            {
                var candidateSnapshot = configuration.GetSnapshot(
                    candidate.WorkDay.EmployeeId,
                    candidate.WorkDay.NominalDate,
                    publications);
                var candidateShape = configuration
                    .GetCalendar(
                        candidate.WorkDay.EmployeeId,
                        candidate.WorkDay.NominalDate,
                        candidateSnapshot.CalendarVersion)
                    .GetDayShape(candidate.WorkDay.NominalDate);
                return resolver.Resolve(candidate, candidateShape);
            })
            .ToImmutableArray();
        var assignments = retainedEvents
            .Where(entry => entry.Type == HoursEventType.DimensionAssignmentRecorded)
            .Select(entry => entry.ReadPayload<DimensionAssignment>());
        var dimensions = new DimensionEvaluator().Evaluate(
            resolved,
            configuration.GetDimensions(employeeId),
            assignments);
        var compliance = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(resolved, dimensions, surrounding),
            configuration.GetComplianceRules(employeeId));
        var ledgerTransactions = retainedEvents
            .Where(entry => entry.Type == HoursEventType.LedgerTransactionRecorded)
            .Select(entry => entry.ReadPayload<LedgerTransaction>())
            .Where(transaction => transaction.WorkDay == registration.WorkDay)
            .Select(ToResponse)
            .ToImmutableArray();
        return new WorkDayResponse(
            employeeId,
            HoursCustomerCatalog.OrganisationId(employeeId),
            date,
            shape.IsWorkingDay,
            shape.ExpectedWork,
            shape.PaidEntitlement,
            FormatOptional(shape.WorkEnvelope),
            shape.CoreHours.Select(Format).ToImmutableArray(),
            shape.RoutineWork.Select(Format).ToImmutableArray(),
            shape.Tags.Select(tag => $"{tag.Key}: {tag.Value}").ToImmutableArray(),
            appliedRules,
            ToResponse(registration),
            resolved.WorkedIntervals.Select(ToRequest).ToImmutableArray(),
            resolved.ActualWorked,
            resolved.RoutineComparison.OutsideRoutine,
            resolved.RoutineComparison.MissingRoutine,
            resolved.RoutineComparison.Differences
                .Select(difference => new RoutineDifferenceResponse(
                    difference.Kind.ToString(),
                    difference.Duration,
                    difference.Description))
                .ToImmutableArray(),
            dimensions.Measures.Select(ToResponse).ToImmutableArray(),
            dimensions.MissingCoverage,
            compliance.Indicators.Select(ToResponse).ToImmutableArray(),
            ledgerTransactions,
            ToSnapshotResponse(registration.ConfigurationSnapshotId, snapshot),
            ToDtoOptional(shape.WorkEnvelope),
            shape.CoreHours.Select(ToDto).ToImmutableArray(),
            shape.RoutineWork.Select(ToDto).ToImmutableArray(),
            ToBrushes(employeeId));
    }

    private DimensionEvaluation ToEvaluation(WorkDayResponse response)
    {
        if (response.Registration is null)
        {
            throw new InvalidOperationException("A business-pressure WorkDay requires a registration.");
        }

        var registration = new WorkRegistration(
            response.Registration.Id,
            new WorkDayKey(response.EmployeeId, response.NominalDate),
            Enum.Parse<Novolis.Hours.Domain.Work.WorkRecordSource>(response.Registration.Source),
            (WorkRecordIntent)response.Registration.Intent,
            response.Registration.Intervals.Select(interval => new WorkInterval(interval.Start, interval.End)).ToImmutableArray(),
            response.Registration.CorrectsRegistrationId,
            response.Registration.Note,
            new ActorRef(response.Registration.RecordedBy, response.Registration.RecordedBy, response.Registration.RecordedByRole),
            response.Registration.RecordedAt,
            new ConfigurationSnapshotId(response.Registration.ConfigurationSnapshotId));
        var shape = configuration
            .GetCalendar(
                response.EmployeeId,
                response.NominalDate,
                response.Configuration.CalendarVersion)
            .GetDayShape(response.NominalDate);
        var resolved = new WorkDayResolver().Resolve(registration, shape);
        var assignments = response.Dimensions
            .Where(measure => measure.Source == DimensionMeasureSource.Manual.ToString() && measure.Interval is not null)
            .Select(measure => DimensionAssignment.Create(
                resolved.Key,
                measure.DimensionId,
                measure.ValueId,
                [new WorkInterval(measure.Interval!.Start, measure.Interval.End)],
                registration.RecordedBy,
                registration.RecordedAt));
        return new DimensionEvaluator().Evaluate(
            resolved,
            configuration.GetDimensions(response.EmployeeId),
            assignments);
    }

    private static ImmutableArray<WorkRegistration> ReadRegistrations(IEnumerable<HoursEvent> events) =>
        events
            .Where(entry => entry.Type == HoursEventType.WorkRegistrationRecorded)
            .Select(entry => entry.ReadPayload<WorkRegistration>())
            .ToImmutableArray();

    private static ImmutableArray<ConfigurationPublication> ReadPublications(
        IEnumerable<HoursEvent> events) =>
        events
            .Where(entry => entry.Type == HoursEventType.ConfigurationPublished)
            .Select(entry => entry.ReadPayload<ConfigurationPublication>())
            .ToImmutableArray();

    private static AppliedDayRuleResponse ToResponse(AppliedDayRule applied) =>
        new(
            applied.RuleId,
            applied.CalendarId,
            applied.CalendarVersion,
            applied.Order,
            applied.Rule.GetType().Name,
            applied.Rule switch
            {
                WorkingDayRule working => $"Working day = {working.Value}",
                ExpectedWorkRule expected => $"Expected work = {expected.Duration}",
                PaidEntitlementRule paid => $"Paid entitlement = {paid.Duration}",
                WorkEnvelopeRule envelope => $"Envelope = {Format(envelope.Range)}",
                CoreHoursRule core => $"Core hours = {string.Join(", ", core.Ranges.Select(Format))}",
                RoutineWorkRule routine => $"Routine = {string.Join(", ", routine.Ranges.Select(Format))}",
                DayTagRule tag => $"Tag = {tag.Tag.Key}:{tag.Tag.Value}",
                _ => applied.Rule.ToString() ?? applied.Rule.GetType().Name,
            },
            applied.Source.ToString(),
            applied.LayerKind.ToString(),
            applied.Provenance?.Jurisdiction,
            applied.Provenance?.HolidayId,
            applied.Provenance?.SourcePackage,
            applied.Provenance?.SourcePackageVersion,
            applied.Provenance?.GeneratorVersion);

    private static DimensionMeasureResponse ToResponse(DimensionMeasure measure) =>
        new(
            measure.DimensionId,
            measure.ValueId,
            measure.Duration,
            measure.Interval is null ? null : ToRequest(measure.Interval),
            measure.Source.ToString(),
            measure.Rule?.RuleId);

    private static ComplianceIndicatorResponse ToResponse(ComplianceIndicator indicator) =>
        new(
            indicator.Code,
            indicator.Category,
            indicator.Message,
            indicator.WorkDay.EmployeeId,
            indicator.WorkDay.NominalDate,
            indicator.AffectedInterval is null ? null : ToRequest(indicator.AffectedInterval),
            indicator.Rule?.RuleId ?? "unknown");

    private static LedgerTransactionResponse ToResponse(LedgerTransaction transaction) =>
        new(
            transaction.Id,
            transaction.WorkDay.EmployeeId,
            transaction.WorkDay.NominalDate,
            transaction.SourceRegistrationId,
            transaction.Postings
                .Select(posting => new LedgerPostingResponse(
                    posting.Account.ToString(),
                    posting.SignedDuration))
                .ToImmutableArray(),
            transaction.Reason);

    private static WorkRegistrationResponse ToResponse(WorkRegistration registration) =>
        new(
            registration.Id,
            registration.WorkDay.EmployeeId,
            registration.WorkDay.NominalDate,
            registration.Source.ToString(),
            (WorkRegistrationIntent)registration.Intent,
            registration.Intervals.Select(ToRequest).ToImmutableArray(),
            registration.CorrectsRegistrationId,
            registration.Note,
            registration.RecordedBy.Id,
            registration.RecordedBy.Role,
            registration.RecordedAt,
            registration.ConfigurationSnapshotId.Value);

    private static WorkIntervalRequest ToRequest(WorkInterval interval) =>
        new(interval.Start, interval.End);

    private static ConfigurationSnapshotResponse ToSnapshotResponse(ConfigurationSnapshot snapshot) =>
        new(
            snapshot.Id.Value,
            snapshot.CalendarVersion,
            snapshot.DimensionVersion,
            snapshot.ComplianceVersion,
            snapshot.WorkflowVersion,
            snapshot.LedgerVersion,
            snapshot.TimeZoneId);

    private static ConfigurationSnapshotResponse ToSnapshotResponse(
        ConfigurationSnapshotId id,
        ConfigurationSnapshot fallback) =>
        id == fallback.Id
            ? ToSnapshotResponse(fallback)
            : new(
                id.Value,
                fallback.CalendarVersion,
                fallback.DimensionVersion,
                fallback.ComplianceVersion,
                fallback.WorkflowVersion,
                fallback.LedgerVersion,
                fallback.TimeZoneId);

    private static string ToWorkDayKey(WorkDayKey key) =>
        $"{key.EmployeeId}:{key.NominalDate:yyyy-MM-dd}";

    private static string Format(LocalTimeRange range) =>
        $"{range.Start:HH\\:mm}–{range.End:HH\\:mm}";

    private static string? FormatOptional(LocalTimeRange? range) =>
        range is null ? null : Format(range);

    private static LocalTimeRangeDto? ToDtoOptional(LocalTimeRange? range) =>
        range is null ? null : ToDto(range);

    private static LocalTimeRangeDto ToDto(LocalTimeRange range) =>
        new(range.Start, range.End);

    private ImmutableArray<DimensionBrushResponse> ToBrushes(string employeeId) =>
        configuration.GetDimensions(employeeId).Definitions
            .Where(definition =>
                definition.AssignmentMode is DimensionAssignmentMode.Manual
                    or DimensionAssignmentMode.DerivedAndManual)
            .Select(definition => new DimensionBrushResponse(
                definition.Id,
                definition.Name,
                definition.Values
                    .Select(value => new DimensionBrushValueResponse(value.Id, value.Name))
                    .ToImmutableArray()))
            .ToImmutableArray();
}
