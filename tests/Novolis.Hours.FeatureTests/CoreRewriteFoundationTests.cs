using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.FeatureTests;

public sealed class CoreRewriteFoundationTests
{
    [Test]
    public void Work_interval_requires_a_positive_elapsed_duration()
    {
        var start = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkInterval(start, start));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkInterval(start, start.AddMinutes(-1)));
    }

    [Test]
    public void Work_day_key_is_the_employee_and_nominal_date_not_a_midnight_boundary()
    {
        var key = new WorkDayKey("ada", new DateOnly(2026, 10, 1));

        Assert.That(key.EmployeeId).IsEqualTo("ada");
        Assert.That(key.NominalDate).IsEqualTo(new DateOnly(2026, 10, 1));
    }

    [Test]
    public void Scheduled_registration_has_no_fake_clock_observations()
    {
        var registration = WorkRegistration.WorkedAsScheduled(
            new WorkDayKey("ada", new DateOnly(2026, 10, 1)),
            ActorRef.Employee("ada"),
            new ConfigurationSnapshotId(Guid.NewGuid()),
            new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
            "Routine followed.");

        Assert.That(registration.Intent).IsEqualTo(WorkRecordIntent.WorkedAsScheduled);
        Assert.That(registration.Intervals).IsEmpty();
        Assert.That(registration.Source).IsEqualTo(WorkRecordSource.Employee);
    }

    [Test]
    public void Correction_is_an_additional_assertion_with_a_source_reference()
    {
        var key = new WorkDayKey("ada", new DateOnly(2026, 10, 1));
        var original = WorkRegistration.Manual(
            key,
            [
                new WorkInterval(
                    new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 1, 16, 0, 0, TimeSpan.Zero)),
            ],
            ActorRef.Employee("ada"),
            new ConfigurationSnapshotId(Guid.NewGuid()),
            new DateTimeOffset(2026, 10, 1, 16, 1, 0, TimeSpan.Zero),
            "Initial registration.");

        var correction = WorkRegistration.Correction(
            key,
            original.Id,
            original.Intervals,
            ActorRef.Employee("ada"),
            original.ConfigurationSnapshotId,
            original.RecordedAt.AddMinutes(5),
            "Corrected note.");

        Assert.That(correction.Id).IsNotEqualTo(original.Id);
        Assert.That(correction.CorrectsRegistrationId).IsEqualTo(original.Id);
        Assert.That(correction.Intent).IsEqualTo(WorkRecordIntent.Correction);
        Assert.That(correction.Intervals).IsEqualTo(original.Intervals);
    }

    [Test]
    public void Work_registration_rejects_empty_employee_and_invalid_correction_references()
    {
        Assert.Throws<ArgumentException>(
            () => new WorkDayKey(string.Empty, new DateOnly(2026, 10, 1)));

        var key = new WorkDayKey("ada", new DateOnly(2026, 10, 1));
        Assert.Throws<ArgumentException>(
            () => WorkRegistration.Correction(
                key,
                Guid.Empty,
                ImmutableArray<WorkInterval>.Empty,
                ActorRef.Employee("ada"),
                new ConfigurationSnapshotId(Guid.NewGuid()),
                DateTimeOffset.UtcNow,
                "Invalid correction."));
    }
}
