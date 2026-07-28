using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// FTL drive maintenance: fold mileage into life, elective overhaul before rated life,
/// forced overhaul after guaranteed burnout. Maintenance cost is cash — not premium alone.
/// </summary>
internal static class DriveMaintenancePulse
{
  public static void TickDay(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones)
  {
    var world = sim.State.World;
    var day = sim.State.Clock.Date;
    var dayIndex = day.DayIndex;

    foreach (var entry in registry.Entries.OrderBy(e => e.FirmId.Value))
    {
      foreach (var ship in world.Shipments.Where(s => s.FirmId.Equals(entry.FirmId) && !s.IsLegacy))
      {
        if (ship.DriveWearAccrued <= 0m)
        {
          continue;
        }

        registry.ObserveMileage(entry.FirmId, ship.DriveWearAccrued, ship.TransitProfile);
        if (ship.Itinerary.LegCount > 0
            && ship.LegIndex < ship.Itinerary.LegCount
            && world.Corridors.TryGetValue(ship.Itinerary.CorridorIds[ship.LegIndex], out var cor)
            && cor.Difficulty >= 2.5m)
        {
          registry.ObserveLongLane(entry.FirmId);
        }

        ship.DriveWearAccrued = 0m;
      }

      if (entry.BurnedOut)
      {
        milestones.AddOnce(dayIndex, "burnout", $"{entry.RegistryName} drive life exhausted");
      }
      else if (entry.OverhaulDue)
      {
        milestones.AddOnce(dayIndex, "overhaul-due",
          $"{entry.RegistryName} life {entry.LifeFraction:0%}");
      }

      registry.DecayAcuteWear(entry);

      if (!world.Ledgers.TryGetValue(entry.FirmId, out var firmLedger)
          || !world.Ledgers.TryGetValue(registry.Underwriter, out var yardLedger))
      {
        continue;
      }

      var needOverhaul = entry.BurnedOut || entry.OverhaulDue || entry.Suspended;
      if (!needOverhaul)
      {
        continue;
      }

      var bill = Money.From(entry.BurnedOut
        ? registry.QuoteBurnoutOverhaul(entry)
        : registry.QuoteElectiveOverhaul(entry));

      // Keep a small cash floor so premiums can reinstate after yard work.
      var floor = entry.OwnerMaster ? 40m : 120m;
      var canPay = firmLedger.Cash.Amount >= bill.Amount + floor;
      if (!canPay
          && yardLedger.Cash.Amount > 30_000m
          && (entry.BurnedOut || entry.OverhaulDue))
      {
        // Station floats overdue / burnout overhaul — paid as yard revenue from Station cash.
        var advance = bill;
        yardLedger.Post(
          AccountRole.WageExpense, AccountRole.Cash, advance, day, "Overhaul advance");
        firmLedger.Post(
          AccountRole.Cash, AccountRole.Revenue, advance, day, "Overhaul advance");
        canPay = firmLedger.Cash.Amount >= bill.Amount + floor;
        if (canPay)
        {
          milestones.AddOnce(dayIndex, "overhaul-advance", entry.RegistryName);
        }
      }

      if (!canPay)
      {
        continue;
      }

      firmLedger.Post(
        AccountRole.TransportTollExpense, AccountRole.Cash, bill, day, "FTL drive overhaul");
      yardLedger.Post(
        AccountRole.Cash, AccountRole.Revenue, bill, day, "FTL drive overhaul");
      entry.MaintenancePaid += bill.Amount;
      var kind = entry.BurnedOut ? "overhaul-forced" : "overhaul";
      registry.ApplyOverhaul(entry);
      milestones.Add(dayIndex, kind,
        $"{entry.RegistryName} #{entry.OverhaulCount} {bill.Amount:0}");
    }
  }
}
