namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Stable hub key (usually Astro system id).</summary>
public readonly record struct MeshHubId(string Value)
{
  public override string ToString() => Value;
  public static MeshHubId From(string value) => new(value);
}

/// <summary>Person, ship, or firm mesh identity — location optional.</summary>
public readonly record struct MeshIdentityId(string Value)
{
  public override string ToString() => Value;
  public static MeshIdentityId From(string value) => new(value);
}

/// <summary>Published packet id.</summary>
public readonly record struct PacketId(Guid Value)
{
  public static PacketId New() => new(Guid.NewGuid());
  public static PacketId From(Guid value) => new(value);
}

/// <summary>In-flight disposable drone instance.</summary>
public readonly record struct DroneId(Guid Value)
{
  public static DroneId New() => new(Guid.NewGuid());
}
