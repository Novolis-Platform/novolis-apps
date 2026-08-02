using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Keeps Calypso insured/overhauled and working the dock board when autopilot is on —
/// enough to chase last-tramp survival without a human at the glass.
/// </summary>
internal static class SurvivalCaptain
{
  /// <summary>True when an order was queued this tick (caller should skip AI haul).</summary>
  public static bool Tick(PlayerTrampAgent agent, AgentContext context, CampaignWorld.Ids ids, PlayerControlState state)
  {
    Stabilize(agent, context, ids, state);

    var world = context.World;
    var firm = agent.FirmId;
    if (world.Shipments.Any(s => !s.IsLegacy && s.FirmId.Equals(firm) && s.Status == ShipmentStatus.InTransit)
        || world.PendingPlanShipments.Any(p => p.FirmId.Equals(firm))
        || world.PendingPlanRepositions.Any(p => p.FirmId.Equals(firm))
        || state.Orders.Count > 0)
    {
      return state.Orders.Count > 0;
    }

    var cash = world.Ledgers.TryGetValue(firm, out var led) ? led.Cash.Amount : 0m;
    var entry = ids.Registry.TryGet(firm);
    var payable = entry?.PremiumPayable ?? 0m;
    // Leave runway for premium + bunker; shrink when thin so Fat locals still clear.
    var haulReserve = cash < 500m ? 40m : 280m;

    if (!ids.Registry.CanOperate(firm) || cash < payable + 40m)
    {
      return EnqueuePremiumRescue(agent, context, ids, state);
    }

    if (state.Manifest.Used >= 1m)
    {
      state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest));
      return true;
    }

    var hub = ResolveSystemId(ids, agent.CurrentHub);
    var live = CaptainJobBoard.ListLiveFreight(
      context.Simulation, ids, state.DefaultProfile, hub, take: 32);

    // Best positive-margin local that we can afford to lift.
    var budget = Math.Max(0m, cash - payable - haulReserve);
    var atBerth = live
      .Where(s => s.AtOrigin && s.Margin > 0m && s.LiftCost <= budget)
      .OrderByDescending(s => s.Margin)
      .ThenBy(s => s.LiftCost)
      .FirstOrDefault();
    if (atBerth is not null)
    {
      state.Orders.Enqueue(new PlayerOrder(
        PlayerOrderKind.CommitSpot,
        OriginSystemId: atBerth.OriginSystemId,
        DestSystemId: atBerth.DestSystemId,
        SkuLabel: atBerth.SkuLabel,
        Quantity: atBerth.Quantity,
        LiftLimit: atBerth.LiftLimit,
        DestBid: atBerth.DestBid,
        Profile: atBerth.Profile));
      return true;
    }

    // Chase remote work — idle Wait burns lien after grace and ends the run.
    var remote = live
      .Where(s => !s.AtOrigin && s.Margin > 0m)
      .OrderByDescending(s => s.Margin)
      .FirstOrDefault();
    if (remote is not null && cash >= payable + haulReserve + 80m)
    {
      state.Orders.Enqueue(new PlayerOrder(
        PlayerOrderKind.TravelTo,
        DestSystemId: remote.OriginSystemId,
        Profile: state.DefaultProfile));
      return true;
    }

    // Thin cash: do not hand the wheel to CarrierFirmAgent (it can bunker the hull dry).
    return false;
  }

  private static void Stabilize(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state) =>
    StabilizeHull(agent, context, ids);

  /// <summary>Shared hull insurance / overhaul stabilize for rule + neural autopilot.</summary>
  internal static void StabilizeHull(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids)
  {
    var entry = ids.Registry.TryGet(agent.FirmId);
    if (entry is null
        || !context.World.Ledgers.TryGetValue(agent.FirmId, out var firm)
        || !context.World.Ledgers.TryGetValue(ids.Registry.Underwriter, out var uw))
    {
      return;
    }

    var day = context.Simulation.State.Clock.Date;
    var cash = firm.Cash.Amount;
    var payable = entry.PremiumPayable;
    // Mid-haul: float against CCA escrow so bunker + premium do not soft-lock the voyage.
    if (cash < Math.Max(250m, payable + 80m) && ids.Escrow.FirmHasOpen(agent.FirmId))
    {
      var need = Math.Max(250m, payable + 120m) - cash;
      ids.Escrow.TryFloatWorkingCapital(context.World, ids, agent.FirmId, need, day);
      cash = firm.Cash.Amount;
    }

    // Empty books — Station (or underwriter) remittance so Calypso can reinstate.
    if (entry.OwnerMaster
        && (!entry.Insured || payable > 40m)
        && cash < Math.Max(payable + 40m, 200m))
    {
      FirmLedger? treasury = null;
      if (context.World.Ledgers.TryGetValue(ids.Station, out var station) && station.Cash.Amount > 2_000m)
      {
        treasury = station;
      }
      else if (context.World.Ledgers.TryGetValue(ids.Registry.Underwriter, out var yard) && yard.Cash.Amount > 2_000m)
      {
        treasury = yard;
      }

      if (treasury is not null)
      {
        var bail = Math.Max(payable + 900m, 1_000m);
        bail = Math.Min(bail, Math.Min(1_500m, treasury.Cash.Amount - 1_000m));
        if (bail >= 200m)
        {
          var pay = Money.From(bail);
          treasury.Post(AccountRole.WageExpense, AccountRole.Cash, pay, day, "Station owner remittance");
          firm.Post(AccountRole.Cash, AccountRole.Revenue, pay, day, "Station owner remittance");
          cash = firm.Cash.Amount;
        }
      }
    }

    if (!entry.Insured || entry.PremiumArrearsDays > 0 || entry.PremiumPayable > 0.0001m)
    {
      HullFinance.TrySettlePremium(firm, uw, entry, day);
    }

    if (entry.BurnedOut || entry.OverhaulDue)
    {
      var billAmt = entry.BurnedOut
        ? ids.Registry.QuoteBurnoutOverhaul(entry)
        : ids.Registry.QuoteElectiveOverhaul(entry);
      cash = firm.Cash.Amount;
      var premiumDue = Math.Max(entry.PremiumPayable, ids.Registry.QuotePremiumDue(entry));
      var runway = premiumDue + 350m;
      if (cash < billAmt + runway
          && entry.OwnerMaster
          && context.World.Ledgers.TryGetValue(ids.Station, out var yard))
      {
        var need = billAmt + runway - cash;
        if (need >= 50m && yard.Cash.Amount >= need + 200m)
        {
          var adv = Money.From(need);
          yard.Post(AccountRole.WageExpense, AccountRole.Cash, adv, day, "Station yard float");
          firm.Post(AccountRole.Cash, AccountRole.Revenue, adv, day, "Station yard float");
        }
      }

      HullFinance.TryPayOverhaul(
        firm, uw, ids.Registry, entry, Money.From(billAmt), day, cashReserve: runway);
    }
  }

  /// <summary>Queue BID sells + PayPremium so Calypso can get back on the registry.</summary>
  internal static bool EnqueuePremiumRescue(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state)
  {
    if (state.Orders.Count > 0)
    {
      return true;
    }

    var day = context.Simulation.State.Clock.Date;
    if (context.World.Ledgers.TryGetValue(agent.FirmId, out var firm))
    {
      var entry = ids.Registry.TryGet(agent.FirmId);
      var payable = entry?.PremiumPayable ?? 0m;
      if (firm.Cash.Amount < payable + 80m && ids.Escrow.FirmHasOpen(agent.FirmId))
      {
        ids.Escrow.TryFloatWorkingCapital(
          context.World, ids, agent.FirmId, Math.Max(200m, payable + 100m) - firm.Cash.Amount, day);
      }

      if (entry is not null
          && context.World.Ledgers.TryGetValue(ids.Registry.Underwriter, out var uw)
          && firm.Cash.Amount > 0.0001m)
      {
        HullFinance.TrySettlePremium(firm, uw, entry, day);
      }

      if (ids.Registry.CanOperate(agent.FirmId))
      {
        return false;
      }
    }

    var hub = ResolveSystemId(ids, agent.CurrentHub);
    if (!ids.Sites.TryGetValue(hub, out var site))
    {
      if (context.World.Ledgers.TryGetValue(agent.FirmId, out var led) && led.Cash.Amount > 0.0001m)
      {
        state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
        return true;
      }

      return false;
    }

    var queuedSell = false;
    var bids = CaptainJobBoard.ListMarket(context.Simulation, ids, hub)
      .Where(l => !l.IsAsk)
      .OrderByDescending(l => l.UnitPrice)
      .Take(8)
      .ToList();
    foreach (var bid in bids)
    {
      var stock = context.World.Inventory.GetQuantity(
        new InventoryKey(agent.FirmId, site.Hub.LocationId, bid.ProductId)).Value;
      if (stock < 1m)
      {
        continue;
      }

      state.Orders.Enqueue(new PlayerOrder(
        PlayerOrderKind.MarketSell,
        OriginSystemId: hub,
        SkuLabel: bid.SkuLabel,
        Quantity: Math.Min(stock, bid.Quantity),
        LiftLimit: bid.UnitPrice,
        CounterpartyFirmId: bid.Counterparty.Value));
      queuedSell = true;
    }

    if (context.World.Ledgers.TryGetValue(agent.FirmId, out var cashLed)
        && cashLed.Cash.Amount > 0.0001m)
    {
      state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
      return true;
    }

    // No cash and no inventory — do not spin futile PayPremium (blocks bunker/inner AI).
    return queuedSell;
  }

  private static string ResolveSystemId(CampaignWorld.Ids ids, TransportHubId hub)
  {
    foreach (var s in ids.Sites.Values)
    {
      if (s.Hub.HubId.Equals(hub))
      {
        return s.Hub.SystemId;
      }
    }

    return "sol";
  }
}
