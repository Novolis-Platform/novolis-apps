using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Gates Calypso's <see cref="CarrierFirmAgent"/>: orders first, else idle ("awaiting James"),
/// unless autopilot is on (then the inner AI plans hauls).
/// </summary>
internal sealed class PlayerTrampAgent : IEconomicAgent
{
  private readonly CarrierFirmAgent _inner;
  private readonly PlayerControlState _state;
  private readonly CampaignWorld.Ids _ids;

  public PlayerTrampAgent(
    CarrierFirmAgent inner,
    PlayerControlState state,
    CampaignWorld.Ids ids)
  {
    _inner = inner;
    _state = state;
    _ids = ids;
    FirmId = inner.FirmId;
  }

  public FirmId FirmId { get; }

  public string LastDecision { get; private set; } = "awaiting James";

  public CarrierFirmAgent Inner => _inner;

  public TransportHubId CurrentHub => _inner.CurrentHub;

  public void Tick(AgentContext context)
  {
    if (!_state.Enabled || _state.Autopilot)
    {
      _inner.Tick(context);
      LastDecision = _inner.LastDecision;
      return;
    }

    if (_state.Orders.TryDequeue(out var order))
    {
      Execute(order, context);
      return;
    }

    var world = context.World;
    var ship = world.Shipments.FirstOrDefault(s =>
      !s.IsLegacy && s.FirmId.Equals(FirmId) && s.Status == ShipmentStatus.InTransit);
    if (ship is not null)
    {
      _inner.Tick(context);
      LastDecision = ship.Quantity.Value <= 0m
        ? $"reposition @ {HubName(context, ship.CurrentHubId)} [{ship.Phase}]"
        : _inner.LastDecision;
      return;
    }

    if (world.PendingPlanShipments.Any(p => p.FirmId.Equals(FirmId))
        || world.PendingPlanRepositions.Any(p => p.FirmId.Equals(FirmId)))
    {
      LastDecision = "awaiting departure";
      return;
    }

    var fail = context.Simulation.State.Events.OfType<ShipmentPlanFailed>()
      .LastOrDefault(e => e.FirmId.Equals(FirmId));
    if (fail is not null
        && fail.Hour.HourIndex >= context.Clock.HourIndex - 1
        && (fail.Reason.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            || fail.Reason.Contains("path", StringComparison.OrdinalIgnoreCase)
            || fail.Reason.Contains("depart", StringComparison.OrdinalIgnoreCase)
            || fail.Reason.Contains("hub", StringComparison.OrdinalIgnoreCase)))
    {
      LastDecision = $"travel failed ({fail.Reason})";
      return;
    }

    LastDecision = "awaiting James";
  }

  private void Execute(PlayerOrder order, AgentContext context)
  {
    switch (order.Kind)
    {
      case PlayerOrderKind.Wait:
        LastDecision = "waiting";
        return;
      case PlayerOrderKind.SetDefaultProfile:
        _state.DefaultProfile = order.Profile;
        LastDecision = $"profile {_state.DefaultProfile}";
        return;
      case PlayerOrderKind.AcceptStandby:
        LastDecision = _state.Opportunities?.ActiveStandbyTramp?.Equals(FirmId) == true
          ? "standby accepted — work the window"
          : "no standby offer";
        return;
      case PlayerOrderKind.RefuseStandby:
        LastDecision = _state.Opportunities?.TryRefuse(FirmId, context.Simulation.State.Clock.Date.DayIndex) == true
          ? "standby refused (≠ premium)"
          : "no standby to refuse";
        return;
      case PlayerOrderKind.PayPremium:
        LastDecision = TryPayPremium(context) ? "premium paid" : "premium unpaid — cash short";
        return;
      case PlayerOrderKind.RequestOverhaul:
        LastDecision = TryElectiveOverhaul(context) ? "overhaul booked" : "overhaul refused — cash/yard";
        return;
      case PlayerOrderKind.TravelTo:
        Travel(order, context);
        return;
      case PlayerOrderKind.CommitSpot:
        CommitSpot(order, context);
        return;
      case PlayerOrderKind.DepartManifest:
        DepartManifest(order, context);
        return;
      default:
        LastDecision = "unknown order";
        break;
    }
  }

