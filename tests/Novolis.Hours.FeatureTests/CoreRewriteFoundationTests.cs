using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.FeatureTests;

public sealed class CoreRewriteFoundationTests
{
    [Test]
    public async Task Work_interval_requires_a_positive_elapsed_duration()
    {
        var start = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        await Assert.That(() => new WorkInterval(start, start))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new WorkInterval(start, start.AddMinutes(-1)))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Work_day_key_is_the_employee_and_nominal_date_not_a_midnight_boundary()
    {
        var key = new WorkDayKey("ada", new DateOnly(2026, 10, 1));

        await Assert.That(key.EmployeeId).IsEqualTo("ada");
        await Assert.That(key.NominalDate).IsEqualTo(new DateOnly(2026, 10, 1));
    }

    [Test]
    public async Task Scheduled_registration_has_no_fake_clock_observations()
    {
        var registration = WorkRegistration.WorkedAsScheduled(
            new WorkDayKey("ada", new DateOnly(2026, 10, 1)),
            ActorRef.Employee("ada"),
            new ConfigurationSnapshotId(Guid.NewGuid()),
            new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
            "Routine followed.");

        await Assert.That(registration.Intent).IsEqualTo(WorkRecordIntent.WorkedAsScheduled);
        await Assert.That(registration.Intervals).IsEmpty();
        await Assert.That(registration.Source).IsEqualTo(WorkRecordSource.Employee);
    }

    [Test]
    public async Task Correction_is_an_additional_assertion_with_a_source_reference()
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

        await Assert.That(correction.Id).IsNotEqualTo(original.Id);
        await Assert.That(correction.CorrectsRegistrationId).IsEqualTo(original.Id);
        await Assert.That(correction.Intent).IsEqualTo(WorkRecordIntent.Correction);
        await Assert.That(correction.Intervals).IsEqualTo(original.Intervals);
    }

    [Test]
    public async Task Work_registration_rejects_empty_employee_and_invalid_correction_references()
    {
        await Assert.That(() => new WorkDayKey(string.Empty, new DateOnly(2026, 10, 1)))
            .Throws<ArgumentException>();

        var key = new WorkDayKey("ada", new DateOnly(2026, 10, 1));
        await Assert.That(() => WorkRegistration.Correction(
                key,
                Guid.Empty,
                ImmutableArray<WorkInterval>.Empty,
                ActorRef.Employee("ada"),
                new ConfigurationSnapshotId(Guid.NewGuid()),
                DateTimeOffset.UtcNow,
                "Invalid correction."))
            .Throws<ArgumentException>();
    }
}
