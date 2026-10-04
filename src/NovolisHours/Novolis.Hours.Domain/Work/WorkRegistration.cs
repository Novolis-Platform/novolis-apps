using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Work;

/// <summary>Immutable assertion about work, separate from all derived meaning.</summary>
public sealed record WorkRegistration
{
    /// <summary>Initializes an immutable work assertion.</summary>
    public WorkRegistration(
        Guid id,
        WorkDayKey workDay,
        WorkRecordSource source,
        WorkRecordIntent intent,
        ImmutableArray<WorkInterval> intervals,
        Guid? correctsRegistrationId,
        string? note,
        ActorRef recordedBy,
        DateTimeOffset recordedAt,
        ConfigurationSnapshotId configurationSnapshotId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A work registration requires an identity.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(recordedBy);
        if (intent == WorkRecordIntent.Correction &&
            (!correctsRegistrationId.HasValue || correctsRegistrationId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                "A correction must identify the registration it corrects.",
                nameof(correctsRegistrationId));
        }

        if (intent != WorkRecordIntent.Correction && correctsRegistrationId.HasValue)
        {
            throw new ArgumentException(
                "Only a correction can reference another registration.",
                nameof(correctsRegistrationId));
        }

        if (intent != WorkRecordIntent.WorkedAsScheduled && intervals.IsEmpty)
        {
            throw new ArgumentException(
                "Manual registrations and corrections require at least one interval.",
                nameof(intervals));
        }

        Id = id;
        WorkDay = workDay;
        Source = source;
        Intent = intent;
        Intervals = intervals;
        CorrectsRegistrationId = correctsRegistrationId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
        configurationSnapshotId.EnsureAssigned();
        ConfigurationSnapshotId = configurationSnapshotId;
    }

    /// <summary>Registration identity.</summary>
    public Guid Id { get; }

    /// <summary>Logical WorkDay to which the assertion belongs.</summary>
    public WorkDayKey WorkDay { get; }

    /// <summary>Origin of the assertion.</summary>
    public WorkRecordSource Source { get; }

    /// <summary>Assertion intent.</summary>
    public WorkRecordIntent Intent { get; }

    /// <summary>Observed intervals; empty only for WorkedAsScheduled.</summary>
    public ImmutableArray<WorkInterval> Intervals { get; }

    /// <summary>Earlier assertion replaced by this correction, when applicable.</summary>
    public Guid? CorrectsRegistrationId { get; }

    /// <summary>Optional context, not a permission or reason code.</summary>
    public string? Note { get; }

    /// <summary>Actor who recorded the assertion.</summary>
    public ActorRef RecordedBy { get; }

    /// <summary>Time at which the assertion was recorded.</summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>Configuration identity required to replay the original interpretation.</summary>
    public ConfigurationSnapshotId ConfigurationSnapshotId { get; }

    /// <summary>Creates a scheduled assertion without inventing observed clock intervals.</summary>
    public static WorkRegistration WorkedAsScheduled(
        WorkDayKey workDay,
        ActorRef recordedBy,
        ConfigurationSnapshotId configurationSnapshotId,
        DateTimeOffset recordedAt,
        string? note = null,
        Guid? id = null,
        WorkRecordSource source = WorkRecordSource.Employee) =>
        new(
            id ?? Guid.CreateVersion7(),
            workDay,
            source,
            WorkRecordIntent.WorkedAsScheduled,
            [],
            null,
            note,
            recordedBy,
            recordedAt,
            configurationSnapshotId);

    /// <summary>Creates a manual observed-interval assertion.</summary>
    public static WorkRegistration Manual(
        WorkDayKey workDay,
        IEnumerable<WorkInterval> intervals,
        ActorRef recordedBy,
        ConfigurationSnapshotId configurationSnapshotId,
        DateTimeOffset recordedAt,
        string? note = null,
        Guid? id = null,
        WorkRecordSource source = WorkRecordSource.Employee) =>
        new(
            id ?? Guid.CreateVersion7(),
            workDay,
            source,
            WorkRecordIntent.ManualRegistration,
            intervals.ToImmutableArray(),
            null,
            note,
            recordedBy,
            recordedAt,
            configurationSnapshotId);

    /// <summary>Creates a correction while retaining the original registration as history.</summary>
    public static WorkRegistration Correction(
        WorkDayKey workDay,
        Guid correctsRegistrationId,
        IEnumerable<WorkInterval> intervals,
        ActorRef recordedBy,
        ConfigurationSnapshotId configurationSnapshotId,
        DateTimeOffset recordedAt,
        string? note = null,
        Guid? id = null,
        WorkRecordSource source = WorkRecordSource.Employee) =>
        new(
            id ?? Guid.CreateVersion7(),
            workDay,
            source,
            WorkRecordIntent.Correction,
            intervals.ToImmutableArray(),
            correctsRegistrationId,
            note,
            recordedBy,
            recordedAt,
            configurationSnapshotId);
}
