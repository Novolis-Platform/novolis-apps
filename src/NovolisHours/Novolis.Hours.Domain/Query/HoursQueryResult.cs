using System.Collections.Immutable;

namespace Novolis.Hours.Domain;

/// <summary>Immutable result of executing a named Hours query.</summary>
public sealed record HoursQueryResult(
    string EmployeeId,
    HoursQueryKind Kind,
    ImmutableArray<HoursQueryRow> Rows);
