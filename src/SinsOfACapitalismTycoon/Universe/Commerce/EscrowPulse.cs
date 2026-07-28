using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// CCA escrow: hold Ops cash until delivery; 5% issuer fee; ≥10% contractor skim to underwriter.
/// Clawback on cancel. Never touches Core books.
/// </summary>
internal sealed class EscrowBook
{
  public sealed record OpenEscrow(
    Guid ShipmentKey,
    FirmId Carrier,
    FirmId Buyer,
    Money Principal,
    Money IssuerFee,
    ProductId Product,
    int OpenedDay);

  private readonly Dictionary<Guid, OpenEscrow> _open = new();
  private readonly HashSet<Guid> _seenShipments = [];

  public int OpenCount => _open.Count;
  public decimal ReleasedTotal { get; private set; }
  public decimal ClawedTotal { get; private set; }
  public decimal IssuerFeesTotal { get; private set; }
  public decimal ContractorSkimTotal { get; private set; }

  public bool FirmHasOpen(FirmId firm) => _open.Values.Any(e => e.Carrier.Equals(firm));

  public void TickDay(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    var world = sim.State.World;
    var day = sim.State.Clock.Date.DayIndex;
    var date = sim.State.Clock.Date;

    foreach (var ship in world.Shipments.Where(s => !s.IsLegacy).ToList())
    {
      if (ship.Phase is ShipmentPhase.Underway or ShipmentPhase.Loading or ShipmentPhase.WaitingBerth)
      {
        TryOpen(world, ids, ship, day, date, milestones);
      }

      if (ship.Phase == ShipmentPhase.Cancelled && _open.ContainsKey(ship.Id.Value))
      {
        Clawback(world, ids, ship.Id.Value, day, date, milestones);
      }
    }

    foreach (var ev in sim.State.Events.TakeLast(80))
    {
      if (ev is not ShipmentDelivered delivered)
      {
        continue;
      }

      var key = FindOpenKey(delivered.FirmId, delivered.ProductId);
      if (key is null)
      {
        continue;
      }

      Release(world, ids, key.Value, _open[key.Value], day, date, milestones, clawback: false);
    }
  }

  private Guid? FindOpenKey(FirmId carrier, ProductId product)
  {
    foreach (var (k, e) in _open)
    {
      if (e.Carrier.Equals(carrier) && e.Product.Equals(product))
      {
        return k;
      }
    }

    return null;
  }

  private void TryOpen(
    EconomyWorld world,
    CampaignWorld.Ids ids,
    ActiveShipment ship,
    int day,
    SimulationDate date,
    MilestoneLog milestones)
  {
    if (!_seenShipments.Add(ship.Id.Value) || _open.ContainsKey(ship.Id.Value))
    {
      return;
    }

    if (ship.Quantity.Value <= 0m)
    {
      return; // Empty reposition — no CCA escrow.
    }

    var entry = ids.Registry.TryGet(ship.FirmId);
    if (entry is null || !entry.OwnerMaster)
    {
      return;
    }

    var unit = UnitValue(ship.ProductId, ids);
    var principal = Money.From(Math.Max(40m, ship.Quantity.Value * unit));
    var issuerFee = Money.From(Math.Round(principal.Amount * 0.05m, 2, MidpointRounding.AwayFromZero));
    var buyer = PreferBuyer(ship.ProductId, ids);
    var total = Money.From(principal.Amount + issuerFee.Amount);

    if (!world.Ledgers.TryGetValue(buyer, out var buyerLedger)
        || !world.Ledgers.TryGetValue(ids.Station, out var station)
        || buyerLedger.Cash.Amount + 0.0001m < total.Amount)
    {
      _seenShipments.Remove(ship.Id.Value);
      return;
    }

    // Buyer funds escrow; Station holds principal in cash and books issuer fee.
    buyerLedger.Post(AccountRole.TransportTollExpense, AccountRole.Cash, total, date, "CCA escrow hold");
    station.Post(AccountRole.Cash, AccountRole.Revenue, issuerFee, date, "CCA issuer fee 5%");
    station.Post(AccountRole.Cash, AccountRole.Revenue, principal, date, "CCA escrow custody");
    IssuerFeesTotal += issuerFee.Amount;
    _open[ship.Id.Value] = new OpenEscrow(
      ship.Id.Value, ship.FirmId, buyer, principal, issuerFee, ship.ProductId, day);
    milestones.AddOnce(day, "escrow",
      $"open {entry.RegistryName} {principal.Amount:0} (+fee {issuerFee.Amount:0})");
  }

