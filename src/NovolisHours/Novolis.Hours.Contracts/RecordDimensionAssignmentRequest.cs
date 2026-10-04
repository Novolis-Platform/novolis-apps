using System.Collections.Immutable;

namespace Novolis.Hours.Contracts;

/// <summary>Appends a manual Dimension assignment to already resolved work.</summary>
public sealed record RecordDimensionAssignmentRequest(
    string EmployeeId,
    DateOnly NominalDate,
    string DimensionId,
    string ValueId,
    ImmutableArray<WorkIntervalRequest> Intervals,
    Guid? CorrectsAssignmentId);
