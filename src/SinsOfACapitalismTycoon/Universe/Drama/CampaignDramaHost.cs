using Novolis.Economy;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Seed-deterministic campaign shocks + Calypso dock weather.
/// Ugly standby lives in <see cref="OpportunitiesPool"/>; reputation bumps on known-responsive.
/// </summary>
internal sealed class CampaignDramaHost
{
  private readonly CampaignWorld.Ids _ids;
  private readonly MilestoneLog _milestones;
  private readonly OpportunitiesPool _opportunities;
  private readonly ReputationLedger _reputation;
  private readonly bool _enabled;
  private readonly HashSet<int> _fired = [];
  private int? _capitalBerthsSaved;
  private long? _capitalDwellSaved;
  private int _emptyBerthUntilDay = -1;

  public CampaignDramaHost(
    CampaignWorld.Ids ids,
    MilestoneLog milestones,
    OpportunitiesPool opportunities,
    ReputationLedger reputation,
    bool enabled = true)
  {
    _ids = ids;
    _milestones = milestones;
    _opportunities = opportunities;
    _reputation = reputation;
    _enabled = enabled;
  }

  public void TickHour(EconomySimulation sim)
  {
    if (!_enabled)
    {
      return;
    }

    var day = sim.State.Clock.Date.DayIndex;
    var hourOfDay = (int)(sim.State.Clock.HourIndex % SimulationHour.HoursPerDay);

    RestoreEmptyBerthIfDue(sim, day);
    _opportunities.TickHour(sim, sim.State.Seed);

    if (hourOfDay != 12)
    {
      return;
    }

    if (day == 12 && _fired.Add(12))
    {
      EmptyBerth(sim, day);
    }

    if (day == 25 && _fired.Add(25))
    {
      FuelFamine(sim, "Transit");
      _milestones.Add(day, "fuel-famine", "Transit bunker drought");
    }

    if (day == 40 && _fired.Add(40))
    {
      ProductionShock(sim);
      _milestones.Add(day, "shock", "Industrial ore loss shock");
    }

    if (day == 55 && _fired.Add(55))
    {
      FiscalBleed(sim);
      _milestones.Add(day, "fiscal", "Station household transfer bleed");
    }

    if (day == 70 && _fired.Add(70))
    {
      FuelFamine(sim, "Mining");
      _milestones.Add(day, "fuel-famine", "Mining bunker drought");
    }
  }

  public void TickDayEnd(EconomySimulation sim) =>
    _opportunities.TickDayEnd(sim, sim.State.Clock.Date.DayIndex);

  private void EmptyBerth(EconomySimulation sim, int day)
  {
    if (!_ids.Sites.TryGetValue("sol", out var sol))
    {
      return;
    }

    if (!sim.State.World.Hubs.TryGetValue(sol.Hub.HubId, out var hub))
    {
      return;
    }

    _capitalBerthsSaved = hub.BerthCapacity;
    _capitalDwellSaved = hub.DwellHours;
    sim.State.World.Hubs[hub.Id] = hub with { BerthCapacity = 1, DwellHours = 18 };
    _emptyBerthUntilDay = day + 3;
    _milestones.Add(day, "empty-berth",
      "Capital berths withdrawn (autonomous handoff) — formal plan failed");
  }

  private void RestoreEmptyBerthIfDue(EconomySimulation sim, int day)
  {
    if (_emptyBerthUntilDay < 0 || day < _emptyBerthUntilDay
        || _capitalBerthsSaved is not { } savedBerths
        || _capitalDwellSaved is not { } savedDwell)
    {
      return;
    }

    if (!_ids.Sites.TryGetValue("sol", out var sol)
        || !sim.State.World.Hubs.TryGetValue(sol.Hub.HubId, out var hub))
    {
      return;
    }

    sim.State.World.Hubs[hub.Id] = hub with
    {
      BerthCapacity = savedBerths,
      DwellHours = savedDwell,
    };
    _emptyBerthUntilDay = -1;
    _capitalBerthsSaved = null;
    _capitalDwellSaved = null;
    _milestones.Add(day, "known-responsive",
      "Capital berths restored — listed operators still on the board");
    foreach (var c in _ids.Carriers.Where(_ids.Registry.CanOperate))
    {
      _reputation.ObserveKnownResponsive(c);
    }
  }

