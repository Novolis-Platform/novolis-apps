using System.Collections.Immutable;

namespace Novolis.Hours.Contracts;

/// <summary>Client request for recording one presence assertion.</summary>
public sealed record RecordWorkRegistrationRequest(
    string EmployeeId,
    DateOnly NominalDate,
    WorkRegistrationIntent Intent,
    ImmutableArray<WorkIntervalRequest> Intervals,
    Guid? CorrectsRegistrationId,
    string? Note);