  private void Clawback(
    EconomyWorld world,
    CampaignWorld.Ids ids,
    Guid key,
    int day,
    SimulationDate date,
    MilestoneLog milestones)
  {
    if (!_open.TryGetValue(key, out var esc))
    {
      return;
    }

    Release(world, ids, key, esc, day, date, milestones, clawback: true);
  }

  private void Release(
    EconomyWorld world,
    CampaignWorld.Ids ids,
    Guid key,
    OpenEscrow esc,
    int day,
    SimulationDate date,
    MilestoneLog milestones,
    bool clawback)
  {
    if (!_open.Remove(key))
    {
      return;
    }

    if (!world.Ledgers.TryGetValue(ids.Station, out var station))
    {
      return;
    }

    if (clawback)
    {
      if (world.Ledgers.TryGetValue(esc.Buyer, out var buyer)
          && station.Cash.Amount + 0.0001m >= esc.Principal.Amount)
      {
        station.Post(AccountRole.WageExpense, AccountRole.Cash, esc.Principal, date, "CCA escrow clawback");
        buyer.Post(AccountRole.Cash, AccountRole.Revenue, esc.Principal, date, "CCA escrow clawback");
      }

      ClawedTotal += esc.Principal.Amount;
      if (ids.Registry.TryGet(esc.Carrier) is { } entry)
      {
        entry.LienPrincipal += Math.Round(esc.Principal.Amount * 0.15m, 2);
      }

      milestones.AddOnce(day, "escrow",
        $"clawback {esc.Principal.Amount:0} {ids.Registry.TryGet(esc.Carrier)?.RegistryName}");
      return;
    }

    var skim = Money.From(Math.Max(esc.Principal.Amount * 0.10m, 20m));
    var toCarrier = Money.From(Math.Max(0m, esc.Principal.Amount - skim.Amount));
    if (station.Cash.Amount + 0.0001m < toCarrier.Amount)
    {
      return;
    }

    // Pay carrier (principal − skim); skim remains Station/underwriter revenue from custody.
    station.Post(AccountRole.WageExpense, AccountRole.Cash, toCarrier, date, "CCA escrow release");
    if (world.Ledgers.TryGetValue(esc.Carrier, out var carrier))
    {
      carrier.Post(AccountRole.Cash, AccountRole.Revenue, toCarrier, date, "CCA escrow payout");
    }

    ContractorSkimTotal += skim.Amount;
    ReleasedTotal += toCarrier.Amount;
    milestones.AddOnce(day, "escrow",
      $"release {toCarrier.Amount:0} skim {skim.Amount:0} {ids.Registry.TryGet(esc.Carrier)?.RegistryName}");
  }

  private static FirmId PreferBuyer(ProductId product, CampaignWorld.Ids ids)
  {
    if (product.Equals(ids.Goods) || product.Equals(ids.Fuel))
    {
      return ids.Station;
    }

    return ids.Industry;
  }

  private static decimal UnitValue(ProductId product, CampaignWorld.Ids ids)
  {
    if (product.Equals(ids.Ore)) return CampaignWorld.OreDelivered;
    if (product.Equals(ids.Parts)) return CampaignWorld.PartsDelivered;
    if (product.Equals(ids.Goods)) return CampaignWorld.GoodsDelivered;
    if (product.Equals(ids.Fuel)) return CampaignWorld.FuelUnitCost;
    return 8m;
  }
}
