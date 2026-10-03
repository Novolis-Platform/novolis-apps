using Novolis.Hours.Domain;
using Novolis.Hours.Infrastructure;

namespace Novolis.Hours.FeatureTests;

public sealed class NorwegianWorktimeFeatureTests
{
    [Test]
    public async Task A_long_non_overtime_day_preserves_the_presence_and_books_only_flex()
    {
        var journal = new InMemoryHoursJournal();
        var service = new HoursService(journal, NorwegianHoursPolicy.Create(year: 2026));

        var entry = await service.RegisterWorkAsync(new RegisterWorkCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            new TimeOnly(9, 30),
            new TimeOnly(21, 45),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [],
            "Late show due to traffic; stayed late for an emergency.",
            ManagerAgreementRecorded: false,
            Actor: HoursActor.Employee("ada")));

        await Assert.That(entry.ExpectedDuration).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(entry.ActualDuration).IsEqualTo(TimeSpan.FromHours(11.75));
        await Assert.That(entry.FlexDelta).IsEqualTo(TimeSpan.FromHours(4.25));
        await Assert.That(entry.FinancialCompensationDuration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(entry.LegalNotices.Select(notice => notice.RuleId)).Contains("working-day.envelope");
        await Assert.That(entry.LegalNotices.Select(notice => notice.RuleId)).Contains("ordinary.daily-limit");

        var view = await service.GetEmployeeViewAsync("ada");
        await Assert.That(view.FlexSaldo).IsEqualTo(TimeSpan.FromHours(4.25));
        await Assert.That(view.LedgerPostings.Count).IsEqualTo(2);
        await Assert.That(view.LedgerPostings.Aggregate(
            TimeSpan.Zero,
            (total, posting) => total + posting.SignedDuration)).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task Financial_compensation_is_a_presence_classification_not_a_payment_or_a_flex_account()
    {
        var service = new HoursService(new InMemoryHoursJournal(), NorwegianHoursPolicy.Create(year: 2026));

        var entry = await service.RegisterWorkAsync(new RegisterWorkCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            new TimeOnly(8, 0),
            new TimeOnly(19, 0),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [new FinancialCompensationSlice(new TimeOnly(16, 0), new TimeOnly(19, 0), "Emergency call-out")],
            "Emergency response.",
            ManagerAgreementRecorded: false,
            Actor: HoursActor.Employee("ada")));

        await Assert.That(entry.ActualDuration).IsEqualTo(TimeSpan.FromHours(10.5));
        await Assert.That(entry.FinancialCompensationDuration).IsEqualTo(TimeSpan.FromHours(3));
        await Assert.That(entry.FlexDelta).IsEqualTo(TimeSpan.Zero);
        await Assert.That(entry.LegalNotices.Select(notice => notice.RuleId))
            .Contains("financial-compensation.manager-agreement");
    }

    [Test]
    public async Task A_disputed_adjustment_escalates_without_removing_records_or_blocking_new_registration()
    {
        var service = new HoursService(new InMemoryHoursJournal(), NorwegianHoursPolicy.Create(year: 2026));
        await service.RegisterWorkAsync(OrdinaryDay());

        var adjustment = await service.ProposeAdjustmentAsync(new ProposeAdjustmentCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            TimeSpan.FromHours(-3),
            HoursAdjustmentReason.ConvertToFinancialCompensation,
            "Converting three flex hours to financial compensation.",
            HoursActor.Manager("mia")));

        var beforeEmployeeConsent = await service.GetEmployeeViewAsync("ada");
        await Assert.That(beforeEmployeeConsent.FlexSaldo).IsEqualTo(TimeSpan.Zero);
        await Assert.That(adjustment.State).IsEqualTo(HoursAdjustmentState.AwaitingEmployee);

        var dispute = await service.RespondToAdjustmentAsync(new RespondToAdjustmentCommand(
            adjustment.Id,
            HoursAdjustmentResponse.Dispute,
            "I did work these hours and need the classification reviewed.",
            HoursActor.Employee("ada")));

        await Assert.That(dispute.State).IsEqualTo(HoursAdjustmentState.Escalated);
        var afterDispute = await service.GetEmployeeViewAsync("ada");
        await Assert.That(afterDispute.Adjustments.Single().Id).IsEqualTo(adjustment.Id);
        await Assert.That(afterDispute.Anomalies.Select(anomaly => anomaly.Code)).Contains("adjustment.disputed");

