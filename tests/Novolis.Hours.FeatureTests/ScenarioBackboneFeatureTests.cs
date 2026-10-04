using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Compliance;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Ledger;
using Novolis.Hours.Domain.Reporting;
using Novolis.Hours.Domain.Review;
using Novolis.Hours.Domain.Work;
using Novolis.Hours.Storage;

namespace Novolis.Hours.FeatureTests;

/// <summary>
/// End-to-end domain acceptance scenarios for the product's core promise:
/// record what happened, preserve why it was interpreted that way, and keep
/// derived meaning auditable without turning legal indicators into gates.
/// </summary>
public sealed class ScenarioBackboneFeatureTests
{
    [Test]
    public async Task Scenario_01_normal_day_uses_the_confirmed_routine_without_flex_or_compliance_movement()
    {
        var date = new DateOnly(2026, 10, 1);
        var shape = CreateRoutineShape(
            date,
            expectedWork: TimeSpan.FromHours(7.5),
            routine: [
                new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
                new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
            ],
            coreHours: [new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0))]);
        var registration = WorkRegistration.WorkedAsScheduled(
            new WorkDayKey("ada", date),
            ActorRef.Employee("ada", "Ada Lovelace"),
            ConfigurationSnapshotId.New(),
            RecordedAt);

        var resolved = new WorkDayResolver().Resolve(registration, shape);
        var dimensions = new DimensionEvaluator().Evaluate(
            resolved,
            CreateRoutineDifferenceConfiguration());
        var compliance = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(resolved, dimensions),
            []);
        var ledger = new LedgerProjector([
            new DimensionLedgerMapping("time-type", "flex-credit", 1),
        ]).Project(dimensions);

        await Assert.That(registration.Intent).IsEqualTo(WorkRecordIntent.WorkedAsScheduled);
        await Assert.That(registration.Source).IsEqualTo(WorkRecordSource.Employee);
        await Assert.That(registration.Intervals).IsEmpty();
        await Assert.That(shape.Rules).Contains(rule =>
            rule.Rule is RoutineWorkRule &&
            rule.Source == RuleSource.Manual);
        await Assert.That(resolved.WorkedIntervals.Length).IsEqualTo(2);
        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(dimensions.Measures).IsEmpty();
        await Assert.That(compliance.Indicators).IsEmpty();
        await Assert.That(ledger.Postings.All(posting =>
            posting.SignedDuration == TimeSpan.Zero)).IsTrue();
    }

    [Test]
    public async Task Scenario_02_honest_split_day_preserves_the_gap_and_does_not_turn_routine_difference_into_anomaly()
    {
        var date = new DateOnly(2026, 10, 2);
        var shape = CreateRoutineShape(
            date,
            expectedWork: TimeSpan.FromHours(7.5),
            routine: [
                new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
                new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
            ]);
        var registration = WorkRegistration.Manual(
            new WorkDayKey("ada", date),
            [
                Interval(date, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                Interval(date, new TimeOnly(16, 0), new TimeOnly(19, 30)),
            ],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            RecordedAt,
            "Dentist and family appointment.");

        var resolved = new WorkDayResolver().Resolve(registration, shape);
        var compliance = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(resolved),
            []);

        await Assert.That(resolved.WorkedIntervals.Length).IsEqualTo(2);
        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(resolved.RoutineComparison.OutsideRoutine)
            .IsGreaterThan(TimeSpan.Zero);
        await Assert.That(resolved.RoutineComparison.Differences)
            .Contains(difference => difference.Kind == RoutineDifferenceKind.SplitWorkDay);
        await Assert.That(registration.Note).IsEqualTo("Dentist and family appointment.");
        await Assert.That(compliance.Indicators).IsEmpty();
    }

    [Test]
    public async Task Scenario_03_cross_midnight_shift_is_one_logical_workday_and_can_emit_night_work_information()
    {
        var date = new DateOnly(2026, 10, 4);
        var registration = WorkRegistration.Manual(
            new WorkDayKey("bea", date),
            [
                new WorkInterval(
                    new DateTimeOffset(2026, 10, 4, 18, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 5, 2, 0, 0, TimeSpan.Zero)),
            ],
            ActorRef.Employee("bea"),
            ConfigurationSnapshotId.New(),
            RecordedAt,
            "Evening shift.");
        var shape = CreateRoutineShape(
            date,
            expectedWork: TimeSpan.FromHours(8),
            routine: [new LocalTimeRange(new TimeOnly(18, 0), new TimeOnly(2, 0))]);
        var resolved = new WorkDayResolver().Resolve(registration, shape);
        var ruleRef = new RuleRef("night-work", "1", RuleSource.Manual);
        var compliance = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(resolved),
            [
                new ComplianceRuleRegistration(
                    "night-work",
                    10,
                    ruleRef,
                    new NightWorkComplianceRule(
                        new TimeOnly(22, 0),
                        new TimeOnly(6, 0),
                        ruleRef)),
            ]);

        await Assert.That(resolved.Key.EmployeeId).IsEqualTo("bea");
        await Assert.That(resolved.Key.NominalDate).IsEqualTo(date);
        await Assert.That(resolved.WorkedIntervals.Length).IsEqualTo(1);
        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(compliance.Indicators).Contains(indicator =>
            indicator.Code == "work.night" &&
            indicator.WorkDay == resolved.Key);
    }

    [Test]
    public async Task Scenario_04_calendar_stack_keeps_holiday_provenance_when_a_24_7_calendar_reopens_the_day()
    {
        var holiday = new DateOnly(2021, 12, 26);
        var calendars = new CalendarRuleSet(
            [
                new Calendar(
                    "national",
                    "2026.1",
                    10,
                    [
                        new WeekdayCalendarRule(
                            "national-sunday",
                            [DayOfWeek.Sunday],
                            [
                                new WorkingDayRule(false),
                                new DayTagRule("calendar", "non-working-day"),
                            ]),
                        new FixedDateCalendarRule(
                            "christmas-public-holiday",
                            holiday,
                            [new DayTagRule("holiday", "Christmas Day")]),
                    ],
                    RuleSource.SourcePackage),
                new Calendar(
                    "gas-station",
                    "2026.1",
                    20,
                    [
                        new WeekdayCalendarRule(
                            "always-open-weekend",
                            [DayOfWeek.Saturday, DayOfWeek.Sunday],
                            [new WorkingDayRule(true)]),
                    ],
                    RuleSource.Manual),
                new Calendar(
                    "employment",
                    "2026.1",
                    30,
                    [
                        new EveryDateCalendarRule(
                            "employment-expectation",
                            [
                                new ExpectedWorkRule(TimeSpan.FromHours(7.5)),
                                new RoutineWorkRule([
                                    new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
                                    new LocalTimeRange(new TimeOnly(12, 0), new TimeOnly(16, 0)),
                                ]),
                            ]),
                    ],
                    RuleSource.Manual),
                new Calendar(
                    "agreement",
                    "2026.1",
                    40,
                    [
                        new FixedDateCalendarRule(
                            "christmas-entitlement",
                            holiday,
                            [new PaidEntitlementRule(TimeSpan.FromHours(7.5))]),
                    ],
                    RuleSource.Manual),
            ],
            "UTC");

        var shape = calendars.GetDayShape(holiday);

        await Assert.That(shape.IsWorkingDay).IsTrue();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.Tags).Contains(new DayTag("holiday", "Christmas Day"));
        await Assert.That(shape.Rules.Select(rule => rule.CalendarId))
            .Contains("national");
        await Assert.That(shape.Rules.Select(rule => rule.CalendarId))
            .Contains("gas-station");
        await Assert.That(shape.Rules.Select(rule => rule.CalendarId))
            .Contains("agreement");
    }

    [Test]
    public async Task Scenario_05_paid_corporate_day_has_zero_expected_work_without_fabricating_a_registration()
    {
        var date = new DateOnly(2026, 12, 24);
        var calendars = new CalendarRuleSet(
            [
                new Calendar(
                    "employment",
                    "2026.1",
                    10,
                    [
                        new WeekdayCalendarRule(
                            "weekday",
                            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                            [
                                new WorkingDayRule(true),
                                new ExpectedWorkRule(TimeSpan.FromHours(7.5)),
                                new RoutineWorkRule([
                                    new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
                                    new LocalTimeRange(new TimeOnly(12, 0), new TimeOnly(16, 0)),
                                ]),
                            ]),
                    ],
                    RuleSource.Manual),
                new Calendar(
                    "corporate",
                    "2026.2",
                    20,
                    [
                        new FixedDateCalendarRule(
                            "christmas-eve-paid-day",
                            date,
                            [
                                new WorkingDayRule(false),
                                new ExpectedWorkRule(TimeSpan.Zero),
                                new PaidEntitlementRule(TimeSpan.FromHours(7.5)),
                                new DayTagRule("holiday", "Christmas Eve"),
                            ]),
                    ],
                    RuleSource.Manual),
            ],
            "UTC");

        var shape = calendars.GetDayShape(date);
        var worked = WorkRegistration.Manual(
            new WorkDayKey("ada", date),
            [Interval(date, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            RecordedAt,
            "Voluntary work on a paid corporate day.");
        var resolved = new WorkDayResolver().Resolve(worked, shape);

        await Assert.That(shape.IsWorkingDay).IsFalse();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.Zero);
        await Assert.That(shape.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(3));
        await Assert.That(resolved.EffectiveRegistration.Intent)
            .IsEqualTo(WorkRecordIntent.ManualRegistration);
        await Assert.That(shape.Tags).Contains(new DayTag("holiday", "Christmas Eve"));
    }

    [Test]
    public async Task Scenario_06_effective_policy_change_interprets_each_historical_day_with_its_own_provenance()
    {
        var beforeChange = new DateOnly(2026, 6, 30);
        var afterChange = new DateOnly(2026, 7, 1);
        var calendars = new CalendarRuleSet(
            [
                new Calendar(
                    "flex-policy",
                    "v1",
                    10,
                    [
                        new EveryDateCalendarRule(
                            "weekday-v1",
                            [
                                new WorkingDayRule(true),
                                new ExpectedWorkRule(TimeSpan.FromHours(7.5)),
                                new RoutineWorkRule([
                                    new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 30)),
                                ]),
                            ]),
                    ],
                    RuleSource.Manual,
                    effectiveTo: beforeChange),
                new Calendar(
                    "flex-policy",
                    "v2",
                    10,
                    [
                        new EveryDateCalendarRule(
                            "weekday-v2",
                            [
                                new WorkingDayRule(true),
                                new ExpectedWorkRule(TimeSpan.FromHours(8)),
                                new RoutineWorkRule([
                                    new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(17, 0)),
                                ]),
                            ]),
                    ],
                    RuleSource.Manual,
                    effectiveFrom: afterChange),
            ],
            "UTC");
        var firstShape = calendars.GetDayShape(beforeChange);
        var secondShape = calendars.GetDayShape(afterChange);
        var first = WorkRegistration.Manual(
            new WorkDayKey("ada", beforeChange),
            [Interval(beforeChange, new TimeOnly(8, 0), new TimeOnly(16, 0))],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            RecordedAt);
        var second = WorkRegistration.Manual(
            new WorkDayKey("ada", afterChange),
            [Interval(afterChange, new TimeOnly(8, 0), new TimeOnly(16, 0))],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            RecordedAt);
        var resolver = new WorkDayResolver();
        var firstResolved = resolver.Resolve(first, firstShape);
        var secondResolved = resolver.Resolve(second, secondShape);

        await Assert.That(firstShape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(secondShape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(firstResolved.RoutineComparison.OutsideRoutine)
            .IsEqualTo(TimeSpan.Zero);
        await Assert.That(secondResolved.RoutineComparison.MissingRoutine)
            .IsEqualTo(TimeSpan.FromHours(1));
        await Assert.That(firstShape.Rules).Contains(rule =>
            rule.CalendarVersion == "v1");
        await Assert.That(secondShape.Rules).Contains(rule =>
            rule.CalendarVersion == "v2");
        await Assert.That(first.ConfigurationSnapshotId)
            .IsNotEqualTo(second.ConfigurationSnapshotId);
    }

    [Test]
    public async Task Scenario_07_derived_flex_is_a_balanced_two_account_transaction()
    {
        var evaluation = CreateFlexEvaluation(
            WorkRegistration.Manual(
                new WorkDayKey("ada", new DateOnly(2026, 10, 7)),
                [Interval(new DateOnly(2026, 10, 7), new TimeOnly(8, 0), new TimeOnly(16, 0))],
                ActorRef.Employee("ada"),
                ConfigurationSnapshotId.New(),
                RecordedAt),
            TimeSpan.FromMinutes(45));
        var transaction = new LedgerProjector([
            new DimensionLedgerMapping("time-type", "flex-credit", 1),
        ]).Project(evaluation);

        await Assert.That(transaction.Postings.Aggregate(
                TimeSpan.Zero,
                (total, posting) => total + posting.SignedDuration))
            .IsEqualTo(TimeSpan.Zero);
        await Assert.That(transaction.Postings).Contains(posting =>
            posting.Account == DurationAccount.EmployeeFlex &&
            posting.SignedDuration == TimeSpan.FromMinutes(45));
        await Assert.That(transaction.Postings).Contains(posting =>
            posting.Account == DurationAccount.OrganisationControl &&
            posting.SignedDuration == TimeSpan.FromMinutes(-45));
    }

    [Test]
    public async Task Scenario_08_correction_appends_a_difference_and_keeps_both_assertions_and_transactions()
    {
        var day = new DateOnly(2026, 10, 8);
        var original = WorkRegistration.Manual(
            new WorkDayKey("ada", day),
            [Interval(day, new TimeOnly(8, 0), new TimeOnly(16, 0))],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            RecordedAt,
            "Initial registration.");
        var correction = WorkRegistration.Correction(
            original.WorkDay,
            original.Id,
            [Interval(day, new TimeOnly(8, 0), new TimeOnly(15, 0))],
            ActorRef.Employee("ada"),
            original.ConfigurationSnapshotId,
            RecordedAt.AddMinutes(5),
            "Corrected registration.");
        var projector = new LedgerProjector([
            new DimensionLedgerMapping("time-type", "flex-credit", 1),
        ]);
        var originalTransaction = projector.Project(
            CreateFlexEvaluation(original, TimeSpan.FromHours(3)));
        var correctedTransaction = projector.Project(
            CreateFlexEvaluation(correction, TimeSpan.FromHours(2)));
        var correctionTransaction = projector.CreateCorrection(
            originalTransaction,
            correctedTransaction,
            "Corrected flex registration.");
        var journal = new InMemoryHoursJournal();

        await journal.AppendBatchAsync([
            HoursEvent.Create(
                "ada",
                HoursEventType.WorkRegistrationRecorded,
                original,
                original.RecordedBy.ToHoursActor(),
                original.RecordedAt),
            HoursEvent.Create(
                "ada",
                HoursEventType.LedgerTransactionRecorded,
                originalTransaction,
                original.RecordedBy.ToHoursActor(),
                original.RecordedAt),
        ]);
        await journal.AppendBatchAsync([
            HoursEvent.Create(
                "ada",
                HoursEventType.WorkRegistrationRecorded,
                correction,
                correction.RecordedBy.ToHoursActor(),
                correction.RecordedAt),
            HoursEvent.Create(
                "ada",
                HoursEventType.LedgerTransactionRecorded,
                correctionTransaction,
                correction.RecordedBy.ToHoursActor(),
                correction.RecordedAt),
        ]);

        var events = await journal.ReadEmployeeAsync("ada");
        var registrations = events
            .Where(entry => entry.Type == HoursEventType.WorkRegistrationRecorded)
            .Select(entry => entry.ReadPayload<WorkRegistration>())
            .ToArray();

        await Assert.That(original.Id).IsNotEqualTo(correction.Id);
        await Assert.That(correction.CorrectsRegistrationId).IsEqualTo(original.Id);
        await Assert.That(registrations.Select(registration => registration.Id))
            .Contains(original.Id);
        await Assert.That(registrations.Select(registration => registration.Id))
            .Contains(correction.Id);
        await Assert.That(registrations.Single(registration => registration.Id == correction.Id).Note)
            .IsEqualTo(correction.Note);
        await Assert.That(correctionTransaction.Postings).Contains(posting =>
            posting.Account == DurationAccount.EmployeeFlex &&
            posting.SignedDuration == TimeSpan.FromHours(-1));
        await Assert.That(new LedgerSaldoProjector().Replay([
            originalTransaction,
            correctionTransaction,
        ])).IsEqualTo(TimeSpan.FromHours(2));
    }

    [Test]
    public async Task Scenario_09_mutual_approval_and_dispute_keep_competing_assertions_visible_through_hr_resolution()
    {
        var day = new DateOnly(2026, 10, 9);
        var employerRegistration = WorkRegistration.Manual(
            new WorkDayKey("ada", day),
            [Interval(day, new TimeOnly(8, 0), new TimeOnly(15, 30))],
            ActorRef.Manager("mia"),
            ConfigurationSnapshotId.New(),
            RecordedAt,
            "Employer registration.",
            source: WorkRecordSource.Employer);
        var employeeCorrection = WorkRegistration.Correction(
            employerRegistration.WorkDay,
            employerRegistration.Id,
            [Interval(day, new TimeOnly(8, 0), new TimeOnly(16, 30))],
            ActorRef.Employee("ada"),
            employerRegistration.ConfigurationSnapshotId,
            RecordedAt.AddMinutes(5),
            "I worked until 16:30.");
        var period = ReviewPeriod.Create("ada", day, day);
        var policy = new ReviewPolicy(
            "monthly-mutual-review",
            "2026.1",
            [
                new ReviewStage("employee", ReviewActionKind.Acknowledge, ResponsibilityRole.Employee, null, 2),
                new ReviewStage("manager", ReviewActionKind.Approve, ResponsibilityRole.Manager, 1, 3),
            ]);
        var employeeAcknowledgement = ReviewAction.Create(
            period.Id,
            ReviewActionKind.Acknowledge,
            ActorRef.Employee("ada"),
            null,
            "I acknowledge the employer assertion but disagree with the end time.",
            RecordedAt.AddDays(1));
        var managerApproval = ReviewAction.Create(
            period.Id,
            ReviewActionKind.Approve,
            ActorRef.Manager("mia"),
            1,
            "Manager reviewed the registered period.",
            RecordedAt.AddDays(2));
        var dispute = ReviewAction.Create(
            period.Id,
            ReviewActionKind.Dispute,
            ActorRef.Employee("ada"),
            null,
            "The employer assertion ends one hour too early.",
            RecordedAt.AddDays(3));
        var disputed = new ReviewProjector().Project(
            period,
            policy,
            [employeeAcknowledgement, managerApproval, dispute],
            day.AddDays(3));
        var resolution = ReviewAction.Create(
            period.Id,
            ReviewActionKind.Resolve,
            ActorRef.HumanResources("hana"),
            null,
            "The employee correction is the effective assertion.",
            RecordedAt.AddDays(4));
        var resolved = new ReviewProjector().Project(
            period,
            policy,
            [employeeAcknowledgement, managerApproval, dispute, resolution],
            day.AddDays(4));

        await Assert.That(employerRegistration.Source).IsEqualTo(WorkRecordSource.Employer);
        await Assert.That(employeeCorrection.CorrectsRegistrationId)
            .IsEqualTo(employerRegistration.Id);
        await Assert.That(disputed.State).IsEqualTo(ReviewState.Disputed);
        await Assert.That(disputed.Escalations.Single().Target)
            .IsEqualTo(ReviewEscalationTarget.HumanResources);
        await Assert.That(resolved.State).IsEqualTo(ReviewState.Approved);
        await Assert.That(resolved.Escalations).IsEmpty();
        await Assert.That(resolved.Actions.Length).IsEqualTo(4);
        await Assert.That(resolved.Actions).Contains(dispute);
        await Assert.That(resolved.Actions).Contains(resolution);
    }

    [Test]
    public async Task Scenario_10_health_and_business_pressure_reports_are_traceable_without_person_ranking()
    {
        var firstDay = CreateResolvedDay("ada", new DateOnly(2026, 10, 12), TimeSpan.FromHours(12));
        var secondDay = CreateResolvedDay("bea", new DateOnly(2026, 10, 13), TimeSpan.FromHours(11));
        var ruleRef = new RuleRef("long-day", "1", RuleSource.Manual);
        var complianceRule = new ComplianceRuleRegistration(
            "long-day",
            10,
            ruleRef,
            new LongWorkdayComplianceRule(TimeSpan.FromHours(8), ruleRef));
        var firstCompliance = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(firstDay),
            [complianceRule]);
        var secondCompliance = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(secondDay),
            [complianceRule]);

        var health = new HealthConcernsProjector().Project(
            firstCompliance.Indicators.Concat(secondCompliance.Indicators));
        var overtime = new WorkInterval(
            new DateTimeOffset(2026, 10, 12, 17, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 12, 19, 0, 0, TimeSpan.Zero));
        var customer = new WorkInterval(
            new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.Zero));
        var dimensions = new DimensionEvaluation(
            firstDay,
            [
                new DimensionMeasure(
                    "time-type",
                    "overtime",
                    overtime.Duration,
                    overtime,
                    DimensionMeasureSource.Derived,
                    ruleRef),
                new DimensionMeasure(
                    "project",
                    "acme",
                    customer.Duration,
                    customer,
                    DimensionMeasureSource.Manual),
            ],
            []);
        var pressure = new BusinessPressureProjector().Project(
            [dimensions],
            "time-type",
            "overtime",
            "project");

        await Assert.That(health.Concerns).Contains(concern =>
            concern.Code == "worked-duration.above-threshold" &&
            concern.SourceWorkDays.Contains(firstDay.Key) &&
            concern.SourceWorkDays.Contains(secondDay.Key));
        await Assert.That(health.Concerns.All(concern =>
            concern.SourceWorkDays.Length > 0)).IsTrue();
        await Assert.That(pressure.Rows).Contains(row =>
            row.ValueId == "acme" &&
            row.AssociatedDuration == TimeSpan.FromHours(1));
        await Assert.That(pressure.Rows).Contains(row =>
            row.ValueId == "Unattributed" &&
            row.AssociatedDuration == TimeSpan.FromHours(1));
    }

    private static DateTimeOffset RecordedAt =>
        new(2026, 10, 1, 16, 0, 0, TimeSpan.Zero);

    private static WorkInterval Interval(DateOnly date, TimeOnly start, TimeOnly end) =>
        new(
            new DateTimeOffset(date.ToDateTime(start), TimeSpan.Zero),
            new DateTimeOffset(
                (end <= start ? date.AddDays(1) : date).ToDateTime(end),
                TimeSpan.Zero));

    private static DayShape CreateRoutineShape(
        DateOnly date,
        TimeSpan expectedWork,
        IEnumerable<LocalTimeRange> routine,
        IEnumerable<LocalTimeRange>? coreHours = null)
    {
        var routineRanges = routine.ToArray();
        return new DayShape(
            date,
            true,
            expectedWork,
            TimeSpan.Zero,
            new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
            coreHours ?? [],
            routineRanges,
            [],
            [
                new AppliedDayRule(
                    "routine",
                    "employment",
                    "2026.1",
                    10,
                    new RoutineWorkRule(routineRanges),
                    RuleSource.Manual),
            ],
            "UTC");
    }

    private static DimensionConfiguration CreateRoutineDifferenceConfiguration() =>
        new([
            new DimensionConfigurationLayer(
                "organisation",
                "2026.1",
                10,
                [
                    new DimensionDefinition(
                        "time-type",
                        "Time type",
                        DimensionAssignmentMode.Derived,
                        DimensionCardinality.Exclusive,
                        false,
                        [new DimensionValue("flex-credit", "Flex credit")]),
                ],
                [
                    new DimensionRuleRegistration(
                        "routine-difference",
                        10,
                        new RuleRef("routine-difference", "1", RuleSource.Manual),
                        new RoutineDifferenceDimensionRule(
                            "time-type",
                            "flex-credit",
                            null)),
                ],
                RuleSource.Manual),
        ]);

    private static DimensionEvaluation CreateFlexEvaluation(
        WorkRegistration registration,
        TimeSpan signedDuration)
    {
        var resolved = new WorkDayResolver().Resolve(
            registration,
            CreateRoutineShape(
                registration.WorkDay.NominalDate,
                TimeSpan.FromHours(8),
                [new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0))]));
        var configuration = new DimensionConfiguration([
            new DimensionConfigurationLayer(
                "organisation",
                "2026.1",
                10,
                [
                    new DimensionDefinition(
                        "time-type",
                        "Time type",
                        DimensionAssignmentMode.Derived,
                        DimensionCardinality.Exclusive,
                        false,
                        [new DimensionValue("flex-credit", "Flex credit")]),
                ],
                [
                    new DimensionRuleRegistration(
                        "flex",
                        10,
                        new RuleRef("flex", "1", RuleSource.Manual),
                        new SignedDurationDimensionRule(
                            "time-type",
                            "flex-credit",
                            signedDuration)),
                ],
                RuleSource.Manual),
        ]);
        return new DimensionEvaluator().Evaluate(resolved, configuration);
    }

    private static ResolvedWorkDay CreateResolvedDay(
        string employeeId,
        DateOnly date,
        TimeSpan duration)
    {
        var registration = WorkRegistration.Manual(
            new WorkDayKey(employeeId, date),
            [
                new WorkInterval(
                    new DateTimeOffset(date.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero),
                    new DateTimeOffset(date.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero) + duration),
            ],
            ActorRef.Employee(employeeId),
            ConfigurationSnapshotId.New(),
            RecordedAt);
        return new WorkDayResolver().Resolve(
            registration,
            CreateRoutineShape(
                date,
                TimeSpan.FromHours(8),
                [new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0))]));
    }
}
