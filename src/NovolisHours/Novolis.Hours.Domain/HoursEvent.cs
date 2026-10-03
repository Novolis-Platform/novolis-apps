using System.Text.Json;

namespace Novolis.Hours.Domain;

/// <summary>Versioned, immutable worktime journal event with a JSON payload for durable replay.</summary>
public sealed record HoursEvent(
    Guid Id,
    string EmployeeId,
    string Type,
    string PayloadJson,
    HoursActor Actor,
    DateTimeOffset OccurredAtUtc)
{
    /// <summary>Creates a journal event from a serializable immutable payload.</summary>
    public static HoursEvent Create<TPayload>(
        string employeeId,
        string type,
        TPayload payload,
        HoursActor actor,
        DateTimeOffset occurredAtUtc) =>
        new(
            Guid.CreateVersion7(),
            employeeId,
            type,
            JsonSerializer.Serialize(payload, HoursJson.Options),
            actor,
            occurredAtUtc);

    /// <summary>Reads the event payload using the shared journal serialization settings.</summary>
    public TPayload ReadPayload<TPayload>() =>
        JsonSerializer.Deserialize<TPayload>(PayloadJson, HoursJson.Options)
        ?? throw new InvalidOperationException($"Journal event '{Id}' has an empty {typeof(TPayload).Name} payload.");
}
