using System.Collections.Immutable;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Append-only human assignment of a Dimension value to resolved work.</summary>
public sealed record DimensionAssignment
{
    /// <summary>Initializes an assignment fact.</summary>
    public DimensionAssignment(
        Guid id,
        WorkDayKey workDay,
        string dimensionId,
        string valueId,
        IEnumerable<WorkInterval> intervals,
        Guid? correctsAssignmentId,
        ActorRef recordedBy,
        DateTimeOffset recordedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An assignment requires an identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueId);
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(recordedBy);
        if (correctsAssignmentId == id)
        {
            throw new ArgumentException(
                "An assignment cannot correct itself.",
                nameof(correctsAssignmentId));
        }

        Intervals = intervals.ToImmutableArray();
        if (Intervals.IsEmpty)
        {
            throw new ArgumentException(
                "A Dimension assignment requires at least one interval.",
                nameof(intervals));
        }

        Id = id;
        WorkDay = workDay;
        DimensionId = dimensionId;
        ValueId = valueId;
        CorrectsAssignmentId = correctsAssignmentId;
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
    }

    /// <summary>Assignment identity.</summary>
    public Guid Id { get; }

    /// <summary>Logical workday described by the assignment.</summary>
    public WorkDayKey WorkDay { get; }

    /// <summary>Dimension being assigned.</summary>
    public string DimensionId { get; }

    /// <summary>Value being assigned.</summary>
    public string ValueId { get; }

    /// <summary>Resolved-work intervals being described.</summary>
    public ImmutableArray<WorkInterval> Intervals { get; }

    /// <summary>Earlier assignment superseded by this fact, if any.</summary>
    public Guid? CorrectsAssignmentId { get; }

    /// <summary>Actor who recorded the fact.</summary>
    public ActorRef RecordedBy { get; }

    /// <summary>Recording timestamp.</summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>Creates a first manual assignment.</summary>
    public static DimensionAssignment Create(
        WorkDayKey workDay,
        string dimensionId,
        string valueId,
        IEnumerable<WorkInterval> intervals,
        ActorRef recordedBy,
        DateTimeOffset recordedAt,
        Guid? id = null) =>
        new(
            id ?? Guid.CreateVersion7(),
            workDay,
            dimensionId,
            valueId,
            intervals,
            null,
            recordedBy,
            recordedAt);

    /// <summary>Creates an append-only correction to an earlier assignment.</summary>
    public static DimensionAssignment Correction(
        WorkDayKey workDay,
        Guid correctsAssignmentId,
        string dimensionId,
        string valueId,
        IEnumerable<WorkInterval> intervals,
        ActorRef recordedBy,
        DateTimeOffset recordedAt,
        Guid? id = null) =>
        new(
            id ?? Guid.CreateVersion7(),
            workDay,
            dimensionId,
            valueId,
            intervals,
            correctsAssignmentId,
            recordedBy,
            recordedAt);
}
