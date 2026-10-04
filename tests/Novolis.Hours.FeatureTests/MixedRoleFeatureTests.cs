using System.Net;
using Novolis.Hours.Client;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.FeatureTests;

public sealed class MixedRoleFeatureTests
{
    [Test]
    public async Task Seeded_people_keep_the_roles_they_actually_hold()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var helen = await fixture.ConnectAsync("helen");
        using var alice = await fixture.ConnectAsync("alice");
        using var nina = await fixture.ConnectAsync("nina");
        using var marc = await fixture.ConnectAsync("marc");
        using var pat = await fixture.ConnectAsync("pat");
        using var aino = await fixture.ConnectAsync("aino");

        await Assert.That((await helen.GetCurrentUserAsync()).AssignedRoles)
            .IsEquivalentTo([HoursClientRole.HumanResources, HoursClientRole.Administrator]);
        await Assert.That((await alice.GetCurrentUserAsync()).AssignedRoles)
            .IsEquivalentTo([HoursClientRole.Manager, HoursClientRole.Employee]);
        await Assert.That((await nina.GetCurrentUserAsync()).AssignedRoles)
            .IsEquivalentTo([
                HoursClientRole.Manager,
                HoursClientRole.Employee,
                HoursClientRole.Administrator]);
        await Assert.That((await marc.GetCurrentUserAsync()).AssignedRoles)
            .IsEquivalentTo([
                HoursClientRole.Manager,
                HoursClientRole.Employee,
                HoursClientRole.Administrator]);
        await Assert.That((await pat.GetCurrentUserAsync()).AssignedRoles)
            .IsEquivalentTo([
                HoursClientRole.Manager,
                HoursClientRole.Employee,
                HoursClientRole.Administrator]);
        await Assert.That((await aino.GetCurrentUserAsync()).AssignedRoles)
            .IsEquivalentTo([HoursClientRole.HumanResources, HoursClientRole.Administrator]);
    }

    [Test]
    public async Task Mixed_admin_stays_inside_the_customer()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var helen = await fixture.ConnectAsync("helen");
        using var marc = await fixture.ConnectAsync("marc");

        await Assert.That(await CaptureForbiddenAsync(() =>
            helen.PublishConfigurationAsync(
                "pierre",
                new PublishConfigurationRequest(new DateOnly(2026, 5, 15))))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            helen.GetWorkDayAsync("pierre", new DateOnly(2026, 5, 1)))).IsTrue();
        await Assert.That(await CaptureForbiddenAsync(() =>
            marc.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1)))).IsTrue();

        var published = await marc.PublishConfigurationAsync(
            "pierre",
            new PublishConfigurationRequest(new DateOnly(2026, 5, 15)));
        await Assert.That(published.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task Working_manager_records_own_hours_and_still_approves()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var alice = await fixture.ConnectAsync("alice");
        using var ada = await fixture.ConnectAsync("ada");

        var own = await alice.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
            "alice",
            new DateOnly(2026, 10, 5),
            WorkRegistrationIntent.WorkedAsScheduled,
            [],
            null,
            "Working manager Monday."));
        await Assert.That(own.RecordedBy).IsEqualTo("alice");
        await Assert.That(own.Source).IsEqualTo(WorkRecordSource.Employee.ToString());

        var periodId = await ada.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(
            "ada",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31)));
        await ada.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Submit", null, null, null));
        await alice.RecordReviewActionAsync(periodId, new RecordReviewActionRequest("Approve", 1, null, null));
        var review = await alice.GetReviewAsync(periodId);
        await Assert.That(review.Actions).Contains(action =>
            action.Kind == "Approve" && action.ActorId == "alice");
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
