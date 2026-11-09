using System.Globalization;
using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class TimeTrackingExportTests
{
    static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "ledger-plus-2",
        TimeSpan.FromHours(2),
        "ledger-plus-2",
        "ledger-plus-2");

    static readonly Guid WorkId = Guid.Parse("0c2cdf46-fac0-4107-b3bb-f8c21c3fac46");

    [Test]
    public async Task Clockify_csv_emits_closed_stays_only()
    {
        var arrived = new DateTimeOffset(2026, 10, 2, 6, 59, 0, TimeSpan.Zero);
        var left = new DateTimeOffset(2026, 10, 2, 15, 59, 0, TimeSpan.Zero);
        var open = new DateTimeOffset(2026, 10, 2, 16, 19, 0, TimeSpan.Zero);
        var asOf = new DateTimeOffset(2026, 10, 2, 19, 19, 0, TimeSpan.Zero);
        var report = AttendanceReport.Build(
            [
                Event(WorkId, PresenceTransition.Arrived, arrived),
                Event(WorkId, PresenceTransition.Left, left),
                Event(WorkId, PresenceTransition.Arrived, open),
            ],
            [Work()]);

        var csv = TimeTrackingExport.ToClockifyCsv(
            report,
            PlusTwo,
            asOf,
            projectName: "Novolis",
            billable: true);

        await Assert.That(csv).Contains("Project,Client,Description");
        await Assert.That(csv).Contains("Novolis");
        await Assert.That(csv).Contains("work");
        await Assert.That(csv).Contains("Yes");
        await Assert.That(csv).Contains("02/10/2026");
        await Assert.That(csv).Contains("08:59");
        await Assert.That(csv).Contains("17:59");
        await Assert.That(csv).Contains("9:00");
        await Assert.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length)
            .IsEqualTo(2);
    }

    [Test]
    public async Task Hours_json_uses_stable_schema_and_local_times()
    {
        var arrived = new DateTimeOffset(2026, 10, 2, 6, 59, 0, TimeSpan.Zero);
        var left = new DateTimeOffset(2026, 10, 2, 15, 59, 0, TimeSpan.Zero);
        var report = AttendanceReport.Build(
            [
                Event(WorkId, PresenceTransition.Arrived, arrived),
                Event(WorkId, PresenceTransition.Left, left),
            ],
            [Work()]);

        var json = TimeTrackingExport.ToHoursJson(report, PlusTwo, left);
        var csv = TimeTrackingExport.ToHoursCsv(report, PlusTwo, left);

        await Assert.That(json).Contains("novolis.hours.presence-visits.v1");
        await Assert.That(json).Contains("\"place\": \"work\"");
        await Assert.That(json).Contains("\"durationMinutes\": 540");
        await Assert.That(csv).Contains("date,place,arrivedLocal,leftLocal,durationMinutes");
        await Assert.That(csv).Contains("2026-10-02,work,08:59:00,17:59:00,540");
    }

    [Test]
    public async Task Observation_settings_clamp_out_of_range_values()
    {
        var settings = new ObservationSettings(
            pollIntervalSeconds: 1,
            freshFixAgeMinutes: 999,
            autoPublishMinimumIntervalMinutes: 0);

        await Assert.That(settings.PollIntervalSeconds).IsEqualTo(30);
        await Assert.That(settings.FreshFixAgeMinutes).IsEqualTo(2);
        await Assert.That(settings.AutoPublishMinimumIntervalMinutes).IsEqualTo(15);
    }

    static TrackedLocation Work() =>
        new(
            WorkId,
            "work",
            new GeoCircle(new GeoCoordinate(58.15, 8.0), 80),
            null,
            PresencePolicyDefaults.Standard);

    static PresenceEvent Event(
        Guid locationId,
        PresenceTransition transition,
        DateTimeOffset at) =>
        new(
            Guid.NewGuid(),
            locationId,
            transition,
            at,
            new PresenceEvidence(PresenceConfidence.Confirmed, TimeSpan.FromMinutes(3)));
}
