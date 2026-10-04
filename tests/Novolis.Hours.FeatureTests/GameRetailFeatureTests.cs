using System.Net;
using Novolis.Hours.Client;
using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.FeatureTests;

public sealed class GameRetailFeatureTests
{
    [Test]
    public async Task Shop_hours_are_monday_to_saturday_in_london_with_sunday_and_boxing_day_closed()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var jamie = await fixture.ConnectAsync("jamie");

        var monday = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 3, 9));
        var saturday = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 3, 14));
        var sunday = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 3, 15));
        var christmas = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 12, 25));
        var boxingObserved = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 12, 28));
        var week = await jamie.GetWorkDaysAsync("jamie", new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15));

        await Assert.That(monday.OrganisationId).IsEqualTo("game-retail");
        await Assert.That(monday.OrganisationName).IsEqualTo("Game");
        await Assert.That(monday.Configuration.TimeZoneId).IsEqualTo("Europe/London");
        await Assert.That(monday.IsWorkingDay).IsTrue();
        await Assert.That(monday.ExpectedWork).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(monday.WorkEnvelope).IsEqualTo("09:00–18:00");
        await Assert.That(monday.AllowsFlex).IsFalse();
        await Assert.That(monday.AllowsDispute).IsFalse();
        await Assert.That(monday.AttendanceConfirmationOnly).IsTrue();
        await Assert.That(saturday.IsWorkingDay).IsTrue();
        await Assert.That(saturday.ExpectedWork).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(sunday.IsWorkingDay).IsFalse();
        await Assert.That(sunday.ExpectedWork).IsEqualTo(TimeSpan.Zero);
        await Assert.That(christmas.IsWorkingDay).IsFalse();
        await Assert.That(christmas.Tags).Contains(tag => tag.Contains("PublicHoliday", StringComparison.Ordinal));
        await Assert.That(boxingObserved.IsWorkingDay).IsFalse();
        await Assert.That(boxingObserved.Tags).Contains(tag =>
            tag.Contains("Boxing", StringComparison.OrdinalIgnoreCase));
        var weekExpected = TimeSpan.Zero;
        foreach (var day in week)
        {
            weekExpected += day.ExpectedWork;
        }

        await Assert.That(weekExpected).IsEqualTo(TimeSpan.FromHours(48));
    }

    [Test]
    public async Task Employee_confirms_attendance_without_a_flex_ledger_or_dispute()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var jamie = await fixture.ConnectAsync("jamie");
        using var priya = await fixture.ConnectAsync("priya");
        using var alice = await fixture.ConnectAsync("alice");

        await jamie.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "jamie",
            new DateOnly(2026, 3, 9),
            WorkRegistrationIntent.WorkedAsScheduled,
            [],
            null,
            "I confirm I attended the contracted shop hours."));
        var day = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 3, 9));
        var periodId = await jamie.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "jamie",
            new DateOnly(2026, 3, 9),
            new DateOnly(2026, 3, 15)));
        var opened = await jamie.GetReviewAsync(periodId);
        var submitForbidden = await CaptureForbiddenAsync(() =>
            jamie.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, "Timesheet.", null)));
        var disputeForbidden = await CaptureForbiddenAsync(() =>
            jamie.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Dispute", null, "I disagree.", null)));
        await jamie.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Acknowledge", null, "I was here.", opened.JournalHead));
        var afterConfirm = await jamie.GetReviewAsync(periodId);
        await priya.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Approve", null, "HR attendance close.", afterConfirm.JournalHead));
        var closed = await jamie.GetReviewAsync(periodId);
        var aliceDay = await CaptureForbiddenAsync(() => alice.GetWorkDayAsync("jamie", new DateOnly(2026, 3, 9)));
        var aliceReview = await CaptureForbiddenAsync(() => alice.GetReviewAsync(periodId));
        var studio = new DayStudioModel(day);
        var week = new WeekStudioModel(
            await jamie.GetWorkDaysAsync("jamie", new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15)),
            new DateOnly(2026, 3, 9));
        var reviewUi = new ReviewMonthModel(closed, [day]);

        await Assert.That(day.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(day.LedgerTransactions.SelectMany(row => row.Postings)).DoesNotContain(posting =>
            posting.Account.Contains("flex", StringComparison.OrdinalIgnoreCase));
        await Assert.That(day.ComplianceIndicators).DoesNotContain(row =>
            row.Code.Contains("weekend", StringComparison.OrdinalIgnoreCase));
        await Assert.That(opened.PolicyId).IsEqualTo("attendance-hr");
        await Assert.That(opened.AllowsDispute).IsFalse();
        await Assert.That(opened.AttendanceConfirmationOnly).IsTrue();
        await Assert.That(opened.Stages.Select(stage => stage.Id))
            .IsEquivalentTo(["employee-confirm", "hr-attendance"]);
        await Assert.That(submitForbidden).IsTrue();
        await Assert.That(disputeForbidden).IsTrue();
        await Assert.That(closed.State).IsEqualTo("Approved");
        await Assert.That(closed.Stages.All(stage => stage.IsComplete)).IsTrue();
        await Assert.That(aliceDay).IsTrue();
        await Assert.That(aliceReview).IsTrue();
        await Assert.That(studio.CommitLabel).IsEqualTo("I was here");
        await Assert.That(studio.AllowsPaint).IsFalse();
        await Assert.That(studio.AllowsFlex).IsFalse();
        await Assert.That(studio.WorkplaceName).IsEqualTo("Game");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 9)).Chip)
            .IsEqualTo("Here");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 14)).Chip)
            .IsEqualTo("Confirm");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 3, 15)).Chip)
            .IsEqualTo("Weekend");
        await Assert.That(reviewUi.EmployeePrimaryLabel).IsEqualTo("Confirm attendance");
        await Assert.That(reviewUi.AllowsDispute).IsFalse();
        await Assert.That(reviewUi.HasSubmitStage).IsFalse();
        await Assert.That(reviewUi.HasApproveStage).IsTrue();
    }

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
