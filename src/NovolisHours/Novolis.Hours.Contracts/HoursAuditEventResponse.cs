namespace Novolis.Hours.Contracts;

/// <summary>Wire audit event reference for drill-down without exposing domain entities.</summary>
public sealed record HoursAuditEventResponse(
    Guid EventId,
    string EmployeeId,
    string EventType,
    string ActorId,
    string ActorRole,
    DateTimeOffset OccurredAtUtc,
    string? PayloadReference = null);
