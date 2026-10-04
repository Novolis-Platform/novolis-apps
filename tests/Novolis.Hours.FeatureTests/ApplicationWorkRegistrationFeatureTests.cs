using Novolis.Hours.Application;
using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;
using Novolis.Hours.Storage;

namespace Novolis.Hours.FeatureTests;

public sealed class ApplicationWorkRegistrationFeatureTests
{
    [Test]
    public async Task Application_service_appends_the_new_registration_fact_without_deriving_policy()
    {
        var journal = new InMemoryHoursJournal();
        var service = new WorkRegistrationService(
            journal,
            new FrozenTimeProvider(new DateTimeOffset(2026, 10, 1, 16, 0, 0, TimeSpan.Zero)));

        var registration = await service.RecordAsync(
            new RecordWorkRegistrationCommand(
                new WorkDayKey("ada", new DateOnly(2026, 10, 1)),
                WorkRecordSource.Employee,
                WorkRecordIntent.ManualRegistration,
                [
                    new WorkInterval(
                        new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
                ],
                null,
                null,
                ActorRef.Employee("ada"),
                ConfigurationSnapshotId.New()));

        var events = await journal.ReadEmployeeAsync("ada");
        await Assert.That(events).HasSingleItem();
        await Assert.That(events[0].Type).IsEqualTo(HoursEventType.WorkRegistrationRecorded);
        await Assert.That(events[0].Actor.Id).IsEqualTo("ada");
        var replayed = events[0].ReadPayload<WorkRegistration>();
        await Assert.That(replayed.Id).IsEqualTo(registration.Id);
        await Assert.That(replayed.WorkDay).IsEqualTo(registration.WorkDay);
        await Assert.That(replayed.Source).IsEqualTo(registration.Source);
        await Assert.That(replayed.Intent).IsEqualTo(registration.Intent);
        await Assert.That(replayed.Intervals.Length).IsEqualTo(registration.Intervals.Length);
        await Assert.That(replayed.Intervals[0]).IsEqualTo(registration.Intervals[0]);
        await Assert.That(replayed.RecordedBy).IsEqualTo(registration.RecordedBy);
        await Assert.That(replayed.ConfigurationSnapshotId).IsEqualTo(registration.ConfigurationSnapshotId);
    }
}
