using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Keeps Calypso insured/overhauled and working the dock desk when autopilot is on —
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

    if (!ids.Registry.CanOperate(firm))
    {
      // Last ditch: sell dock stock, then remit — keep ticking until insured or broke.
      return EnqueuePremiumRescue(agent, context, ids, state);
    }

    if (state.Manifest.Used >= 1m)
    {
      state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest));
      return true;
    }

    var hub = ResolveSystemId(ids, agent.CurrentHub);
    var live = CaptainJobBoard.ListLiveFreight(
      context.Simulation, ids, state.DefaultProfile, hub, take: 24);

    var atBerth = live.FirstOrDefault(s => s.AtOrigin && s.Margin > 0m)
                  ?? live.FirstOrDefault(s => s.AtOrigin);
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

    var remote = live.FirstOrDefault(s => !s.AtOrigin && s.Margin > 8m)
                 ?? live.FirstOrDefault(s => !s.AtOrigin);
    if (remote is not null)
    {
      state.Orders.Enqueue(new PlayerOrder(
        PlayerOrderKind.TravelTo,
        DestSystemId: remote.OriginSystemId,
        Profile: state.DefaultProfile));
      return true;
    }

    return false;
  }

  private static void Stabilize(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state)
  {
    var entry = ids.Registry.TryGet(agent.FirmId);
    if (entry is null
        || !context.World.Ledgers.TryGetValue(agent.FirmId, out var firm)
        || !context.World.Ledgers.TryGetValue(ids.Registry.Underwriter, out var uw))
    {
      return;
    }

    var day = context.Simulation.State.Clock.Date;
    if (!entry.Insured || entry.PremiumArrearsDays > 0 || entry.PremiumPayable > 0.0001m)
    {
      HullFinance.TrySettlePremium(firm, uw, entry, day);
    }

    if (entry.BurnedOut || entry.OverhaulDue)
    {
      var bill = Money.From(entry.BurnedOut
        ? ids.Registry.QuoteBurnoutOverhaul(entry)
        : ids.Registry.QuoteElectiveOverhaul(entry));
      HullFinance.TryPayOverhaul(firm, uw, ids.Registry, entry, bill, day);
    }
  }

  /// <summary>Queue BID sells + PayPremium so Calypso can get back on the registry.</summary>
  private static bool EnqueuePremiumRescue(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state)
  {
    if (state.Orders.Count > 0)
    {
      return true;
    }

    var hub = ResolveSystemId(ids, agent.CurrentHub);
    if (!ids.Sites.TryGetValue(hub, out var site))
    {
      state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
      return true;
    }

    var bids = CaptainJobBoard.ListMarket(context.Simulation, ids, hub)
      .Where(l => !l.IsAsk)
      .OrderByDescending(l => l.UnitPrice)
      .Take(3)
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
    }

    state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
    return true;
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
