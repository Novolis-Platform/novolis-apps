using System.Collections.Immutable;

namespace Novolis.Hours.Contracts;

/// <summary>Wire projection of an immutable work assertion.</summary>
public sealed record WorkRegistrationResponse(
    Guid Id,
    string EmployeeId,
    DateOnly NominalDate,
    string Source,
    WorkRegistrationIntent Intent,
    ImmutableArray<WorkIntervalRequest> Intervals,
    Guid? CorrectsRegistrationId,
    string? Note,
    string RecordedBy,
    string RecordedByRole,
    DateTimeOffset RecordedAt,
    Guid ConfigurationSnapshotId);