  private void Travel(PlayerOrder order, AgentContext context)
  {
    if (_ids.Registry.CanOperate(FirmId) is false)
    {
      LastDecision = "registry hold — cannot operate";
      return;
    }

    if (string.IsNullOrWhiteSpace(order.DestSystemId)
        || !_ids.Sites.TryGetValue(order.DestSystemId, out var destSite))
    {
      LastDecision = "travel dest unknown";
      return;
    }

    var originHub = CurrentHub;
    var destHub = destSite.Hub.HubId;
    if (originHub.Equals(destHub))
    {
      LastDecision = "already at berth";
      return;
    }

    if (context.World.Shipments.Any(s =>
          !s.IsLegacy && s.FirmId.Equals(FirmId) && s.Status == ShipmentStatus.InTransit)
        || context.World.PendingPlanShipments.Any(p => p.FirmId.Equals(FirmId))
        || context.World.PendingPlanRepositions.Any(p => p.FirmId.Equals(FirmId)))
    {
      LastDecision = "hull busy — cannot travel";
      return;
    }

    var profile = order.Profile;
    if (!ItineraryPlanner.TryPlan(
          originHub, destHub, Quantity.Zero, _ids.Hull, context.World.Corridors, out var itinerary, profile)
        || itinerary.LegCount == 0)
    {
      LastDecision = $"no route → {destSite.Hub.Name}";
      return;
    }

    if (!context.World.Hubs.TryGetValue(originHub, out var originHubObj))
    {
      LastDecision = "origin hub unknown";
      return;
    }

    // Bunker first-leg fuel at berth (AI tramp does the same before PlanShipment).
    var first = context.World.Corridors[itinerary.CorridorIds[0]];
    var need = ItineraryPlanner.FuelBurnForLeg(first, _ids.Hull, profile).Value;
    need = Math.Min(Math.Max(need, 8m), _ids.Hull.FuelTankCapacity.Value);
    var fuelKey = new InventoryKey(FirmId, originHubObj.LocationId, _ids.Fuel);
    var have = context.World.Inventory.GetQuantity(fuelKey).Value;
    if (have + 0.0000001m < need)
    {
      var buyQty = Math.Max(12m, need - have + 4m);
      context.Enqueue(new PostHubOrder(
        FirmId, originHubObj.LocationId, _ids.Fuel, HubOrderSide.Buy,
        Quantity.From(buyQty), Money.From(CampaignWorld.FuelUnitCost * 1.5m)));
      context.Enqueue(new PlaceProcurementOrder(
        FirmId, originHubObj.LocationId, _ids.Fuel, Quantity.From(Math.Max(8m, need - have + 2m)),
        Money.From(CampaignWorld.FuelUnitCost * 1.8m)));
      // Defer reposition one tick so MatchHubOrders / procure can fill the tank.
      _state.Orders.Enqueue(new PlayerOrder(
        PlayerOrderKind.TravelTo, DestSystemId: order.DestSystemId, Profile: profile));
      LastDecision = $"bunkering for → {destSite.Hub.Name}";
      return;
    }

    context.Enqueue(new PlanReposition(
      FirmId, originHub.Value, destHub.Value, _ids.HullId.Value, (int)profile));
    LastDecision = $"travel → {destSite.Hub.Name} [{profile}]";
  }

  private void CommitSpot(PlayerOrder order, AgentContext context)
  {
    _ = context;
    if (_ids.Registry.CanOperate(FirmId) is false)
    {
      LastDecision = "registry hold — cannot operate";
      return;
    }

    if (string.IsNullOrWhiteSpace(order.OriginSystemId)
        || string.IsNullOrWhiteSpace(order.DestSystemId)
        || string.IsNullOrWhiteSpace(order.SkuLabel)
        || order.Quantity < 1m)
    {
      LastDecision = "spot order incomplete";
      return;
    }

    if (!_ids.Sites.TryGetValue(order.OriginSystemId, out var originSite))
    {
      LastDecision = "load berth unknown";
      return;
    }

    if (!originSite.Hub.HubId.Equals(CurrentHub))
    {
      LastDecision = "not at load berth — travel first";
      return;
    }

    var product = ResolveSku(order.SkuLabel);
    if (product is null)
    {
      LastDecision = $"unknown sku {order.SkuLabel}";
      return;
    }

    var qty = Math.Min(order.Quantity, _state.Manifest.Room);
    if (qty < 1m)
    {
      LastDecision = "hold full";
      return;
    }

    if (!_state.Manifest.TryAdd(
          order.OriginSystemId,
          order.DestSystemId,
          order.SkuLabel,
          product.Value,
          qty,
          order.LiftLimit,
          order.DestBid,
          order.Profile,
          out var fail))
    {
      LastDecision = $"manifest refused ({fail})";
      return;
    }

    LastDecision =
      $"manifest +{order.SkuLabel} ×{qty:0} ({_state.Manifest.Used:0}/{CampaignWorld.HullCargoCapacity:0})";
  }

