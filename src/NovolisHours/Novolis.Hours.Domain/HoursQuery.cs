namespace Novolis.Hours.Domain;

/// <summary>Immutable read request for a named worktime projection slice.</summary>
public sealed record HoursQuery(
    string EmployeeId,
    HoursQueryKind Kind,
    DateOnly? From = null,
    DateOnly? Through = null);
