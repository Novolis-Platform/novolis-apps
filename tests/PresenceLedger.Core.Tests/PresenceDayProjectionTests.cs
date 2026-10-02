using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class PresenceDayProjectionTests
{
    static readonly Guid HomeId = Guid.Parse("0fca6e9c-e87b-4660-a10c-b6e33b51b37a");
    static readonly Guid WorkId = Guid.Parse("5ad8d6e7-63f1-4d22-a5e4-0a972702b4d1");
    static readonly DateTimeOffset Day = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Projects_a_home_work_home_day_with_an_open_final_interval()
    {
        var home = Location(HomeId, "Home");
        var work = Location(WorkId, "Work");
        var events = new[]
        {
            Event(HomeId, PresenceTransition.Arrived, 0),
            Event(HomeId, PresenceTransition.Left, 20),
            Event(WorkId, PresenceTransition.Arrived, 40),
            Event(WorkId, PresenceTransition.Left, 16 * 60),
            Event(HomeId, PresenceTransition.Arrived, 16 * 60 + 20),
        };

        var result = new PresenceDayProjector().Project(
            new DateOnly(2026, 9, 29),
            TimeZoneInfo.Utc,
            events,
            [],
            [home, work]);

        await Assert.That(result.Intervals).Count().IsEqualTo(3);
        await Assert.That(result.Intervals[0].DisplayName).IsEqualTo("Home");
        await Assert.That(result.Intervals[1].DisplayName).IsEqualTo("Work");
        await Assert.That(result.Intervals[2].DisplayName).IsEqualTo("Home");
        await Assert.That(result.Intervals[2].IsOpen).IsTrue();
        await Assert.That(result.OpenIntervalCount).IsEqualTo(1);
    }

    [Test]
    public async Task Uses_the_latest_location_revision_for_the_whole_day()
    {
        var oldRevision = Location(HomeId, "Old Home");
        var newRevision = Location(HomeId, "New Home");

        var result = new PresenceDayProjector().Project(
            new DateOnly(2026, 9, 29),
            TimeZoneInfo.Utc,
            [
                Event(HomeId, PresenceTransition.Arrived, 1),
                Event(HomeId, PresenceTransition.Left, 30),
                Event(HomeId, PresenceTransition.Arrived, 13 * 60),
            ],
            [],
            [oldRevision, newRevision]);

        await Assert.That(result.Intervals[0].DisplayName).IsEqualTo("New Home");
        await Assert.That(result.Intervals[1].DisplayName).IsEqualTo("New Home");
    }

    [Test]
    public async Task Clips_samples_to_the_selected_local_day()
    {
        var result = new PresenceDayProjector().Project(
            new DateOnly(2026, 9, 29),
            TimeZoneInfo.Utc,
            [],
            [
                new PresenceObservationRecord(
                    Day.AddMinutes(-1),
                    new GeoCoordinate(58, 8),
                    5,
                    RecordedWifiStatus.Available,
                    "Home"),
                new PresenceObservationRecord(
                    Day.AddMinutes(1),
                    new GeoCoordinate(58, 8),
                    5,
                    RecordedWifiStatus.Available,
                    "Home"),
            ],
            []);

        await Assert.That(result.Observations).Count().IsEqualTo(1);
        await Assert.That(result.Observations[0].ConnectedSsid).IsEqualTo("Home");
    }

    static TrackedLocation Location(Guid id, string name) =>
        new(
            id,
            name,
            new GeoCircle(new GeoCoordinate(58.14623, 7.99517), 50),
            null,
            PresencePolicyDefaults.LocationOnly);

    static PresenceEvent Event(
        Guid locationId,
        PresenceTransition transition,
        int minute) =>
        new(
            Guid.NewGuid(),
            locationId,
            transition,
            Day.AddMinutes(minute),
            new PresenceEvidence(
                PresenceConfidence.Confirmed,
                TimeSpan.FromMinutes(3)));
}
