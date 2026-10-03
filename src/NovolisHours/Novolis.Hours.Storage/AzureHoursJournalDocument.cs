using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Scalar Azure Table row for one append-only Hours event.</summary>
public sealed class AzureHoursJournalDocument : IHasId
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>Employee stream identifier.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Versioned Hours event type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Immutable domain payload serialized with the Hours JSON contract.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Actor identifier.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Actor display name captured at append time.</summary>
    public string ActorDisplayName { get; set; } = string.Empty;

    /// <summary>Actor role captured at append time.</summary>
    public string ActorRole { get; set; } = nameof(HoursActorRole.System);

    /// <summary>Append timestamp used for deterministic replay ordering.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Creates a scalar row while preserving the domain event contract.</summary>
    public static AzureHoursJournalDocument FromEvent(HoursEvent entry) =>
        new()
        {
            Id = entry.Id,
            EmployeeId = entry.EmployeeId,
            Type = entry.Type,
            PayloadJson = entry.PayloadJson,
            ActorId = entry.Actor.Id,
            ActorDisplayName = entry.Actor.DisplayName,
            ActorRole = entry.Actor.Role.ToString(),
            OccurredAtUtc = entry.OccurredAtUtc,
        };

    /// <summary>Rehydrates the immutable domain event.</summary>
    public HoursEvent ToEvent()
    {
        if (!Enum.TryParse<HoursActorRole>(ActorRole, ignoreCase: false, out var role))
        {
            throw new InvalidOperationException(
                $"Hours Azure journal row '{Id}' contains unknown actor role '{ActorRole}'.");
        }

        return new HoursEvent(
            Id,
            EmployeeId,
            Type,
            PayloadJson,
            new HoursActor(ActorId, ActorDisplayName, role),
            OccurredAtUtc);
    }
}
