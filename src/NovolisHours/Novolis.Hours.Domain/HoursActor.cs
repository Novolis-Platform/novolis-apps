namespace Novolis.Hours.Domain;

/// <summary>Immutable actor metadata stored beside every journal event.</summary>
public sealed record HoursActor(string Id, string DisplayName, HoursActorRole Role)
{
    /// <summary>Creates an employee actor.</summary>
    public static HoursActor Employee(string id, string? displayName = null) =>
        new(id, displayName ?? id, HoursActorRole.Employee);

    /// <summary>Creates a manager actor.</summary>
    public static HoursActor Manager(string id, string? displayName = null) =>
        new(id, displayName ?? id, HoursActorRole.Manager);

    /// <summary>Creates a human-resources actor.</summary>
    public static HoursActor HumanResources(string id = "hr", string? displayName = null) =>
        new(id, displayName ?? id, HoursActorRole.HumanResources);

    /// <summary>Creates a higher-level reviewer actor.</summary>
    public static HoursActor Higher(string id = "higher", string? displayName = null) =>
        new(id, displayName ?? id, HoursActorRole.Higher);

    /// <summary>Creates a system actor.</summary>
    public static HoursActor System() => new("system", "System", HoursActorRole.System);
}