  private void FuelFamine(EconomySimulation sim, string roleName)
  {
    var world = sim.State.World;
    var role = roleName switch
    {
      "Mining" => SystemRole.Mining,
      "Transit" => SystemRole.Transit,
      _ => SystemRole.Waypoint,
    };

    foreach (var site in _ids.Sites.Values.Where(s => s.Hub.Role == role))
    {
      foreach (var firm in new[] { _ids.Station, _ids.Industry }.Concat(_ids.Carriers).Append(_ids.MegaHauler))
      {
        var key = new InventoryKey(firm, site.Hub.LocationId, _ids.Fuel);
        var qty = world.Inventory.GetQuantity(key);
        if (qty.Value <= 0m)
        {
          continue;
        }

        var take = Quantity.From(Math.Max(0m, qty.Value * 0.85m));
        world.Inventory.TryTake(key, take, out _, out _);
      }
    }
  }

  private void ProductionShock(EconomySimulation sim)
  {
    var world = sim.State.World;
    var taken = 0m;
    foreach (var plant in _ids.Sites.Values
               .Where(s => s.Hub.Role is SystemRole.Industrial or SystemRole.Mining)
               .OrderBy(s => s.Hub.SystemId, StringComparer.Ordinal))
    {
      var key = new InventoryKey(_ids.Industry, plant.Hub.LocationId, _ids.Ore);
      var qty = world.Inventory.GetQuantity(key);
      if (qty.Value <= 0m)
      {
        continue;
      }

      var takeQty = Quantity.From(Math.Min(qty.Value, 40m - taken));
      if (takeQty.Value <= 0m)
      {
        break;
      }

      if (world.Inventory.TryTake(key, takeQty, out _, out _))
      {
        taken += takeQty.Value;
      }

      if (taken >= 40m)
      {
        break;
      }
    }

    var lossUnits = Math.Max(taken, 35m);
    if (!world.Ledgers.TryGetValue(_ids.Station, out var uw)
        || !world.Ledgers.TryGetValue(_ids.Industry, out var ind))
    {
      return;
    }

    var gross = Money.From(lossUnits * 12m);
    var net = Money.From(Math.Max(0m, gross.Amount - 40m));
    if (net.Amount <= 0m || uw.Cash.Amount + 0.0001m < net.Amount)
    {
      _milestones.Add(sim.State.Clock.Date.DayIndex, "claim",
        "underwriter short production-loss");
      _ids.Registry.ActuarialLoad = Math.Max(_ids.Registry.ActuarialLoad, 1.5m);
      return;
    }

    uw.Post(
      Novolis.Economy.Accounting.AccountRole.CostOfGoodsSold,
      Novolis.Economy.Accounting.AccountRole.Cash,
      net,
      sim.State.Clock.Date,
      "Production loss claim");
    ind.Post(
      Novolis.Economy.Accounting.AccountRole.Cash,
      Novolis.Economy.Accounting.AccountRole.Revenue,
      net,
      sim.State.Clock.Date,
      "Production loss claim");
    _ids.Registry.ClaimsPaid += net.Amount;
    _ids.Registry.ActuarialLoad = Math.Max(_ids.Registry.ActuarialLoad, 1.15m);
    _milestones.Add(sim.State.Clock.Date.DayIndex, "claim",
      $"production-loss claim {net.Amount:0} units {taken:0} (stock not restored)");
  }

  private void FiscalBleed(EconomySimulation sim)
  {
    var world = sim.State.World;
    if (!world.Ledgers.TryGetValue(_ids.Station, out var station) || station.Cash.Amount < 2_000m)
    {
      return;
    }

    var bleed = Money.From(Math.Min(2_500m, station.Cash.Amount * 0.12m));
    station.Post(
      Novolis.Economy.Accounting.AccountRole.WageExpense,
      Novolis.Economy.Accounting.AccountRole.Cash,
      bleed,
      sim.State.Clock.Date,
      "Fiscal household transfer");
    var cohorts = world.Cohorts.Where(c => c.Definition.Population.Value > 0).ToList();
    var heads = cohorts.Sum(c => (decimal)c.Definition.Population.Value);
    if (heads <= 0)
    {
      return;
    }

    foreach (var c in cohorts)
    {
      var share = bleed.Amount * ((decimal)c.Definition.Population.Value / heads);
      c.BudgetRemaining = Money.From(c.BudgetRemaining.Amount + share);
    }
  }
}