        await service.RegisterWorkAsync(OrdinaryDay(day: new DateOnly(2026, 10, 2)));
        var afterNewRegistration = await service.GetEmployeeViewAsync("ada");
        await Assert.That(afterNewRegistration.Entries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task HR_can_commit_or_reject_an_escalated_adjustment_without_losing_the_dispute_audit_trail()
    {
        var service = new HoursService(new InMemoryHoursJournal(), NorwegianHoursPolicy.Create(year: 2026));
        var adjustment = await service.ProposeAdjustmentAsync(new ProposeAdjustmentCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            TimeSpan.FromHours(-3),
            HoursAdjustmentReason.ConvertToFinancialCompensation,
            "Converting three flex hours to financial compensation.",
            HoursActor.Manager("mia")));
        await service.RespondToAdjustmentAsync(new RespondToAdjustmentCommand(
            adjustment.Id,
            HoursAdjustmentResponse.Dispute,
            "Please review the classification.",
            HoursActor.Employee("ada")));

        var resolved = await service.ResolveAdjustmentAsync(new ResolveAdjustmentCommand(
            adjustment.Id,
            HoursAdjustmentResolution.Accept,
            "HR verified the manager agreement and approved the conversion.",
            HoursActor.HumanResources("hana")));

        await Assert.That(resolved.State).IsEqualTo(HoursAdjustmentState.ResolvedAccepted);
        await Assert.That(resolved.ResolvedBy).IsEqualTo(HoursActor.HumanResources("hana"));
        await Assert.That(resolved.ResolutionComment).Contains("verified");
        await Assert.That((await service.GetEmployeeViewAsync("ada")).FlexSaldo).IsEqualTo(TimeSpan.FromHours(-3));
    }

    [Test]
    public async Task Approval_deadlines_are_transparent_anomalies_instead_of_a_lockout()
    {
        var service = new HoursService(new InMemoryHoursJournal(), NorwegianHoursPolicy.Create(year: 2026));
        var period = await service.OpenApprovalPeriodAsync(new OpenApprovalPeriodCommand(
            "ada",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            HoursActor.System()));

        var checkedPeriod = await service.CheckApprovalDeadlinesAsync(
            period.Id,
            new DateOnly(2026, 10, 12),
            HoursActor.System());

        await Assert.That(checkedPeriod.State).IsEqualTo(HoursApprovalState.Registered);
        await Assert.That(checkedPeriod.Anomalies.Select(anomaly => anomaly.Code)).Contains("approval.employee-submit-overdue");

        await service.RegisterWorkAsync(OrdinaryDay(day: new DateOnly(2026, 10, 12)));
        var view = await service.GetEmployeeViewAsync("ada");
        await Assert.That(view.Entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Quarterly_positive_flex_normalization_is_proposed_and_never_means_payment()
    {
        var service = new HoursService(new InMemoryHoursJournal(), NorwegianHoursPolicy.Create(year: 2026));
        foreach (var day in new[]
                 {
                     new DateOnly(2026, 10, 1),
                     new DateOnly(2026, 10, 2),
                     new DateOnly(2026, 10, 5),
                     new DateOnly(2026, 10, 6),
                     new DateOnly(2026, 10, 7),
                     new DateOnly(2026, 10, 8),
                 })
        {
            await service.RegisterWorkAsync(LongDay(day));
        }

        var proposal = await service.ProposeFlexNormalizationAsync(
            "ada",
            new DateOnly(2026, 10, 8),
            HoursActor.Manager("mia"));

        await Assert.That(proposal).IsNotNull();
        await Assert.That(proposal!.State).IsEqualTo(HoursAdjustmentState.AwaitingEmployee);
        await Assert.That(proposal.Reason).IsEqualTo(HoursAdjustmentReason.FlexNormalization);
        await Assert.That(proposal.Comment).Contains("does not imply payment");
        await Assert.That((await service.GetEmployeeViewAsync("ada")).FlexSaldo).IsEqualTo(TimeSpan.FromHours(48));

        await service.RespondToAdjustmentAsync(new RespondToAdjustmentCommand(
            proposal.Id,
            HoursAdjustmentResponse.Accept,
            "I acknowledge the configured carry cap.",
            HoursActor.Employee("ada")));

        var committed = await service.GetEmployeeViewAsync("ada");
        await Assert.That(committed.FlexSaldo).IsEqualTo(TimeSpan.FromHours(40));
        await Assert.That(committed.LedgerPostings.Aggregate(
            TimeSpan.Zero,
            (total, posting) => total + posting.SignedDuration)).IsEqualTo(TimeSpan.Zero);
    }

    static RegisterWorkCommand OrdinaryDay(DateOnly? day = null) =>
        new(
            "ada",
            day ?? new DateOnly(2026, 10, 1),
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [],
            "Normal working day.",
            ManagerAgreementRecorded: false,
            Actor: HoursActor.Employee("ada"));

    static RegisterWorkCommand LongDay(DateOnly day) =>
        new(
            "ada",
            day,
            new TimeOnly(7, 0),
            new TimeOnly(23, 0),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [],
            "Long approved day for settlement scenario.",
            ManagerAgreementRecorded: false,
            Actor: HoursActor.Employee("ada"));
}
