using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class AttendanceReportTests
{
    static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "ledger-plus-2",
        TimeSpan.FromHours(2),
        "ledger-plus-2",
        "ledger-plus-2");

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    static readonly Guid HomeId = Guid.Parse("a2f98b1b-795e-49c5-87cb-c22366325958");
    static readonly Guid WorkId = Guid.Parse("0c2cdf46-fac0-4107-b3bb-f8c21c3fac46");

    [Test]
    public async Task Phone_day_lists_home_and_work_arrivals_and_departures()
    {
        var asOf = new DateTimeOffset(2026, 10, 2, 19, 19, 17, TimeSpan.Zero);
        var report = AttendanceReport.Build(
            PresenceReplay.Replay([Home(), Work()], ReadPhoneDay()).Events,
            [Home(), Work()]);
        var home = report.Places.Single(place => place.DisplayName == "home");
        var work = report.Places.Single(place => place.DisplayName == "work");
        var text = report.Format(asOf, PlusTwo, CultureInfo.InvariantCulture);

        await Assert.That(home.VisitCount).IsEqualTo(2);
        await Assert.That(home.DayCount(PlusTwo, asOf)).IsEqualTo(1);
        await Assert.That((int)home.Visits[0].DurationUntil(asOf).TotalMinutes).IsEqualTo(86);
        await Assert.That(home.Visits[1].LeftAt).IsNull();
        await Assert.That((int)work.Visits.Single().DurationUntil(asOf).TotalMinutes).IsEqualTo(540);
        await Assert.That(text).Contains("arrived 07:06");
        await Assert.That(text).Contains("left 08:33 · 1h 26m");
        await Assert.That(text).Contains("arrived 08:59");
        await Assert.That(text).Contains("left 17:59 · 9h 00m");
        await Assert.That(text).Contains("arrived 18:19");
        await Assert.That(text).Contains("still there · 3h 00m");
        await Assert.That(text).Contains("By day");
    }

    [Test]
    public async Task A_stay_that_crosses_midnight_is_reported_on_both_days()
    {
        var arrived = new DateTimeOffset(2026, 10, 2, 20, 30, 0, TimeSpan.Zero);
        var left = new DateTimeOffset(2026, 10, 3, 4, 10, 0, TimeSpan.Zero);
        var report = AttendanceReport.Build(
            [
                Event(HomeId, PresenceTransition.Arrived, arrived),
                Event(HomeId, PresenceTransition.Left, left),
            ],
            [Home()]);
        var home = report.Places.Single();
        var marks = report.Marks(PlusTwo, left);

        await Assert.That(home.DayCount(PlusTwo, left)).IsEqualTo(2);
        await Assert.That(marks[0].Day).IsEqualTo(new DateOnly(2026, 10, 2));
        await Assert.That(marks[0].Line).IsEqualTo("arrived 22:30");
        await Assert.That(marks[1].Day).IsEqualTo(new DateOnly(2026, 10, 3));
        await Assert.That(marks[1].Line).IsEqualTo("left 06:10 · 7h 40m");
    }

    [Test]
    public async Task An_empty_ledger_formats_as_no_arrivals()
    {
        var text = new AttendanceReport([]).Format(
            DateTimeOffset.UnixEpoch,
            TimeZoneInfo.Utc,
            CultureInfo.InvariantCulture);

        await Assert.That(text).Contains("No arrivals or departures yet.");
    }

    static PresenceEvent Event(Guid locationId, PresenceTransition transition, DateTimeOffset at) =>
        new(
            Guid.NewGuid(),
            locationId,
            transition,
            at,
            new PresenceEvidence(PresenceConfidence.Confirmed, TimeSpan.FromMinutes(3)));

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

    static IReadOnlyList<PresenceObservationRecord> ReadPhoneDay()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "2026-10-02-phone.ndjson");
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<PresenceObservationRecord>(line, Json)
                ?? throw new InvalidOperationException("Phone sample did not deserialize."))
            .ToArray();
    }
}
