using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Compliance;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Reporting;
using Novolis.Hours.Domain.Review;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.FeatureTests;

public sealed class ReviewComplianceFeatureTests
{
    [Test]
    public async Task Sequential_review_deadlines_are_visible_without_locking_later_work()
    {
        var period = ReviewPeriod.Create(
            "ada",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30));
        var policy = CreatePolicy();
        ReviewAction[] actions =
        [
            ReviewAction.Create(
                period.Id,
                ReviewActionKind.Submit,
                ActorRef.Employee("ada"),
                null,
                null,
                new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero)),
            ReviewAction.Create(
                period.Id,
                ReviewActionKind.Approve,
                ActorRef.Manager("mia"),
                1,
                "Reviewed the registered period.",
                new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)),
        ];

        var projection = new ReviewProjector().Project(
            period,
            policy,
            actions,
            new DateOnly(2026, 10, 12));

        await Assert.That(projection.State).IsEqualTo(ReviewState.Submitted);
        await Assert.That(projection.OutstandingStages.Select(stage => stage.Stage.Id))
            .Contains("hr");
        await Assert.That(projection.Anomalies.Select(anomaly => anomaly.Code))
            .Contains("review.hr.overdue");
        await Assert.That(projection.Stages[0].DueDate)
            .IsEqualTo(new DateOnly(2026, 10, 2));
        await Assert.That(projection.Stages[1].DueDate)
            .IsEqualTo(new DateOnly(2026, 10, 7));
    }

    [Test]
    public async Task Dispute_escalates_to_configured_hr_or_higher_target_and_resolution_preserves_history()
    {
        var period = ReviewPeriod.Create(
            "ada",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30));
        var policy = new ReviewPolicy(
            "monthly",
            "2026.1",
            [new ReviewStage("employee", ReviewActionKind.Submit, ResponsibilityRole.Employee, null, 2)],
            ReviewEscalationTarget.Higher);
        var dispute = ReviewAction.Create(
            period.Id,
            ReviewActionKind.Dispute,
            ActorRef.Employee("ada"),
            null,
            "The employer correction does not describe the work I recorded.",
            new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero));

        var disputed = new ReviewProjector().Project(
            period,
            policy,
            [dispute],
            new DateOnly(2026, 10, 3));
        var resolved = ReviewAction.Create(
            period.Id,
            ReviewActionKind.Resolve,
            ActorRef.Higher("director"),
            null,
            "The competing assertions remain visible; the employee assertion is effective.",
            new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero));
        var afterResolution = new ReviewProjector().Project(
            period,
            policy,
            [dispute, resolved],
            new DateOnly(2026, 10, 4));

        await Assert.That(disputed.State).IsEqualTo(ReviewState.Disputed);
        await Assert.That(disputed.Escalations[0].Target)
            .IsEqualTo(ReviewEscalationTarget.Higher);
        await Assert.That(afterResolution.Escalations).IsEmpty();
        await Assert.That(afterResolution.Actions.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Correction_after_approval_reopens_current_steps_but_keeps_old_approval()
    {
        var period = ReviewPeriod.Create(
            "ada",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30));
        var policy = CreatePolicy();
        var approvalAt = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        ReviewAction[] actions =
        [
            ReviewAction.Create(
                period.Id,
                ReviewActionKind.Submit,
                ActorRef.Employee("ada"),
                null,
                null,
                new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero)),
            ReviewAction.Create(
                period.Id,
                ReviewActionKind.Approve,
                ActorRef.Manager("mia"),
                1,
                "Manager approved.",
                new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)),
            ReviewAction.Create(
                period.Id,
                ReviewActionKind.Approve,
                ActorRef.HumanResources("hana"),
                2,
                "HR approved.",
                approvalAt),
        ];

        var projection = new ReviewProjector().Project(
            period,
            policy,
            actions,
            new DateOnly(2026, 10, 12),
            approvalAt.AddHours(1));

        await Assert.That(projection.ChangedAfterApproval).IsTrue();
        await Assert.That(projection.Actions.Length).IsEqualTo(3);
        await Assert.That(projection.OutstandingStages.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Compliance_is_post_registration_information_and_health_report_has_no_person_ranking()
    {
        var resolved = CreateResolvedDay(TimeSpan.FromHours(12));
        var ruleRef = new RuleRef("long-day", "1", RuleSource.Manual);
        var evaluation = new ComplianceEvaluator().Evaluate(
            new ComplianceContext(resolved),
            [
                new ComplianceRuleRegistration(
                    "long-day",
                    10,
                    ruleRef,
                    new LongWorkdayComplianceRule(TimeSpan.FromHours(8), ruleRef)),
            ]);
        var report = new HealthConcernsProjector().Project(evaluation.Indicators);

        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(12));
        await Assert.That(evaluation.Indicators).Contains(indicator =>
            indicator.Code == "worked-duration.above-threshold" &&
            indicator.Rule.RuleId == "long-day");
        await Assert.That(report.Concerns).Contains(concern =>
            concern.Code == "worked-duration.above-threshold" &&
            concern.SourceWorkDays.Contains(resolved.Key));
    }

    [Test]
    public async Task Business_pressure_uses_temporal_overlap_and_keeps_unattributed_remainder()
    {
        var resolved = CreateResolvedDay(TimeSpan.FromHours(3));
        var ruleRef = new RuleRef("overtime", "1", RuleSource.Manual);
        var overtime = new WorkInterval(
            new DateTimeOffset(2026, 10, 1, 17, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 1, 19, 0, 0, TimeSpan.Zero));
        var acme = new WorkInterval(
            new DateTimeOffset(2026, 10, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero));
        var dimensions = new DimensionEvaluation(
            resolved,
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
                    acme.Duration,
                    acme,
                    DimensionMeasureSource.Manual),
            ],
            []);

        var report = new BusinessPressureProjector().Project(
            [dimensions],
            "time-type",
            "overtime",
            "project");

        await Assert.That(report.Rows).Contains(row =>
            row.ValueId == "acme" && row.AssociatedDuration == TimeSpan.FromHours(1));
        await Assert.That(report.Rows).Contains(row =>
            row.ValueId == "Unattributed" && row.AssociatedDuration == TimeSpan.FromHours(1));
    }

    private static ReviewPolicy CreatePolicy() =>
        new(
            "monthly",
            "2026.1",
            [
                new ReviewStage("employee", ReviewActionKind.Submit, ResponsibilityRole.Employee, null, 2),
                new ReviewStage("manager", ReviewActionKind.Approve, ResponsibilityRole.Manager, 1, 3),
                new ReviewStage("hr", ReviewActionKind.Approve, ResponsibilityRole.HumanResources, 2, 2),
            ]);

    private static ResolvedWorkDay CreateResolvedDay(TimeSpan duration)
    {
        var date = new DateOnly(2026, 10, 1);
        var registration = WorkRegistration.Manual(
            new WorkDayKey("ada", date),
            [
                new WorkInterval(
                    new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero) + duration),
            ],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            DateTimeOffset.UtcNow);
        var shape = new DayShape(
            date,
            true,
            duration,
            TimeSpan.Zero,
            null,
            [],
            [new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0))],
            [],
            [],
            "UTC");
        return new WorkDayResolver().Resolve(registration, shape);
    }
}
