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
