using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Last-tramp memoir: light-commercial hulls that still hold the registry door
/// (<see cref="ShipRegistryEntry.CanOperate"/>). Mega hauler is never in the count.
/// </summary>
internal static class TrampSurvival
{
  public const string LightCommercial = "LightCommercial";

  public sealed record Snapshot(
    int OperableCount,
    int TotalLightCommercial,
    bool CalypsoOperable,
    bool CalypsoIsSoleSurvivor,
    IReadOnlyList<string> OperableNames,
    IReadOnlyList<string> GroundedNames);

  public static Snapshot Capture(CampaignWorld.Ids ids)
  {
    var operable = new List<string>();
    var grounded = new List<string>();
    var total = 0;
    var calypsoOk = false;

    foreach (var entry in ids.Registry.Entries.OrderBy(e => e.RegistryName, StringComparer.Ordinal))
    {
      if (!IsLightCommercialTramp(entry))
      {
        continue;
      }

      total++;
      if (entry.CanOperate)
      {
        operable.Add(entry.RegistryName);
        if (entry.FirmId.Equals(ids.Carrier))
        {
          calypsoOk = true;
        }
      }
      else
      {
        grounded.Add($"{entry.RegistryName} ({entry.StandingLabel})");
      }
    }

    return new Snapshot(
      operable.Count,
      total,
      calypsoOk,
      CalypsoIsSoleSurvivor: calypsoOk && operable.Count == 1,
      operable,
      grounded);
  }

  public static bool IsLightCommercialTramp(ShipRegistryEntry entry) =>
    entry.HullClass.Equals(LightCommercial, StringComparison.OrdinalIgnoreCase);

  public static string FormatLine(Snapshot snap, bool lastTrampMode, bool won, bool lost) =>
    won ? $"LAST TRAMP · WIN — {snap.OperableNames[0]} sole operable ({snap.OperableCount}/{snap.TotalLightCommercial})"
    : lost ? $"LAST TRAMP · LOSE — Calypso grounded · rivals left {snap.OperableCount}"
    : lastTrampMode
      ? $"Tramps operable {snap.OperableCount}/{snap.TotalLightCommercial} · Calypso {(snap.CalypsoOperable ? "OK" : "DOWN")}"
      : $"Tramps operable {snap.OperableCount}/{snap.TotalLightCommercial}";
}
