using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class PresenceReplayTests
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    static readonly Guid HomeId = Guid.Parse("a2f98b1b-795e-49c5-87cb-c22366325958");
    static readonly Guid WorkId = Guid.Parse("0c2cdf46-fac0-4107-b3bb-f8c21c3fac46");
    static readonly Guid BarrikadenId = Guid.Parse("d7cb3f5c-95e3-4df6-9b5b-2b7bca3f0d87");

    [Test]
    public async Task Phone_day_applies_home_and_work_to_samples_recorded_before_the_places_mattered()
    {
        var replay = PresenceReplay.Replay(
            [Home(), Work()],
            ReadPhoneDay());
        var home = replay.Events.Where(item => item.LocationId == HomeId).ToArray();
        var work = replay.Events.Where(item => item.LocationId == WorkId).ToArray();

        await Assert.That(home.Count(item => item.Transition == PresenceTransition.Arrived))
            .IsGreaterThanOrEqualTo(1);
        await Assert.That(home[0].Transition).IsEqualTo(PresenceTransition.Arrived);
        await Assert.That(home[0].At)
            .IsLessThan(new DateTimeOffset(2026, 10, 2, 5, 15, 0, TimeSpan.Zero));
        await Assert.That(home.Any(item =>
                item.Transition == PresenceTransition.Left
                && item.At < new DateTimeOffset(2026, 10, 2, 7, 10, 0, TimeSpan.Zero)))
            .IsTrue();
        await Assert.That(home.Any(item =>
                item.Transition == PresenceTransition.Arrived
                && item.At > new DateTimeOffset(2026, 10, 2, 16, 0, 0, TimeSpan.Zero)))
            .IsTrue();

        await Assert.That(work).Count().IsEqualTo(2);
        await Assert.That(work[0].Transition).IsEqualTo(PresenceTransition.Arrived);
        await Assert.That(work[0].At)
            .IsGreaterThan(new DateTimeOffset(2026, 10, 2, 6, 50, 0, TimeSpan.Zero))
            .And.IsLessThan(new DateTimeOffset(2026, 10, 2, 7, 10, 0, TimeSpan.Zero));
        await Assert.That(work[1].Transition).IsEqualTo(PresenceTransition.Left);
        await Assert.That(work[1].At)
            .IsGreaterThan(new DateTimeOffset(2026, 10, 2, 15, 25, 0, TimeSpan.Zero))
            .And.IsLessThan(new DateTimeOffset(2026, 10, 2, 16, 30, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task Phone_day_pulls_an_indoor_fix_back_onto_the_home_network()
    {
        var anchored = SsidPositionAnchor.Apply([Home(), Work()], ReadPhoneDay());
        var spike = anchored.Single(item =>
            item.At >= new DateTimeOffset(2026, 10, 2, 16, 52, 0, TimeSpan.Zero)
            && item.At < new DateTimeOffset(2026, 10, 2, 16, 53, 0, TimeSpan.Zero)
            && item.Position is not null);
        var commute = anchored.Single(item =>
            item.At >= new DateTimeOffset(2026, 10, 2, 6, 40, 0, TimeSpan.Zero)
            && item.At < new DateTimeOffset(2026, 10, 2, 6, 41, 0, TimeSpan.Zero)
            && item.Position is not null);

        await Assert.That(spike.Position).IsEqualTo(Home().Area.Center);
        await Assert.That(commute.Position!.Value.Longitude).IsGreaterThan(7.97);
        await Assert.That(commute.Position.Value.Longitude).IsLessThan(7.98);
    }

    [Test]
    public async Task A_fix_stays_when_the_same_network_returns_after_the_evidence_gap()
    {
        var start = new DateTimeOffset(2026, 10, 2, 16, 0, 0, TimeSpan.Zero);
        var away = new GeoCoordinate(58.16, 7.97);
        var ssid = Home().Wifi!.Ssid;
        var samples = new[]
        {
            new PresenceObservationRecord(start, null, null, RecordedWifiStatus.Available, ssid),
            new PresenceObservationRecord(start.AddMinutes(3), away, 8, RecordedWifiStatus.Unavailable, null),
            new PresenceObservationRecord(start.AddMinutes(6), null, null, RecordedWifiStatus.Available, ssid),
        };

        var anchored = SsidPositionAnchor.Apply([Home()], samples);

        await Assert.That(anchored[1].Position).IsEqualTo(away);
    }

    [Test]
    public async Task Moving_a_place_reapplies_it_to_earlier_fixes()
    {
        var start = new DateTimeOffset(2026, 10, 2, 7, 0, 0, TimeSpan.Zero);
        var movedCenter = new GeoCoordinate(58.143788, 7.992383);
        var samples = Enumerable.Range(0, 8)
            .Select(minute => new PresenceObservationRecord(
                start.AddMinutes(minute),
                movedCenter,
                5,
                RecordedWifiStatus.Unavailable,
                null))
            .ToArray();

        var before = PresenceReplay.Replay(
            [Place(new GeoCoordinate(58.144248, 7.99078))],
            samples);
        var after = PresenceReplay.Replay([Place(movedCenter)], samples);

        await Assert.That(before.Events).IsEmpty();
        await Assert.That(after.Events.Single().Transition)
            .IsEqualTo(PresenceTransition.Arrived);
        await Assert.That(after.Events.Single().At).IsEqualTo(start);
    }

    [Test]
    public async Task Sparse_gps_fixes_confirm_a_legacy_location_only_place()
    {
        var locationId = Guid.Parse("923ad1ea-4522-43ea-a4c1-387c0a1dbc52");
        var location = new TrackedLocation(
            locationId,
            "third place",
            new GeoCircle(new GeoCoordinate(58.147712, 8.001682), 50),
            null,
            new PresencePolicy(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(3),
                TimeSpan.FromMinutes(2)));
        var start = new DateTimeOffset(2026, 10, 8, 16, 41, 0, TimeSpan.Zero);
        var center = location.Area.Center;
        var records = new[]
        {
            new PresenceObservationRecord(
                start,
                center,
                15,
                RecordedWifiStatus.Redacted,
                null),
            new PresenceObservationRecord(
                start.AddMinutes(4),
                center,
                15,
                RecordedWifiStatus.Redacted,
                null),
            new PresenceObservationRecord(
                start.AddMinutes(8),
                center,
                100,
                RecordedWifiStatus.Redacted,
                null),
            new PresenceObservationRecord(
                start.AddMinutes(12),
                new GeoCoordinate(58.16, 8.04),
                10,
                RecordedWifiStatus.Redacted,
                null),
            new PresenceObservationRecord(
                start.AddMinutes(16),
                new GeoCoordinate(58.16, 8.04),
                10,
                RecordedWifiStatus.Redacted,
                null),
        };

        var replay = PresenceReplay.Replay([location], records);

        await Assert.That(replay.Events).Count().IsEqualTo(2);
        await Assert.That(replay.Events[0].Transition).IsEqualTo(PresenceTransition.Arrived);
        await Assert.That(replay.Events[0].At).IsEqualTo(start);
        await Assert.That(replay.Events[1].Transition).IsEqualTo(PresenceTransition.Left);
        await Assert.That(replay.Events[1].At).IsEqualTo(start.AddMinutes(12));
    }

    [Test]
    public async Task Confirming_Barrikaden_closes_an_existing_work_presence()
    {
        var start = new DateTimeOffset(2026, 10, 8, 16, 0, 0, TimeSpan.Zero);
        var workCenter = new GeoCoordinate(58.143788, 7.992383);
        var barrikadenCenter = new GeoCoordinate(58.147712, 8.001682);
        var work = new TrackedLocation(
            WorkId,
            "Work",
            new GeoCircle(workCenter, 80),
            null,
            new PresencePolicy(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(20),
                TimeSpan.FromMinutes(10)));
        var barrikaden = new TrackedLocation(
            BarrikadenId,
            "Barrikaden",
            new GeoCircle(barrikadenCenter, 80),
            new WifiEvidence("Barrikaden"),
            new PresencePolicy(
                TimeSpan.FromMinutes(3),
                TimeSpan.FromMinutes(3),
                TimeSpan.FromMinutes(5)));
        var observations = new[]
        {
            new PresenceObservationRecord(start, workCenter, 5, RecordedWifiStatus.Redacted, null),
            new PresenceObservationRecord(
                start.AddMinutes(5),
                workCenter,
                5,
                RecordedWifiStatus.Redacted,
                null),
            new PresenceObservationRecord(
                start.AddMinutes(10),
                null,
                null,
                RecordedWifiStatus.Available,
                "Barrikaden"),
            new PresenceObservationRecord(
                start.AddMinutes(13),
                null,
                null,
                RecordedWifiStatus.Available,
                "Barrikaden"),
        };

        var replay = PresenceReplay.Replay([work, barrikaden], observations);
        var workEvents = replay.Events.Where(item => item.LocationId == WorkId).ToArray();
        var barrikadenEvents = replay.Events
            .Where(item => item.LocationId == BarrikadenId)
            .ToArray();
        var workLeft = workEvents.Single(
            item => item.Transition == PresenceTransition.Left);
        var barrikadenArrival = barrikadenEvents.Single(
            item => item.Transition == PresenceTransition.Arrived);

        await Assert.That(workEvents.Count(item => item.Transition == PresenceTransition.Arrived))
            .IsEqualTo(1);
        await Assert.That(workLeft.At).IsEqualTo(barrikadenArrival.At);
        await Assert.That(replay.States.Single(item => item.LocationId == WorkId).State)
            .IsEqualTo(PresenceState.Absent);
        await Assert.That(
                replay.States.Count(item => item.State == PresenceState.Present))
            .IsEqualTo(1);
        await Assert.That(
                replay.States.Single(item => item.LocationId == BarrikadenId).State)
            .IsEqualTo(PresenceState.Present);
    }

    static TrackedLocation Home() =>
        new(
            HomeId,
            "home",
            new GeoCircle(new GeoCoordinate(58.147214, 7.951295), 50),
            new WifiEvidence("DickeTitten"),
            PresencePolicyDefaults.Standard);

    static TrackedLocation Work() =>
        new(
            WorkId,
            "work",
            new GeoCircle(new GeoCoordinate(58.143788, 7.992383), 69.0673828125),
            new WifiEvidence("Semine"),
            PresencePolicyDefaults.Standard);

    static TrackedLocation Place(GeoCoordinate center) =>
        new(
            WorkId,
            "work",
            new GeoCircle(center, 69.0673828125),
            null,
            PresencePolicyDefaults.LocationOnly);

    static IReadOnlyList<PresenceObservationRecord> ReadPhoneDay()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "2026-10-02-phone.ndjson");
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<PresenceObservationRecord>(line, Json)
                ?? throw new InvalidOperationException("Phone sample did not deserialize."))
            .ToArray();
    }
}
