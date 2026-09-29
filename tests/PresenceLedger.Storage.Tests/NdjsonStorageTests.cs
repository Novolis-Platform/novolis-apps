using Novolis.Math.Geometry;
using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.Storage.Tests;

public sealed class NdjsonStorageTests
{
    [Test]
    public async Task Round_trip_preserves_locations_events_and_state()
    {
        var root = CreateRoot();
        try
        {
            var storage = new NdjsonPresenceStorage(root);
            var location = Location();
            var at = new DateTimeOffset(2026, 9, 28, 8, 4, 0, TimeSpan.FromHours(2));
            var presenceEvent = new PresenceEvent(
                Guid.Parse("2f41b5dd-57d1-45b6-9679-9dceecf1811c"),
                location.Id,
                PresenceTransition.Arrived,
                at,
                new PresenceEvidence(
                    PresenceConfidence.Confirmed,
                    TimeSpan.FromMinutes(3)));
            var state = new LocationPresenceState(
                location.Id,
                PresenceState.Present,
                null,
                null,
                at);

            await storage.Locations.SaveAsync(location);
            await storage.Events.AppendAsync(presenceEvent);
            await storage.States.SaveAsync(state);

            var locations = await ReadAll(storage.Locations.ReadAsync());
            var events = await ReadAll(storage.Events.ReadAsync());
            var restoredState = await storage.States.GetAsync(location.Id);

            await Assert.That(locations).Count().IsEqualTo(1);
            await Assert.That(locations[0]).IsEqualTo(location);
            await Assert.That(events).Count().IsEqualTo(1);
            await Assert.That(events[0]).IsEqualTo(presenceEvent);
            await Assert.That(restoredState).IsEqualTo(state);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public async Task Location_and_state_snapshots_are_last_write_wins()
    {
        var root = CreateRoot();
        try
        {
            var storage = new NdjsonPresenceStorage(root);
            var original = Location();
            var replacement = new TrackedLocation(
                original.Id,
                "Renamed Office",
                original.Area,
                original.Wifi,
                original.Policy);
            await storage.Locations.SaveAsync(original);
            await storage.Locations.SaveAsync(replacement);
            await storage.States.SaveAsync(LocationPresenceState.CreateAbsent(original.Id));
            var present = new LocationPresenceState(
                original.Id,
                PresenceState.Present,
                null,
                null,
                DateTimeOffset.UtcNow);
            await storage.States.SaveAsync(present);

            var locations = await ReadAll(storage.Locations.ReadAsync());
            var state = await storage.States.GetAsync(original.Id);

            await Assert.That(locations).Count().IsEqualTo(1);
            await Assert.That(locations[0].DisplayName).IsEqualTo("Renamed Office");
            await Assert.That(state).IsEqualTo(present);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public async Task A_malformed_line_does_not_hide_later_events()
    {
        var root = CreateRoot();
        try
        {
            var storage = new NdjsonPresenceStorage(root);
            await File.AppendAllTextAsync(
                storage.Events.FilePath,
                "{ this is not valid json }\n");
            var location = Location();
            var presenceEvent = new PresenceEvent(
                Guid.NewGuid(),
                location.Id,
                PresenceTransition.Left,
                DateTimeOffset.UtcNow,
                new PresenceEvidence(
                    PresenceConfidence.Confirmed,
                    TimeSpan.FromMinutes(3)));
            await storage.Events.AppendAsync(presenceEvent);

            var events = await ReadAll(storage.Events.ReadAsync());

            await Assert.That(events).Count().IsEqualTo(1);
            await Assert.That(events[0]).IsEqualTo(presenceEvent);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public async Task Observations_are_written_to_utc_daily_files_and_read_by_date()
    {
        var root = CreateRoot();
        try
        {
            var storage = new NdjsonPresenceStorage(root);
            var observation = new PresenceObservationRecord(
                new DateTimeOffset(2026, 9, 29, 23, 59, 0, TimeSpan.FromHours(2)),
                new GeoCoordinate(58.14623, 7.99517),
                12,
                RecordedWifiStatus.Available,
                "Semine");

            await storage.Observations.AppendAsync(observation);

            var utcDate = new DateOnly(2026, 9, 29);
            var restored = await ReadAll(storage.Observations.ReadAsync(utcDate));

            await Assert.That(storage.Observations.GetFilePath(utcDate))
                .IsEqualTo(Path.Combine(root, "observations", "2026-09-29.ndjson"));
            await Assert.That(restored).Count().IsEqualTo(1);
            await Assert.That(restored[0]).IsEqualTo(observation);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    static TrackedLocation Location() =>
        new(
            Guid.Parse("5ad8d6e7-63f1-4d22-a5e4-0a972702b4d1"),
            "Office",
            new GeoCircle(new GeoCoordinate(58.14623, 7.99517), 200),
            new WifiEvidence("CorpWifi"),
            PresencePolicyDefaults.Standard);

    static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "presence-ledger-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    static async Task<List<T>> ReadAll<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
            values.Add(value);
        return values;
    }
}
