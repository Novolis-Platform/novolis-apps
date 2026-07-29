using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Stall-abandon and Priority wear events → insured claim (deductible) that does not restore cargo.
/// Underwriter cash drains; premiums rise via actuarial load on the registry.
/// </summary>
internal static class ClaimsPulse
{
  public const decimal Deductible = 40m;
  public const decimal ClaimPerCargoUnit = 8m;
  public const decimal PriorityWearClaimThreshold = 28m;

  public static void TickDay(
    EconomySimulation sim,
    ShipRegistry registry,
    CampaignWorld.Ids ids,
    MilestoneLog milestones,
    ShipBiographyLog bios,
    ClaimsTracker tracker)
  {
    var world = sim.State.World;
    var day = sim.State.Clock.Date.DayIndex;
    var date = sim.State.Clock.Date;

    if (!world.Ledgers.TryGetValue(registry.Underwriter, out var underwriter))
    {
      return;
    }

    foreach (var ship in world.Shipments.Where(s => !s.IsLegacy).ToList())
    {
      if (ship.Phase == ShipmentPhase.Cancelled && tracker.TryMarkCancelled(ship.Id.Value))
      {
        var lossValue = Money.From(Math.Max(Deductible, ship.Quantity.Value * ClaimPerCargoUnit));
        PayClaim(world, registry, underwriter, ship.FirmId, lossValue, date, milestones, day,
          $"stall-abandon claim {lossValue.Amount:0} qty {ship.Quantity.Value:0.#}");
        var hubName = world.Hubs.TryGetValue(ship.CurrentHubId, out var h) ? h.Name : "?";
        bios.Record(day, ship.FirmId, hubName, hubName,
          CampaignWorld.SkuLabel(ship.ProductId, ids), ship.Quantity.Value,
          ship.TransitProfile, 0m, "stall-abandon");
      }

      if (ship.Phase == ShipmentPhase.Underway
          && ship.TransitProfile == TransitProfile.PriorityCommercial
          && ship.DriveWearAccrued >= PriorityWearClaimThreshold)
      {
        var entry = registry.TryGet(ship.FirmId);
        // Bomb-edging claim: Priority stress while past elective overhaul window — not every hot leg.
        if (entry is null || entry.LifeFraction < FtlDriveLifePolicy.ElectiveOverhaulFraction)
        {
          continue;
        }

        var bombKey = $"{ship.FirmId.Value:N}|{entry.OverhaulCount}";
        if (!tracker.TryMarkBombEdge(bombKey))
        {
          continue;
        }

        var lossValue = Money.From(Deductible + ship.DriveWearAccrued * 2m);
        PayClaim(world, registry, underwriter, ship.FirmId, lossValue, date, milestones, day,
          $"priority-wear claim {lossValue.Amount:0}");
      }
    }

    registry.ActuarialLoad = underwriter.Cash.Amount < 8_000m
      ? 1.35m
      : underwriter.Cash.Amount < 14_000m ? 1.15m : 1.0m;
  }

  private static void PayClaim(
    EconomyWorld world,
    ShipRegistry registry,
    FirmLedger underwriter,
    FirmId insured,
    Money gross,
    SimulationDate date,
    MilestoneLog milestones,
    int day,
    string detail)
  {
    var entry = registry.TryGet(insured);
    if (entry is null || !entry.Insured)
    {
      milestones.Add(day, "claim", "denied — uninsured " + detail);
      return;
    }

    var net = Money.From(Math.Max(0m, gross.Amount - Deductible));
    if (net.Amount <= 0m || underwriter.Cash.Amount + 0.0001m < net.Amount)
    {
      milestones.Add(day, "claim", "underwriter short " + detail);
      registry.ActuarialLoad = Math.Max(registry.ActuarialLoad, 1.5m);
      return;
    }

    underwriter.Post(AccountRole.CostOfGoodsSold, AccountRole.Cash, net, date, "Transport claim payout");
    if (world.Ledgers.TryGetValue(insured, out var firm))
    {
      firm.Post(AccountRole.Cash, AccountRole.Revenue, net, date, "Transport claim receipt");
    }

    registry.ClaimsPaid += net.Amount;
    milestones.Add(day, "claim", detail);
  }
}
