using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.FeatureTests;

public sealed class NordvikOfficeUxFeatureTests
{
    [Test]
    public async Task Norwegian_office_keeps_flex_paint_and_a_manager_ladder_with_dispute()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        using var alice = await fixture.ConnectAsync("alice");

        var monday = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1));
        var saturday = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 3));
        await ada.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            WorkRegistrationIntent.WorkedAsScheduled,
            [],
            null,
            "Worked as scheduled."));
        var recorded = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1));
        var periodId = await ada.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        await ada.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Submit", null, "October submitted.", null));
        var review = await ada.GetReviewAsync(periodId);
        await ada.RecordReviewActionAsync(
            periodId,
            new RecordReviewActionRequest("Dispute", null, "Core hours were covered.", review.JournalHead));
        var disputed = await ada.GetReviewAsync(periodId);
        var studio = new DayStudioModel(recorded);
        var week = new WeekStudioModel(
            await ada.GetWorkDaysAsync("ada", new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4)),
            new DateOnly(2026, 10, 1));
        var reviewUi = new ReviewMonthModel(disputed, [recorded]);

        await Assert.That(monday.OrganisationId).IsEqualTo("nordvik-office");
        await Assert.That(monday.OrganisationName).IsEqualTo("Nordvik");
        await Assert.That(monday.Configuration.TimeZoneId).IsEqualTo("Europe/Oslo");
        await Assert.That(monday.AllowsFlex).IsTrue();
        await Assert.That(monday.AllowsDispute).IsTrue();
        await Assert.That(monday.AttendanceConfirmationOnly).IsFalse();
        await Assert.That(monday.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(saturday.IsWorkingDay).IsFalse();
        await Assert.That(recorded.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(review.PolicyId).IsEqualTo("cascading-approval");
        await Assert.That(review.AllowsDispute).IsTrue();
        await Assert.That(review.AttendanceConfirmationOnly).IsFalse();
        await Assert.That(review.Stages.Select(stage => stage.Id))
            .IsEquivalentTo(["employee-submit", "manager-level-1", "manager-level-2", "hr-final"]);
        await Assert.That(disputed.Actions.Any(action => action.Kind == "Dispute")).IsTrue();
        await Assert.That(studio.CommitLabel).IsEqualTo("I worked as planned");
        await Assert.That(studio.AllowsPaint).IsTrue();
        await Assert.That(studio.AllowsFlex).IsTrue();
        await Assert.That(studio.WorkplaceName).IsEqualTo("Nordvik");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 1)).Chip)
            .IsEqualTo("As planned");
        await Assert.That(week.Tiles.Single(tile => tile.Date == new DateOnly(2026, 10, 3)).Chip)
            .IsEqualTo("Closed");
        await Assert.That(reviewUi.EmployeePrimaryLabel).IsEqualTo("Hand month to HR");
        await Assert.That(reviewUi.AllowsDispute).IsTrue();
        await Assert.That(reviewUi.HasSubmitStage).IsTrue();
        var managerDay = await alice.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1));
        await Assert.That(managerDay.OrganisationName).IsEqualTo("Nordvik");
    }
}
