using System.Net;
using Novolis.Hours.Client;
using Novolis.Hours.Contracts;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.FeatureTests;

public sealed class WorkflowDivergenceFeatureTests
{
    [Test]
    public async Task Single_approver_closes_after_one_manager_and_stays_inside_the_atelier()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var pierre = await fixture.ConnectAsync("pierre");
        using var marc = await fixture.ConnectAsync("marc");
        using var alice = await fixture.ConnectAsync("alice");

        var periodId = await pierre.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "pierre",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31)));
        await pierre.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, "May submitted.", null));
        await marc.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "Single close.", null));
        var review = await pierre.GetReviewAsync(periodId);
        var foreign = await CaptureForbiddenAsync(() => alice.GetReviewAsync(periodId));

        await Assert.That(review.PolicyId).IsEqualTo("single-approver");
        await Assert.That(review.State).IsEqualTo("Approved");
        await Assert.That(review.Stages.Select(stage => stage.Id)).IsEquivalentTo(["employee-submit", "manager-approve"]);
        await Assert.That(review.Stages.All(stage => stage.IsComplete)).IsTrue();
        await Assert.That(foreign).IsTrue();
    }

    [Test]
    public async Task Employer_only_closes_when_the_employer_approves_without_employee_submit()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var anna = await fixture.ConnectAsync("anna");
        using var kasia = await fixture.ConnectAsync("kasia");

        await kasia.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "anna",
            new DateOnly(2026, 5, 4),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 5, 4, 8, 0, 16, 0)],
            null,
            "Employer recorded the day."));
        var periodId = await kasia.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "anna",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31)));
        await kasia.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "Employer close.", null));
        var review = await anna.GetReviewAsync(periodId);

        await Assert.That(review.PolicyId).IsEqualTo("employer-only");
        await Assert.That(review.State).IsEqualTo("Approved");
        await Assert.That(review.Stages.Single().Id).IsEqualTo("employer-approve");
        await Assert.That(review.Actions.Any(action => action.Kind == "Submit")).IsFalse();
    }

    [Test]
    public async Task Employee_hr_closes_without_a_manager_ladder()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var liisa = await fixture.ConnectAsync("liisa");
        using var helen = await fixture.ConnectAsync("helen");

        var periodId = await liisa.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "liisa",
            new DateOnly(2026, 12, 1),
            new DateOnly(2026, 12, 31)));
        await liisa.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, "December submitted.", null));
        await helen.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "HR close.", null));
        var review = await liisa.GetReviewAsync(periodId);

        await Assert.That(review.PolicyId).IsEqualTo("employee-hr");
        await Assert.That(review.State).IsEqualTo("Approved");
        await Assert.That(review.Stages.Select(stage => stage.Id)).IsEquivalentTo(["employee-submit", "hr-close"]);
        await Assert.That(review.Stages.All(stage => stage.IsComplete)).IsTrue();
    }

    [Test]
    public async Task System_reads_and_stamps_across_customers()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var system = await fixture.ConnectAsync("system");
        using var pierre = await fixture.ConnectAsync("pierre");

        var ada = await system.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1));
        var anna = await system.GetWorkDayAsync("anna", new DateOnly(2026, 5, 3));
        var liisa = await system.GetWorkDayAsync("liisa", new DateOnly(2026, 12, 6));
        var stamp = await system.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "pierre",
            new DateOnly(2026, 5, 11),
            WorkRegistrationIntent.ManualRegistration,
            [Interval(2026, 5, 11, 9, 0, 17, 0)],
            null,
            "Platform stamp.",
            WorkRecordSource.Integration));
        var health = await system.GetHealthReportAsync();
        var stamped = await pierre.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 11));
        var periodId = await pierre.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "pierre",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31)));
        await pierre.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, "May submitted.", null));
        var systemApprove = await CaptureForbiddenAsync(() =>
            system.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", null, "Platform must not close.", null)));

        await Assert.That(ada.OrganisationId).IsEqualTo("nordvik-office");
        await Assert.That(anna.OrganisationId).IsEqualTo("warsaw-settlement");
        await Assert.That(liisa.OrganisationId).IsEqualTo("helsinki-flex");
        await Assert.That(stamp.Source).IsEqualTo(WorkRecordSource.Integration.ToString());
        await Assert.That(stamped.Registration!.RecordedBy).IsEqualTo("system");
        await Assert.That(health).IsNotNull();
        await Assert.That(systemApprove).IsTrue();
    }

    private static WorkIntervalRequest Interval(
        int year,
        int month,
        int day,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute) =>
        new(
            new DateTimeOffset(year, month, day, startHour, startMinute, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(year, month, day, endHour, endMinute, 0, TimeSpan.FromHours(2)));

    private static async Task<bool> CaptureForbiddenAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return false;
        }
        catch (HttpRequestException exception)
        {
            return exception.StatusCode == HttpStatusCode.Forbidden;
        }
    }
}
