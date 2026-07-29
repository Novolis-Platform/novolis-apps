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
      if (_state is { Enabled: true, Autopilot: true })
      {
        var queued = SurvivalCaptain.Tick(this, context, _ids, _state);
        if (queued && _state.Orders.TryDequeue(out var survivalOrder))
        {
          Execute(survivalOrder, context);
          return;
        }
      }

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
      _state.LastAction = PlayerActionResult.Fail(
        "travel", PlayerActionErrorCodes.PlanFailed, LastDecision);
      return;
    }

    LastDecision = "awaiting James";
  }

  private void Execute(PlayerOrder order, AgentContext context)
  {
    _state.IntentStack.MarkActive(order.Kind);
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
        LastDecision = TryPayPremium(context) ? "premium settled" : "premium unpaid — cash short";
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
      case PlayerOrderKind.MarketBuy:
        MarketBuy(order, context);
        return;
      case PlayerOrderKind.MarketSell:
        MarketSell(order, context);
        return;
      default:
        LastDecision = "unknown order";
        break;
    }
  }

  private void Travel(PlayerOrder order, AgentContext context)
  {
    const string action = "travel";
    if (_ids.Registry.CanOperate(FirmId) is false)
    {
      LastDecision = "registry hold — cannot operate";
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.Registry, LastDecision);
      return;
    }

    if (string.IsNullOrWhiteSpace(order.DestSystemId)
        || !_ids.Sites.TryGetValue(order.DestSystemId, out var destSite))
    {
      LastDecision = "travel dest unknown";
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.UnknownDest, LastDecision);
      return;
    }

    var originHub = CurrentHub;
    var destHub = destSite.Hub.HubId;
    if (originHub.Equals(destHub))
    {
      LastDecision = "already at dock";
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.AlreadyHere, LastDecision);
      return;
    }

    if (context.World.Shipments.Any(s =>
          !s.IsLegacy && s.FirmId.Equals(FirmId) && s.Status == ShipmentStatus.InTransit)
        || context.World.PendingPlanShipments.Any(p => p.FirmId.Equals(FirmId))
        || context.World.PendingPlanRepositions.Any(p => p.FirmId.Equals(FirmId)))
    {
      LastDecision = "hull busy — cannot travel";
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.Busy, LastDecision);
      return;
    }

    var profile = order.Profile;
    if (!ItineraryPlanner.TryPlan(
          originHub, destHub, Quantity.Zero, _ids.Hull, context.World.Corridors, out var itinerary, profile)
        || itinerary.LegCount == 0)
    {
      LastDecision = $"no route → {destSite.Hub.Name}";
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.NoRoute, LastDecision);
      return;
    }

    if (!context.World.Hubs.TryGetValue(originHub, out var originHubObj))
    {
      LastDecision = "origin hub unknown";
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.OriginUnknown, LastDecision);
      return;
    }

    // Bunker first-leg fuel at dock (AI tramp does the same before PlanShipment).
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
      _state.LastAction = PlayerActionResult.Fail(action, PlayerActionErrorCodes.Bunkering, LastDecision);
      _state.IntentStack.MarkWaitingFuel(LastDecision);
      return;
    }

    context.Enqueue(new PlanReposition(
      FirmId, originHub.Value, destHub.Value, _ids.HullId.Value, (int)profile));
    LastDecision = $"travel → {destSite.Hub.Name} [{profile}]";
    _state.LastAction = PlayerActionResult.Success(action, LastDecision);
    _state.IntentStack.MarkDone(PlayerOrderKind.TravelTo);
  }

  private void CommitSpot(PlayerOrder order, AgentContext context)
  {
    _ = context;
    string outcome;
    if (_ids.Registry.CanOperate(FirmId) is false)
    {
      LastDecision = "registry hold — cannot operate";
      outcome = "registry";
      goto LogCommit;
    }

    if (string.IsNullOrWhiteSpace(order.OriginSystemId)
        || string.IsNullOrWhiteSpace(order.DestSystemId)
        || string.IsNullOrWhiteSpace(order.SkuLabel)
        || order.Quantity < 1m)
    {
      LastDecision = "spot order incomplete";
      outcome = "incomplete";
      goto LogCommit;
    }

    if (!_ids.Sites.TryGetValue(order.OriginSystemId, out var originSite))
    {
      LastDecision = "load dock unknown";
      outcome = "unknown-origin";
      goto LogCommit;
    }

    if (!originSite.Hub.HubId.Equals(CurrentHub))
    {
      LastDecision = "not at load dock — travel first";
      outcome = "not-at-dock";
      goto LogCommit;
    }

    var product = ResolveSku(order.SkuLabel);
    if (product is null)
    {
      LastDecision = $"unknown sku {order.SkuLabel}";
      outcome = "unknown-sku";
      goto LogCommit;
    }

    var dockStock = CaptainJobBoard.DockStock(context.Simulation, _ids, order.OriginSystemId!);
    var physicalRoom = CampaignWorld.HullCargoCapacity - dockStock - _state.Manifest.Used;
    var qty = Math.Min(order.Quantity, Math.Min(_state.Manifest.Room, Math.Max(0m, physicalRoom)));
    if (qty < 1m)
    {
      LastDecision = dockStock > 0.5m
        ? $"hold full — dock stock {dockStock:0} (sell or haul first)"
        : "hold full";
      outcome = "hold-full";
      goto LogCommit;
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
      outcome = "refused:" + fail;
      goto LogCommit;
    }

    // Firm delivery pay (DestBid) becomes CCA escrow principal when this lot sails.
    if (order.DestBid > 0m)
    {
      _ids.Escrow.StageHaulContract(FirmId, product.Value, order.DestBid);
    }

    LastDecision =
      $"manifest +{order.SkuLabel} ×{qty:0} pay {order.DestBid * qty:0} ({_state.Manifest.Used:0}/{CampaignWorld.HullCargoCapacity:0})";
    outcome = "ok";
    _state.LastAction = PlayerActionResult.Success("acceptSpot", LastDecision);
    LogCommit:
    if (outcome != "ok")
    {
      _state.LastAction = PlayerActionResult.Fail(
        "acceptSpot",
        outcome switch
        {
          "hold-full" => PlayerActionErrorCodes.HoldFull,
          "not-at-dock" => PlayerActionErrorCodes.NotAtDock,
          _ => PlayerActionErrorCodes.Rejected,
        },
        LastDecision);
    }
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
      // Put it back — shouldn't happen if accept was dock-gated.
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      LastDecision = "not at load dock";
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

    // Same bunker gate as TravelTo — PlanShipment fails without first-leg fuel.
    if (!ItineraryPlanner.TryPlan(
          originSite.Hub.HubId, destSite.Hub.HubId, Quantity.From(lot.Quantity), _ids.Hull,
          context.World.Corridors, out var itinerary, lot.Profile)
        || itinerary.LegCount == 0)
    {
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      LastDecision = $"no route → {destSite.Hub.Name}";
      return;
    }

    if (!context.World.Hubs.TryGetValue(originSite.Hub.HubId, out var originHubObj))
    {
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      LastDecision = "origin hub unknown";
      return;
    }

    var first = context.World.Corridors[itinerary.CorridorIds[0]];
    var need = ItineraryPlanner.FuelBurnForLeg(first, _ids.Hull, lot.Profile).Value;
    need = Math.Min(Math.Max(need, 8m), _ids.Hull.FuelTankCapacity.Value);
    var fuelKey = new InventoryKey(FirmId, originHubObj.LocationId, _ids.Fuel);
    var haveFuel = context.World.Inventory.GetQuantity(fuelKey).Value;
    if (haveFuel + 0.0000001m < need)
    {
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      var buyQty = Math.Max(12m, need - haveFuel + 4m);
      context.Enqueue(new PostHubOrder(
        FirmId, originHubObj.LocationId, _ids.Fuel, HubOrderSide.Buy,
        Quantity.From(buyQty), Money.From(CampaignWorld.FuelUnitCost * 1.5m)));
      context.Enqueue(new PlaceProcurementOrder(
        FirmId, originHubObj.LocationId, _ids.Fuel, Quantity.From(Math.Max(8m, need - haveFuel + 2m)),
        Money.From(CampaignWorld.FuelUnitCost * 1.8m)));
      _state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest, SkuLabel: lot.SkuLabel));
      LastDecision = $"bunkering for depart → {destSite.Hub.Name}";
      _state.IntentStack.MarkWaitingFuel(LastDecision);
      return;
    }

    // PlanShipment runs in AcquireInputs after MatchHubOrders — cargo must already be in
    // inventory. Buy+plan same tick only works when a seller fills immediately; otherwise
    // we strand inventory from later fills and report a false depart success.
    var cargoKey = new InventoryKey(FirmId, originSite.Hub.LocationId, lot.ProductId);
    var haveCargo = context.World.Inventory.GetQuantity(cargoKey).Value;
    if (haveCargo + 0.0000001m < 1m)
    {
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        lot.Quantity, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
      var liftBuy = lot.LiftLimit > 0m ? lot.LiftLimit : CampaignWorld.OreBuy;
      context.Enqueue(new PostHubOrder(
        FirmId, originSite.Hub.LocationId, lot.ProductId, HubOrderSide.Buy,
        Quantity.From(lot.Quantity), Money.From(liftBuy)));
      _state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest, SkuLabel: lot.SkuLabel));
      LastDecision = $"lifting {lot.SkuLabel} ×{lot.Quantity:0} for depart → {destSite.Hub.Name}";
      _state.IntentStack.MarkWaitingCargo(LastDecision);
      return;
    }

    var shipQty = Math.Min(haveCargo, lot.Quantity);
    if (shipQty + 0.0000001m < lot.Quantity)
    {
      // Keep remainder on manifest for a follow-up depart after this lift sails.
      var remain = lot.Quantity - shipQty;
      _state.Manifest.TryAdd(
        lot.OriginSystemId, lot.DestSystemId, lot.SkuLabel, lot.ProductId,
        remain, lot.LiftLimit, lot.DestBid, lot.Profile, out _);
    }

    context.Enqueue(new PlanShipment(
      FirmId, originSite.Hub.HubId.Value, destSite.Hub.HubId.Value,
      lot.ProductId, Quantity.From(shipQty), _ids.HullId.Value, (int)lot.Profile));
    LastDecision =
      $"depart {lot.SkuLabel} {originSite.Hub.Name}→{destSite.Hub.Name} ×{shipQty:0} [{lot.Profile}]";
    _state.IntentStack.MarkDone(PlayerOrderKind.DepartManifest);
  }

  private void MarketBuy(PlayerOrder order, AgentContext context)
  {
    if (_ids.Registry.CanOperate(FirmId) is false)
    {
      LastDecision = "registry hold — cannot operate";
      return;
    }

    if (order.CounterpartyFirmId is null
        || string.IsNullOrWhiteSpace(order.SkuLabel)
        || order.Quantity < 1m
        || order.LiftLimit <= 0m)
    {
      LastDecision = "market buy incomplete";
      return;
    }

    var product = ResolveSku(order.SkuLabel);
    if (product is null)
    {
      LastDecision = $"unknown sku {order.SkuLabel}";
      return;
    }

    if (!_ids.Sites.TryGetValue(
          string.IsNullOrWhiteSpace(order.OriginSystemId) ? ResolveSystemId(CurrentHub) : order.OriginSystemId!,
          out var site)
        || !site.Hub.HubId.Equals(CurrentHub))
    {
      LastDecision = "market buy — not at dock";
      return;
    }

    var stock = CaptainJobBoard.DockStock(context.Simulation, _ids, site.Hub.SystemId);
    var room = CampaignWorld.HullCargoCapacity - stock - _state.Manifest.Used;
    var qty = Math.Min(order.Quantity, Math.Max(0m, room));
    if (qty < 1m)
    {
      LastDecision = "hold full — cannot buy";
      return;
    }

    var cash = context.World.Ledgers.TryGetValue(FirmId, out var led) ? led.Cash.Amount : 0m;
    var afford = order.LiftLimit > 0m ? Math.Floor(cash / order.LiftLimit) : 0m;
    qty = Math.Min(qty, afford);
    if (qty < 1m)
    {
      LastDecision = "market buy — cash short";
      return;
    }

    context.Enqueue(new TransferGoodsForCash(
      FirmId.From(order.CounterpartyFirmId.Value),
      FirmId,
      site.Hub.LocationId,
      product.Value,
      Quantity.From(qty),
      Money.From(order.LiftLimit)));
    LastDecision = $"bought {order.SkuLabel} ×{qty:0} @ {order.LiftLimit:0.##}";
  }

  private void MarketSell(PlayerOrder order, AgentContext context)
  {
    if (order.CounterpartyFirmId is null
        || string.IsNullOrWhiteSpace(order.SkuLabel)
        || order.Quantity < 1m
        || order.LiftLimit <= 0m)
    {
      LastDecision = "market sell incomplete";
      return;
    }

    var product = ResolveSku(order.SkuLabel);
    if (product is null)
    {
      LastDecision = $"unknown sku {order.SkuLabel}";
      return;
    }

    if (!_ids.Sites.TryGetValue(
          string.IsNullOrWhiteSpace(order.OriginSystemId) ? ResolveSystemId(CurrentHub) : order.OriginSystemId!,
          out var site)
        || !site.Hub.HubId.Equals(CurrentHub))
    {
      LastDecision = "market sell — not at dock";
      return;
    }

    var have = context.World.Inventory.GetQuantity(
      new InventoryKey(FirmId, site.Hub.LocationId, product.Value)).Value;
    var qty = Math.Min(order.Quantity, have);
    if (qty < 1m)
    {
      LastDecision = "market sell — no stock";
      return;
    }

    context.Enqueue(new TransferGoodsForCash(
      FirmId,
      FirmId.From(order.CounterpartyFirmId.Value),
      site.Hub.LocationId,
      product.Value,
      Quantity.From(qty),
      Money.From(order.LiftLimit)));
    LastDecision = $"sold {order.SkuLabel} ×{qty:0} @ {order.LiftLimit:0.##}";
  }

  private string ResolveSystemId(TransportHubId hub)
  {
    foreach (var s in _ids.Sites.Values)
    {
      if (s.Hub.HubId.Equals(hub))
      {
        return s.Hub.SystemId;
      }
    }

    return "sol";
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

    return HullFinance.TrySettlePremium(firm, uw, entry, context.Simulation.State.Clock.Date);
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
    return HullFinance.TryPayOverhaul(
      firm, yard, _ids.Registry, entry, bill, context.Simulation.State.Clock.Date);
  }
}
