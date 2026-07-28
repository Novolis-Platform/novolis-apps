using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Builds library economic agents from the campaign seed (Astro stays in the app).</summary>
internal static class SinsAgents
{
  public sealed class Bundle
  {
    private readonly List<IEconomicAgent> _pulse = [];
    private readonly List<(FirmId Tramp, FirmId Household, string Name)> _ventures = [];

    public required ExtractiveFirmAgent Mining { get; init; }
    public required ManufacturingFirmAgent Industry { get; init; }
    public required RetailFirmAgent Station { get; init; }
    public required CarrierFirmAgent Carrier { get; init; }
    public required List<CarrierFirmAgent> Carriers { get; init; }
    public required CarrierFirmAgent MegaHauler { get; init; }
    public required TreasuryFirmAgent Treasury { get; init; }
    public required IReadOnlyList<HouseholdFirmAgent> Households { get; init; }
    public required SolExportHubAgent SolExport { get; init; }
    public required CapacityInvestAgent Capacity { get; init; }
    public required LoanRepayAgent LoanRepay { get; init; }
    public HouseholdTrampVentureAgent VenturesAgent { get; set; } = null!;
    public MilestoneLog Milestones { get; init; } = null!;
    public ShipBiographyLog Biographies { get; init; } = null!;

    public IReadOnlyList<IEconomicAgent> PulseOrder => _pulse;

    public IReadOnlyList<(FirmId Tramp, FirmId Household, string Name)> Ventures => _ventures;

    public void RebuildPulse()
    {
      _pulse.Clear();
      _pulse.Add(Mining);
      _pulse.Add(Industry);
      _pulse.Add(Station);
      _pulse.AddRange(Carriers);
      _pulse.Add(MegaHauler);
      _pulse.Add(Treasury);
      _pulse.Add(Capacity);
      _pulse.Add(LoanRepay);
      _pulse.AddRange(Households);
      if (SolExportEnabled)
      {
        _pulse.Add(SolExport);
      }

      if (VenturesEnabled)
      {
        _pulse.Add(VenturesAgent);
      }
    }

    public bool SolExportEnabled { get; set; } = true;
    public bool VenturesEnabled { get; set; } = true;

    public void RegisterVenture(
      FirmId tramp,
      FirmId household,
      string name,
      CarrierFirmAgent agent)
    {
      Carriers.Add(agent);
      _ventures.Add((tramp, household, name));
    }
  }

