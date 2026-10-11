using System.Text.Json;
using System.Text.Json.Serialization;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class PresencePhoneExportTests
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Test]
    public async Task Export_confirms_the_third_location_after_sparse_gps_fixes()
    {
        var root = Environment.GetEnvironmentVariable("NOVOLIS_PRESENCE_LEDGER_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        var locations = ReadLines<TrackedLocation>(
                Path.Combine(root, "locations.ndjson"))
            .GroupBy(item => item.Id)
            .Select(group => group.Last())
            .ToArray();
        var observations = Directory
            .EnumerateFiles(Path.Combine(root, "observations"), "*.ndjson")
            .OrderBy(path => path, StringComparer.Ordinal)
            .SelectMany(ReadLines<PresenceObservationRecord>)
            .ToArray();
        var third = locations.Single(item => item.DisplayName == "Barrikaden");
        var replay = PresenceReplay.Replay(locations, observations);
        var events = replay.Events
            .Where(item => item.LocationId == third.Id)
            .OrderBy(item => item.At)
            .ToArray();

        Console.WriteLine(
            $"export-observations={observations.Length} third-events={events.Length} " +
            $"third-policy-gap={third.Policy.MaximumEvidenceGap}");
        foreach (var presenceEvent in events)
            Console.WriteLine($"third-event={presenceEvent.Transition} at={presenceEvent.At:o}");

        await Assert.That(events.Count(item => item.Transition == PresenceTransition.Arrived))
            .IsGreaterThanOrEqualTo(1);
        await Assert.That(events.Any(item =>
                item.Transition == PresenceTransition.Arrived
                && item.At >= new DateTimeOffset(2026, 10, 8, 16, 0, 0, TimeSpan.Zero)
                && item.At <= new DateTimeOffset(2026, 10, 8, 17, 0, 0, TimeSpan.Zero)))
            .IsTrue();
    }

    static IEnumerable<T> ReadLines<T>(string path)
    {
        if (!File.Exists(path))
            return [];

        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<T>(line, Json)
                ?? throw new InvalidDataException($"Could not deserialize {path}."));
    }
}
