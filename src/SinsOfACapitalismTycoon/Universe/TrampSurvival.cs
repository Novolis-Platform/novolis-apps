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

  /// <summary>
  /// Next scheduled rival squeeze day for hulls still operable (seed-stable rival order).
  /// </summary>
  public static int? NextSqueezeDay(CampaignWorld.Ids ids, int dayIndex)
  {
    var rivals = ids.Registry.Entries
      .Where(IsLightCommercialTramp)
      .Where(e => !e.FirmId.Equals(ids.Carrier))
      .OrderBy(e => e.RegistryName, StringComparer.Ordinal)
      .ToList();

    for (var i = 0; i < rivals.Count; i++)
    {
      var entry = rivals[i];
      if (entry.Suspended || entry.BurnedOut || entry.Revoked)
      {
        continue;
      }

      var due = LastTrampPressure.FirstSqueezeDay + i * LastTrampPressure.SqueezeSpacingDays;
      if (dayIndex < due)
      {
        return due;
      }
    }

    return null;
  }

  public static string FormatLine(
    Snapshot snap,
    bool lastTrampMode,
    bool won,
    bool lost,
    int dayIndex = 0,
    CampaignWorld.Ids? ids = null)
  {
    if (won)
    {
      return $"LAST TRAMP · WIN — {snap.OperableNames[0]} sole operable ({snap.OperableCount}/{snap.TotalLightCommercial})";
    }

    if (lost)
    {
      return $"LAST TRAMP · LOSE — Calypso grounded · rivals left {snap.OperableCount}";
    }

    if (!lastTrampMode)
    {
      return $"Tramps operable {snap.OperableCount}/{snap.TotalLightCommercial}";
    }

    var core =
      $"Tramps operable {snap.OperableCount}/{snap.TotalLightCommercial} · Calypso {(snap.CalypsoOperable ? "OK" : "DOWN")}";
    if (ids is not null && NextSqueezeDay(ids, dayIndex) is { } next)
    {
      core += $" · next squeeze ~d{next}";
    }

    var groundedRivals = snap.GroundedNames
      .Where(n => !n.StartsWith(CampaignWorld.PlayerHullName, StringComparison.OrdinalIgnoreCase))
      .ToList();
    if (groundedRivals.Count > 0)
    {
      var first = groundedRivals[0];
      var cut = first.IndexOf(" (", StringComparison.Ordinal);
      var name = cut > 0 ? first[..cut] : first;
      core += groundedRivals.Count == 1
        ? $" · down: {name}"
        : $" · down {groundedRivals.Count} (e.g. {name})";
    }

    return core;
  }
}