  public static Bundle Create(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones,
    ShipBiographyLog biographies)
  {
    AgentSite Site(CampaignWorld.Site s) => new(
      s.Hub.LocationId, s.Facility, s.Hub.HubId, s.Hub.Name);
    AgentSite MfgSite(CampaignWorld.Site s) => new(
      s.Hub.LocationId, s.MfgFacility, s.Hub.HubId, s.Hub.Name);

    var miningSites = ids.Sites.Values
      .Where(s => s.Hub.Role == SystemRole.Mining && s.MfgFacility is not null)
      .Select(MfgSite)
      .ToList();
    var plantSites = ids.Sites.Values
      .Where(s => s.Hub.Role == SystemRole.Industrial && s.MfgFacility is not null)
      .Select(MfgSite)
      .ToList();
    var retailSites = ids.Sites.Values
      .Where(s => s.Facility is not null
                  && s.Hub.Role is SystemRole.Capital or SystemRole.Inhabited or SystemRole.Mining)
      .Select(Site)
      .ToList();
    var bunkerSites = ids.Sites.Values
      .Where(s => s.Hub.Role is SystemRole.Transit or SystemRole.Capital
        or SystemRole.Industrial or SystemRole.Mining)
      .Select(s => new AgentSite(
        s.Hub.LocationId, s.Facility ?? s.MfgFacility ?? s.CarrierPost, s.Hub.HubId, s.Hub.Name))
      .ToList();
    var allSites = ids.Sites.Values
      .Select(s => new AgentSite(
        s.Hub.LocationId, s.Facility ?? s.MfgFacility ?? s.CarrierPost, s.Hub.HubId, s.Hub.Name))
      .ToList();

    decimal Floor(ProductId p)
    {
      if (p.Equals(ids.Ore)) return CampaignWorld.OreBuy;
      if (p.Equals(ids.Parts)) return CampaignWorld.PartsBuy;
      if (p.Equals(ids.Goods)) return CampaignWorld.GoodsFactory;
      return CampaignWorld.FuelUnitCost;
    }

    decimal Gate(ProductId p) =>
      TapeAwareGatePricing.Gate(sim.State.World.MarketBook, p, Floor(p));

    bool AvoidCongested(TransportHubId hub)
    {
      var world = sim.State.World;
      if (!world.Hubs.TryGetValue(hub, out var h) || h.BerthCapacity <= 0)
      {
        return false;
      }

      var waiting = world.Shipments.Count(s =>
        !s.IsLegacy && s.Phase == ShipmentPhase.WaitingBerth && s.CurrentHubId.Equals(hub));
      return waiting >= h.BerthCapacity;
    }

    var mining = new ExtractiveFirmAgent(ids.Mining, new ExtractiveFirmAgentPolicy(
      miningSites, ids.Ore, ids.Parts,
      BaseOutputRate: 3.5m, OutputCap: CampaignWorld.MineOreCap,
      InputPerOutput: CampaignWorld.PartsPerOre, InputFloor: CampaignWorld.MinePartsFloor,
      SellAboveStock: 8m, SellKeepFloor: 4m, SellMaxQty: 30m,
      OutputGatePrice: CampaignWorld.OreBuy, InputLimitPrice: CampaignWorld.PartsDelivered));

    var industry = new ManufacturingFirmAgent(ids.Industry, new ManufacturingFirmAgentPolicy(
      plantSites, ids.Ore, CampaignWorld.PlantOreFloor + 12m, CampaignWorld.OreDelivered,
      [
        new ManufacturedSkuPolicy(
          ids.Parts, BaseRate: 6m, StockTarget: 55m, MinInputOnHand: 1m, RequiredInput: ids.Ore,
          SellAboveStock: 3m, SellKeepFloor: 2m, SellMaxQty: 24m, GatePrice: CampaignWorld.PartsBuy),
        new ManufacturedSkuPolicy(
          ids.Goods, BaseRate: 5.5m, StockTarget: CampaignWorld.RetailStockTarget,
          MinInputOnHand: 1m, RequiredInput: ids.Parts,
          SellAboveStock: 2m, SellKeepFloor: 1m, SellMaxQty: 28m, GatePrice: CampaignWorld.GoodsFactory),
        new ManufacturedSkuPolicy(
          ids.Fuel, BaseRate: 4m, StockTarget: 72m, MinInputOnHand: 6m, RequiredInput: ids.Ore,
          SellAboveStock: 10m, SellKeepFloor: 4m, SellMaxQty: 24m, GatePrice: CampaignWorld.FuelUnitCost),
      ]));

    var station = new RetailFirmAgent(ids.Station, new RetailFirmAgentPolicy(
      retailSites, bunkerSites,
      [
        new RetailSkuPolicy(
          ids.Goods, CampaignWorld.GoodsSell, CampaignWorld.RetailStockTarget,
          CampaignWorld.GoodsDelivered, PostRetailPrice: true),
      ],
      new BunkerSkuPolicy(
        ids.Fuel, MinStock: 28m, BuyLimitPrice: CampaignWorld.FuelUnitCost * 1.25m,
        SellPrice: CampaignWorld.FuelUnitCost, AllowProcurement: true)));

    var homeHubs = new List<TransportHubId>();
    var mineHomes = miningSites.Where(s => s.HubId is not null).Select(s => s.HubId!.Value).ToList();
    var plantHomes = plantSites.Where(s => s.HubId is not null).Select(s => s.HubId!.Value).ToList();
    var pool = new List<TransportHubId> { ids.Sites["sol"].Hub.HubId };
    pool.AddRange(mineHomes);
    pool.AddRange(plantHomes);
    if (pool.Count == 0)
    {
      pool.Add(ids.Sites["sol"].Hub.HubId);
    }

    for (var i = 0; i < ids.Carriers.Count; i++)
    {
      homeHubs.Add(pool[i % pool.Count]);
    }

    foreach (var (name, firm) in ids.Firms)
    {
      if (ids.Registry.TryGet(firm) is not null)
      {
        biographies.Name(firm, name.StartsWith("Tramp", StringComparison.Ordinal)
          ? ids.Registry.TryGet(firm)!.RegistryName
          : ids.Registry.TryGet(firm)?.RegistryName ?? name);
      }
    }

    foreach (var e in ids.Registry.Entries)
    {
      biographies.Name(e.FirmId, e.RegistryName);
    }

    var trampAgents = new List<CarrierFirmAgent>(ids.Carriers.Count);
    for (var i = 0; i < ids.Carriers.Count; i++)
    {
      var firm = ids.Carriers[i];
      trampAgents.Add(new CarrierFirmAgent(
        firm,
        new CarrierFirmAgentPolicy(
          allSites, [ids.Ore, ids.Parts, ids.Goods], ids.Fuel,
          ids.HullId, ids.Hull, CampaignWorld.MinMargin, Gate,
          FuelBuyLimitPrice: CampaignWorld.FuelUnitCost * 1.5m,
          MinBunkerFuel: 8m,
          ChooseTransitProfile: p => TransitChooser.ForTramp(p, ids),
          CanOperate: () => ids.Registry.CanOperate(firm),
          AvoidHub: AvoidCongested,
          EffectiveMinMargin: () => ids.Reputation.EffectiveMinMargin(firm, CampaignWorld.MinMargin),
          RefuseHaul: (sku, origin, dest, profile) => JumpBandGate.ShouldRefuse(
            sim.State.World, ids, ids.Reputation, ids.Escrow, firm, sku, origin, dest, profile,
            milestones, sim.State.Clock.Date.DayIndex)),
        homeHubs[i],
        rngSalt: 0x43415252UL ^ (ulong)(i + 1) * 0x9E3779B97F4A7C15UL));
    }

    var mega = new CarrierFirmAgent(
      ids.MegaHauler,
      new CarrierFirmAgentPolicy(
        allSites, [ids.Ore, ids.Fuel], ids.Fuel,
        ids.MegaHullId, ids.MegaHull, CampaignWorld.MinMargin * 0.5m, Gate,
        FuelBuyLimitPrice: CampaignWorld.FuelUnitCost * 1.35m,
        MinBunkerFuel: 16m,
        ChooseTransitProfile: p => TransitChooser.ForMegaHauler(p, ids),
        CanOperate: () => ids.Registry.CanOperate(ids.MegaHauler),
        AvoidHub: AvoidCongested),
      ids.Sites["sol"].Hub.HubId,
      rngSalt: 0x4D454741UL);

    var treasury = new TreasuryFirmAgent(ids.Station, new TreasuryFirmAgentPolicy(
      [ids.Mining, ids.Industry, .. ids.Carriers, ids.MegaHauler],
      CashFloorToLend: 4_000m,
      BorrowerCashFloor: CampaignWorld.FirmCashFloor + 400m,
      LoanPrincipal: Money.From(2_000m),
      AnnualInterestRate: 0.06m,
      TermHours: SimulationHour.HoursPerDay * 90,
      MaxActiveLoansToBorrower: 4));

    sim.Enqueue(new OriginateLoan(
      ids.Station, ids.Industry, Money.From(3_000m), 0.06m, SimulationHour.HoursPerDay * 150));

    var households = sim.State.World.Cohorts
      .Where(c => c.Definition.HouseholdFirmId is not null)
      .OrderBy(c => c.Definition.Id.Value)
      .Select(c => new HouseholdFirmAgent(
        c.Definition.HouseholdFirmId!.Value,
        new HouseholdFirmAgentPolicy(
          PreferredBorrower: null,
          PreferredIssuer: ids.Mining,
          PurchaseFraction: 0.01m,
          PurchasePrice: Money.From(40m),
          MaxActiveLoans: 1)))
      .ToList();

    var capacity = new CapacityInvestAgent(ids, milestones);
    var repay = new LoanRepayAgent(ids, milestones);
    var solExport = new SolExportHubAgent(ids);
    var bundle = new Bundle
    {
      Mining = mining,
      Industry = industry,
      Station = station,
      Carrier = trampAgents[0],
      Carriers = trampAgents,
      MegaHauler = mega,
      Treasury = treasury,
      Households = households,
      SolExport = solExport,
      Capacity = capacity,
      LoanRepay = repay,
      VenturesAgent = null!,
      Milestones = milestones,
      Biographies = biographies,
    };
    bundle.VenturesAgent = new HouseholdTrampVentureAgent(ids, bundle, allSites, pool, Gate, milestones, biographies);
    bundle.SolExportEnabled = true;
    bundle.VenturesEnabled = true;
    bundle.RebuildPulse();
    return bundle;
  }
}
