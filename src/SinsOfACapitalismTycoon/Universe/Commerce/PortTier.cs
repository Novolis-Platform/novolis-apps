namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Calypso station-tier overlays on campaign <see cref="SystemRole"/>.</summary>
internal enum PortTierKind : byte
{
  Hub = 0,
  Refinery = 1,
  Edge = 2,
  Shady = 3,
  Cert = 4,
}

/// <summary>Role → tier tag + ops multipliers (dwell/toll/premium friction).</summary>
internal static class PortTier
{
  public static PortTierKind ForRole(SystemRole role) =>
    role switch
    {
      SystemRole.Capital => PortTierKind.Hub,
      SystemRole.Industrial => PortTierKind.Refinery,
      SystemRole.Inhabited => PortTierKind.Cert,
      SystemRole.Mining => PortTierKind.Shady,
      SystemRole.Transit => PortTierKind.Edge,
      _ => PortTierKind.Edge,
    };

  public static string Tag(PortTierKind tier) =>
    tier switch
    {
      PortTierKind.Hub => "hub",
      PortTierKind.Refinery => "refinery",
      PortTierKind.Cert => "cert",
      PortTierKind.Shady => "shady",
      _ => "edge",
    };

  public static string Tag(SystemRole role) => Tag(ForRole(role));

  /// <summary>Dwell multiplier at seed (fee-heavy hubs slower).</summary>
  public static decimal DwellFactor(PortTierKind tier) =>
    tier switch
    {
      PortTierKind.Hub => 1.25m,
      PortTierKind.Refinery => 1.10m,
      PortTierKind.Cert => 1.05m,
      PortTierKind.Shady => 0.95m,
      _ => 1.0m,
    };

  /// <summary>Corridor toll multiplier when either end is this tier.</summary>
  public static decimal TollFactor(PortTierKind tier) =>
    tier switch
    {
      PortTierKind.Hub => 1.20m,
      PortTierKind.Refinery => 1.10m,
      PortTierKind.Cert => 1.05m,
      PortTierKind.Shady => 0.90m,
      _ => 0.95m,
    };

  /// <summary>Insurance premium friction (shady/cert slightly kinder).</summary>
  public static decimal PremiumFactor(PortTierKind tier) =>
    tier switch
    {
      PortTierKind.Hub => 1.05m,
      PortTierKind.Refinery => 1.02m,
      PortTierKind.Cert => 0.95m,
      PortTierKind.Shady => 0.95m,
      _ => 1.0m,
    };

  /// <summary>Daily berth standing fee when a tramp sits at hub/refinery.</summary>
  public static decimal BerthStandingFee(PortTierKind tier) =>
    tier switch
    {
      PortTierKind.Hub => 12m,
      PortTierKind.Refinery => 8m,
      PortTierKind.Cert => 6m,
      _ => 0m,
    };
}
