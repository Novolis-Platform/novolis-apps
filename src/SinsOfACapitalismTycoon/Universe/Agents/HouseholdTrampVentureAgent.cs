using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Comfortable households sometimes borrow against a tramp hull (Station term loan) and strike out
/// as owner-operators — entry chaos against the seeded tramp cartel.
/// </summary>
internal sealed class HouseholdTrampVentureAgent : IEconomicAgent
{
  public const int MaxVentures = 2;
  public const decimal VentureChancePerDay = 0.12m;
  public const decimal MinSurplus = 900m;
  public const decimal DownPayment = 400m;
  public const decimal HullLoan = 3_500m;
  public const int EarliestVentureDay = 35;

  private readonly CampaignWorld.Ids _ids;
  private readonly SinsAgents.Bundle _bundle;
  private readonly List<AgentSite> _allSites;
  private readonly List<TransportHubId> _homePool;
  private readonly Func<ProductId, decimal> _gate;
  private readonly MilestoneLog _milestones;
  private readonly ShipBiographyLog _bios;
  private readonly ulong _rngSalt;
  private int _nextVenture;

  public HouseholdTrampVentureAgent(
    CampaignWorld.Ids ids,
    SinsAgents.Bundle bundle,
    List<AgentSite> allSites,
    List<TransportHubId> homePool,
    Func<ProductId, decimal> gate,
    MilestoneLog milestones,
    ShipBiographyLog bios,
    ulong rngSalt = 0x56454E54UL)
  {
    _ids = ids;
    _bundle = bundle;
    _allSites = allSites;
    _homePool = homePool;
    _gate = gate;
    _milestones = milestones;
    _bios = bios;
    _rngSalt = rngSalt;
    FirmId = ids.Station;
  }

  public FirmId FirmId { get; }

  public string LastDecision { get; private set; } = "venture idle";

  public int VenturesStarted => _nextVenture;

  public IReadOnlyList<(FirmId Tramp, FirmId Household, string Name)> Ventures => _bundle.Ventures;

  public void Tick(AgentContext context)
  {
    if (context.Clock.HourIndex % SimulationHour.HoursPerDay != 6)
    {
      LastDecision = "venture wait";
      return;
    }

    if (context.Clock.Date.DayIndex < EarliestVentureDay)
    {
      LastDecision = $"venture delay to d{EarliestVentureDay}";
      return;
    }

    if (_nextVenture >= MaxVentures)
    {
      LastDecision = $"venture cap {_nextVenture}";
      return;
    }

    var world = context.World;
    if (!world.Ledgers.TryGetValue(_ids.Station, out var station)
        || station.Cash.Amount < HullLoan + 2_000m
        || world.IsCreditFrozen(_ids.Station))
    {
      LastDecision = "treasury thin";
      return;
    }

    var rng = new DeterministicRandom(
      context.Simulation.State.Seed ^ _rngSalt ^ (ulong)_nextVenture ^ (ulong)context.Clock.HourIndex);
    var candidates = world.Cohorts
      .Where(c => c.Definition.HouseholdFirmId is not null
                  && world.IsAboveComfort(c)
                  && c.BudgetRemaining.Amount - world.ComfortFloor(c).Amount >= MinSurplus)
      .OrderByDescending(c => c.BudgetRemaining.Amount)
      .ToList();

    if (candidates.Count == 0)
    {
      LastDecision = "no comfortable HH";
      return;
    }

    var pick = candidates[Math.Min(candidates.Count - 1, rng.NextInt(Math.Min(5, candidates.Count)))];
    if ((decimal)rng.NextDouble() >= VentureChancePerDay)
    {
      LastDecision = "venture pass";
      return;
    }

    var hh = pick.Definition.HouseholdFirmId!.Value;
    if (_bundle.Ventures.Any(v => v.Household.Equals(hh)))
    {
      LastDecision = "already ventured";
      return;
    }

    var n = _nextVenture + 1;
    var trampId = FirmId.From(Guid.Parse($"00000000-0000-4000-8000-00000000{(0xd0 + _nextVenture):x4}"));
    var name = $"MV Prospect {n}";
    world.EnsureFirm(trampId, name);

    if (_ids.Sites.TryGetValue("sol", out var sol))
    {
      world.Inventory.Add(
        new InventoryKey(trampId, sol.Hub.LocationId, _ids.Fuel),
        new ProductBatch(
          _ids.Fuel,
          Quantity.From(18m),
          new ProductQuality(100m),
          Money.From(CampaignWorld.FuelUnitCost),
          context.Clock.Date,
          BrandId: null),
        bypassLimits: true);
    }

    _ids.Registry.Register(ShipRegistryEntry.Create(
      trampId, name, "LightCommercial", ownerMaster: true, lienPrincipal: HullLoan));
    _ids.Desk.Firms.Register(FirmRegistryEntry.Create(trampId, $"{name} Co."));
    _ids.Desk.Licenses.Register(LicenseRegistryEntry.Create(
      LicenseRegistryEntry.IdFor(trampId, "priority-freight"),
      $"{name} Priority endorsement",
      scope: "priority-freight",
      holderSubjectId: trampId.Value));
    _bios.Name(trampId, name);

    bool AvoidCongested(TransportHubId hub)
    {
      if (!world.Hubs.TryGetValue(hub, out var h) || h.BerthCapacity <= 0)
      {
        return false;
      }

      var waiting = world.Shipments.Count(s =>
        !s.IsLegacy && s.Phase == ShipmentPhase.WaitingBerth && s.CurrentHubId.Equals(hub));
      return waiting >= h.BerthCapacity;
    }

    var home = _homePool[_nextVenture % _homePool.Count];
    var agent = new CarrierFirmAgent(
      trampId,
      new CarrierFirmAgentPolicy(
        _allSites, [_ids.Ore, _ids.Parts, _ids.Goods], _ids.Fuel,
        _ids.HullId, _ids.Hull, CampaignWorld.MinMargin, _gate,
        FuelBuyLimitPrice: CampaignWorld.FuelUnitCost * 1.5m,
        MinBunkerFuel: 8m,
        ChooseTransitProfile: p => TransitChooser.ForTramp(p, _ids),
        CanOperate: () => _ids.Registry.CanOperate(trampId),
        AvoidHub: AvoidCongested,
        EffectiveMinMargin: () => _ids.Reputation.EffectiveMinMargin(trampId, CampaignWorld.MinMargin),
        RefuseHaul: (sku, origin, dest, profile) => JumpBandGate.ShouldRefuse(
          world, _ids, _ids.Reputation, _ids.Escrow, trampId, sku, origin, dest, profile,
          _milestones, context.Clock.Date.DayIndex)),
      home,
      rngSalt: 0x50524F53UL ^ (ulong)n * 0x9E3779B97F4A7C15UL);

    context.Enqueue(new OriginateLoan(
      _ids.Station,
      trampId,
      Money.From(HullLoan),
      AnnualInterestRate: 0.09m,
      TermHours: SimulationHour.HoursPerDay * 180));

    context.Enqueue(new PurchaseOwnership(
      trampId, hh, Fraction: 0.85m, Price: Money.From(DownPayment)));

    _bundle.RegisterVenture(trampId, hh, name, agent);
    _nextVenture++;
    LastDecision = $"venture {name} hh-loan {HullLoan:0}";
    _milestones.Add(context.Clock.Date.DayIndex, "venture", name);
  }
}
