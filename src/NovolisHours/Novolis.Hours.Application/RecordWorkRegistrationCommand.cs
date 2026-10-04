using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Application;

/// <summary>Application input for recording one immutable work assertion.</summary>
public sealed record RecordWorkRegistrationCommand(
    WorkDayKey WorkDay,
    WorkRecordSource Source,
    WorkRecordIntent Intent,
    ImmutableArray<WorkInterval> Intervals,
    Guid? CorrectsRegistrationId,
    string? Note,
    ActorRef RecordedBy,
    ConfigurationSnapshotId ConfigurationSnapshotId,
    DateTimeOffset? RecordedAt = null);