  private void DepartManifest(PlayerOrder order, AgentContext context)
  {
    if (_ids.Registry.CanOperate(FirmId) is false)
    {
      LastDecision = "registry hold — cannot operate";
      return;
    }

    var lot = _state.Manifest.TakeForDepart(order.SkuLabel);
    if (lot is null)
    {
      LastDecision = "manifest empty";
      return;
    }

    if (!_ids.Sites.TryGetValue(lot.OriginSystemId, out var originSite)
        || !_ids.Sites.TryGetValue(lot.DestSystemId, out var destSite))
    {
      LastDecision = "manifest hubs unknown";
      return;
    }

    if (!originSite.Hub.HubId.Equals(CurrentHub))
    {
      // Put it back — shouldn't happen if accept was berth-gated.
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      LastDecision = "not at load berth";
      return;
    }

    if (JumpBandGate.ShouldRefuse(
          context.World, _ids, _ids.Reputation, _ids.Escrow, FirmId, lot.ProductId,
          originSite.Hub.HubId, destSite.Hub.HubId, lot.Profile, null,
          context.Simulation.State.Clock.Date.DayIndex))
    {
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      LastDecision = "jump-band refuse";
      return;
    }

    var lift = lot.LiftLimit > 0m ? lot.LiftLimit : CampaignWorld.OreBuy;
    context.Enqueue(new PostHubOrder(
      FirmId, originSite.Hub.LocationId, lot.ProductId, HubOrderSide.Buy,
      Quantity.From(lot.Quantity), Money.From(lift)));
    context.Enqueue(new PlanShipment(
      FirmId, originSite.Hub.HubId.Value, destSite.Hub.HubId.Value,
      lot.ProductId, Quantity.From(lot.Quantity), _ids.HullId.Value, (int)lot.Profile));
    LastDecision =
      $"depart {lot.SkuLabel} {originSite.Hub.Name}→{destSite.Hub.Name} ×{lot.Quantity:0} [{lot.Profile}]";
  }

  private ProductId? ResolveSku(string label) =>
    label.Trim().ToLowerInvariant() switch
    {
      "raw" or "ore" => _ids.Ore,
      "capital" or "parts" => _ids.Parts,
      "final" or "goods" => _ids.Goods,
      "energy" or "fuel" => _ids.Fuel,
      _ => null
    };

  private static string HubName(AgentContext context, TransportHubId id) =>
    context.World.Hubs.TryGetValue(id, out var h) ? h.Name : "?";

  private bool TryPayPremium(AgentContext context)
  {
    var entry = _ids.Registry.TryGet(FirmId);
    if (entry is null
        || !context.World.Ledgers.TryGetValue(FirmId, out var firm)
        || !context.World.Ledgers.TryGetValue(_ids.Registry.Underwriter, out var uw))
    {
      return false;
    }

    var quote = Money.From(Math.Max(14m, _ids.Registry.QuoteDailyPremium(entry)));
    if (firm.Cash.Amount + 0.0001m < quote.Amount)
    {
      return false;
    }

    var day = context.Simulation.State.Clock.Date;
    firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, quote, day, "Hull insurance premium");
    uw.Post(AccountRole.Cash, AccountRole.Revenue, quote, day, "Hull insurance premium");
    entry.PremiumPaid += quote.Amount;
    entry.Insured = true;
    entry.PremiumArrearsDays = 0;
    entry.Suspended = false;
    return true;
  }

  private bool TryElectiveOverhaul(AgentContext context)
  {
    var entry = _ids.Registry.TryGet(FirmId);
    if (entry is null
        || !context.World.Ledgers.TryGetValue(FirmId, out var firm)
        || !context.World.Ledgers.TryGetValue(_ids.Registry.Underwriter, out var yard))
    {
      return false;
    }

    var bill = Money.From(_ids.Registry.QuoteElectiveOverhaul(entry));
    if (firm.Cash.Amount < bill.Amount + 40m)
    {
      return false;
    }

    var day = context.Simulation.State.Clock.Date;
    firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, bill, day, "FTL drive overhaul");
    yard.Post(AccountRole.Cash, AccountRole.Revenue, bill, day, "FTL drive overhaul");
    entry.MaintenancePaid += bill.Amount;
    _ids.Registry.ApplyOverhaul(entry);
    return true;
  }
}
