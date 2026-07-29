namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Stable mesh node key (usually Astro system id).</summary>
public readonly record struct MeshNodeId(string Value)
{
  public override string ToString() => Value;
  public static MeshNodeId From(string value) => new(value);
}

/// <summary>Mailbox / feed owner key. Prefer <see cref="MeshIdentityIds"/> factories for naming.</summary>
public readonly record struct MeshIdentityId(string Value)
{
  public override string ToString() => Value;
  public static MeshIdentityId From(string value) => new(value);
}

/// <summary>Canonical identity id prefixes: <c>person:</c>, <c>household:</c>, <c>firm:</c>, <c>ship:</c>, <c>thing:</c>.</summary>
public static class MeshIdentityIds
{
  public const string PersonPrefix = "person:";
  public const string HouseholdPrefix = "household:";
  public const string FirmPrefix = "firm:";
  public const string ShipPrefix = "ship:";
  public const string ThingPrefix = "thing:";

  public static MeshIdentityId Person(string key) => MeshIdentityId.From(PersonPrefix + key);
  public static MeshIdentityId Household(string key) => MeshIdentityId.From(HouseholdPrefix + key);
  public static MeshIdentityId Firm(string key) => MeshIdentityId.From(FirmPrefix + key);
  public static MeshIdentityId Ship(string key) => MeshIdentityId.From(ShipPrefix + key);
  public static MeshIdentityId Thing(string key) => MeshIdentityId.From(ThingPrefix + key);

  public static MeshIdentityKind? TryParseKind(MeshIdentityId id)
  {
    var v = id.Value;
    if (v.StartsWith(PersonPrefix, StringComparison.Ordinal)) return MeshIdentityKind.Person;
    if (v.StartsWith(HouseholdPrefix, StringComparison.Ordinal)) return MeshIdentityKind.Household;
    if (v.StartsWith(FirmPrefix, StringComparison.Ordinal)) return MeshIdentityKind.Firm;
    if (v.StartsWith(ShipPrefix, StringComparison.Ordinal)) return MeshIdentityKind.Ship;
    if (v.StartsWith(ThingPrefix, StringComparison.Ordinal)) return MeshIdentityKind.Thing;
    return null;
  }
}

/// <summary>Named channel (Atom/RSS-style). <see cref="Emergency"/> is mandatory for every mailbox.</summary>
public readonly record struct MeshFeedId(string Value)
{
  public override string ToString() => Value;
  public static MeshFeedId From(string value) => new(value);

  /// <summary>Forced civil alert channel — cannot unsubscribe; force-delivered at co-located nodes.</summary>
  public static MeshFeedId Emergency { get; } = From("Emergency");

  public static MeshFeedId NewsGeneral { get; } = From("News.General");
  public static MeshFeedId NewsSpaceWhales { get; } = From("News.SpaceWhales");
  public static MeshFeedId NewsPrices { get; } = From("News.Prices");

  public bool IsMandatory => Value.Equals(Emergency.Value, StringComparison.Ordinal);

  public static bool IsMandatoryFeed(MeshFeedId feed) => feed.IsMandatory;
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
