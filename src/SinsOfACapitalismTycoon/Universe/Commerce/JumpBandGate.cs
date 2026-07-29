using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Jump-band refuse: long+dense Priority refused unless reputation ≥40 or open escrow.
/// Fiction: twelve ly dense sprint — refuse unless someone else owns the drive.
/// </summary>
internal static class JumpBandGate
{
  public const decimal DifficultyThreshold = 2.5m;
  public const decimal ReputationOverride = 40m;
  public const long HoursProxyThreshold = 48;

  public static bool ShouldRefuse(
    EconomyWorld world,
    CampaignWorld.Ids ids,
    ReputationLedger reputation,
    EscrowBook escrow,
    FirmId firm,
    ProductId sku,
    TransportHubId origin,
    TransportHubId dest,
    TransitProfile profile,
    MilestoneLog? milestones = null,
    int day = 0)
  {
    if (profile != TransitProfile.PriorityCommercial)
    {
      return false;
    }

    if (!sku.Equals(ids.Parts) && !sku.Equals(ids.Goods))
    {
      return false;
    }

    if (reputation.Get(firm) >= ReputationOverride || escrow.FirmHasOpen(firm))
    {
      return false;
    }

    if (!IsLongDenseLane(world, origin, dest, out var maxDiff, out var hours))
    {
      return false;
    }

    if (maxDiff < DifficultyThreshold || hours < HoursProxyThreshold)
    {
      return false;
    }

    milestones?.AddOnce(day, "jump-refuse",
      $"{ids.Registry.TryGet(firm)?.RegistryName ?? "tramp"} Priority dense band (diff {maxDiff:0.#} · {hours}h)");
    return true;
  }

  private static bool IsLongDenseLane(
    EconomyWorld world,
    TransportHubId origin,
    TransportHubId dest,
    out decimal maxDifficulty,
    out long hours)
  {
    maxDifficulty = 0m;
    hours = 0;
    // Direct corridor or any corridor touching both hubs as endpoints.
    foreach (var cor in world.Corridors.Values)
    {
      var touches =
        (cor.From.Equals(origin) && cor.To.Equals(dest))
        || (cor.From.Equals(dest) && cor.To.Equals(origin));
      if (!touches)
      {
        continue;
      }

      maxDifficulty = Math.Max(maxDifficulty, cor.Difficulty);
      hours += TransitProfiles.EffectiveHours(cor, TransitProfile.PriorityCommercial);
    }

    return maxDifficulty > 0m;
  }

  /// <summary>Daily dock standing fees at capital/refinery for parked operable hulls.</summary>
  public static void TickDockFees(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    var world = sim.State.World;
    var day = sim.State.Clock.Date.DayIndex;
    var date = sim.State.Clock.Date;
    if (!world.Ledgers.TryGetValue(ids.Station, out var station))
    {
      return;
    }

    foreach (var entry in ids.Registry.Entries.Where(e => e.OwnerMaster && e.CanOperate))
    {
      var hubId = world.Shipments
        .Where(s => s.FirmId.Equals(entry.FirmId) && !s.IsLegacy)
        .Select(s => s.CurrentHubId)
        .FirstOrDefault();
      if (hubId.Value == Guid.Empty || !world.Hubs.TryGetValue(hubId, out var hub))
      {
        continue;
      }

      var site = ids.Sites.Values.FirstOrDefault(s => s.Hub.HubId.Equals(hub.Id));
      if (site is null)
      {
        continue;
      }

      var feeAmt = PortTier.DockStandingFee(PortTier.ForRole(site.Hub.Role));
      if (feeAmt <= 0m)
      {
        continue;
      }

      if (!world.Ledgers.TryGetValue(entry.FirmId, out var firm)
          || firm.Cash.Amount + 0.0001m < feeAmt)
      {
        continue;
      }

      var fee = Money.From(feeAmt);
      firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, fee, date, "Port dock standing");
      station.Post(AccountRole.Cash, AccountRole.Revenue, fee, date, "Port dock standing");
      milestones.AddOnce(day, "dock-fee",
        $"{entry.RegistryName} @{site.Hub.Name} · {PortTier.Tag(site.Hub.Role)} {feeAmt:0}");
    }
  }
}
