namespace Novolis.Hours.Domain.Work;

/// <summary>Immutable actor provenance used by work, rule, review, and report facts.</summary>
public sealed record ActorRef
{
    /// <summary>Initializes an actor reference.</summary>
    public ActorRef(string id, string displayName, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        Role = role;
    }

    /// <summary>Stable actor identifier.</summary>
    public string Id { get; }

    /// <summary>Display name captured with the fact.</summary>
    public string DisplayName { get; }

    /// <summary>Actor responsibility at the time of the fact.</summary>
    public string Role { get; }

    /// <summary>Creates an employee actor reference.</summary>
    public static ActorRef Employee(string id, string? displayName = null) =>
        new(id, displayName ?? id, "Employee");

    /// <summary>Creates a manager actor reference.</summary>
    public static ActorRef Manager(string id, string? displayName = null) =>
        new(id, displayName ?? id, "Manager");

    /// <summary>Creates an HR actor reference.</summary>
    public static ActorRef HumanResources(string id, string? displayName = null) =>
        new(id, displayName ?? id, "HumanResources");

    /// <summary>Creates a higher-level reviewer actor reference.</summary>
    public static ActorRef Higher(string id, string? displayName = null) =>
        new(id, displayName ?? id, "Higher");

    /// <summary>Creates a system actor reference.</summary>
    public static ActorRef System(string id = "system") =>
        new(id, id, "System");

    /// <summary>Adapts the provenance actor to the existing journal envelope.</summary>
    public HoursActor ToHoursActor()
    {
        if (!Enum.TryParse<HoursActorRole>(Role, ignoreCase: true, out var parsedRole))
        {
            parsedRole = HoursActorRole.System;
        }

        return new HoursActor(Id, DisplayName, parsedRole);
    }
}
