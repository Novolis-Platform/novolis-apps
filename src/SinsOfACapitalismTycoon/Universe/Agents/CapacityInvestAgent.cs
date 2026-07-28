using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// When manufacturing sites are input-starved and the firm has cash, invest in capacity.
/// </summary>
internal sealed class CapacityInvestAgent : IEconomicAgent
{
  public const decimal MinCash = 6_000m;
  public const decimal UpgradeCost = 800m;
  public const decimal CapacityFactor = 1.12m;
  public const decimal InputStarvation = 4m;
  public const int MinDaysBetweenUpgrades = 12;

  private readonly CampaignWorld.Ids _ids;
  private readonly MilestoneLog _milestones;
  private int _upgrades;
  private int _lastUpgradeDay = -999;

  public CapacityInvestAgent(CampaignWorld.Ids ids, MilestoneLog milestones)
  {
    _ids = ids;
    _milestones = milestones;
    FirmId = ids.Industry;
  }

  public FirmId FirmId { get; }

  public string LastDecision { get; private set; } = "capacity idle";

  public int Upgrades => _upgrades;

  public void Tick(AgentContext context)
  {
    if (context.Clock.HourIndex % SimulationHour.HoursPerDay != 8)
    {
      return;
    }

    var day = context.Clock.Date.DayIndex;
    if (day - _lastUpgradeDay < MinDaysBetweenUpgrades)
    {
      LastDecision = "capacity cooldown";
      return;
    }

    var world = context.World;
    if (!world.Ledgers.TryGetValue(_ids.Industry, out var ledger) || ledger.Cash.Amount < MinCash)
    {
      LastDecision = "capacity thin cash";
      return;
    }

    if (world.IsCreditFrozen(_ids.Industry))
    {
      LastDecision = "capacity credit frozen";
      return;
    }

    // Prefer mining upgrade when parts-starved.
    if (world.Ledgers.TryGetValue(_ids.Mining, out var mineLedger)
        && mineLedger.Cash.Amount >= MinCash
        && !world.IsCreditFrozen(_ids.Mining))
    {
      foreach (var site in _ids.Sites.Values
                 .Where(s => s.Hub.Role == SystemRole.Mining && s.MfgFacility is not null)
                 .OrderBy(s => s.Hub.SystemId, StringComparer.Ordinal))
      {
        var parts = world.Inventory.GetQuantity(
          new InventoryKey(_ids.Mining, site.Hub.LocationId, _ids.Parts)).Value;
        if (parts < InputStarvation)
        {
          context.Enqueue(new UpgradeFacility(site.MfgFacility!.Value, Money.From(UpgradeCost), CapacityFactor));
          Commit(day, $"upgrade mine @{site.Hub.Name}");
          return;
        }
      }
    }

    FacilityId? starvedPlant = null;
    foreach (var site in _ids.Sites.Values
               .Where(s => s.Hub.Role == SystemRole.Industrial && s.MfgFacility is not null)
               .OrderBy(s => s.Hub.SystemId, StringComparer.Ordinal))
    {
      var ore = world.Inventory.GetQuantity(
        new InventoryKey(_ids.Industry, site.Hub.LocationId, _ids.Ore)).Value;
      if (ore < InputStarvation)
      {
        starvedPlant = site.MfgFacility;
        break;
      }
    }

    if (starvedPlant is { } fid)
    {
      context.Enqueue(new UpgradeFacility(fid, Money.From(UpgradeCost), CapacityFactor));
      Commit(day, $"upgrade plant factor {CapacityFactor:0.##}");
      return;
    }

    // Opportunistic expand when very flush, infrequently.
    if (ledger.Cash.Amount >= MinCash * 3m && day % 30 == 0)
    {
      var any = _ids.Sites.Values
        .Where(s => s.Hub.Role == SystemRole.Industrial && s.MfgFacility is not null)
        .OrderBy(s => s.Hub.SystemId, StringComparer.Ordinal)
        .FirstOrDefault();
      if (any?.MfgFacility is { } opp)
      {
        context.Enqueue(new UpgradeFacility(opp, Money.From(UpgradeCost), CapacityFactor));
        Commit(day, "opportunistic plant expand");
        return;
      }
    }

    LastDecision = "capacity wait";
  }

  private void Commit(int day, string decision)
  {
    _upgrades++;
    _lastUpgradeDay = day;
    LastDecision = decision;
    _milestones.Add(day, "upgrade", decision);
  }
}
