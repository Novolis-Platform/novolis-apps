using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Keeps Calypso insured/overhauled and working the berth desk when autopilot is on —
/// enough to chase last-tramp survival without a human at the glass.
/// </summary>
internal static class SurvivalCaptain
{
  /// <summary>True when an order was queued this tick (caller should skip AI haul).</summary>
  public static bool Tick(PlayerTrampAgent agent, AgentContext context, CampaignWorld.Ids ids, PlayerControlState state)
  {
    Stabilize(agent, context, ids);

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
      return false;
    }

    if (state.Manifest.Used >= 1m)
    {
      state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest));
      return true;
    }

    var hub = ResolveSystemId(ids, agent.CurrentHub);
    var spots = CaptainJobBoard.ListSpot(context.Simulation, ids, state.DefaultProfile, hub, berthOnly: false, take: 24);
    var atBerth = spots.FirstOrDefault(s => s.AtOrigin && s.Margin > 0m)
                  ?? spots.FirstOrDefault(s => s.AtOrigin);
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

    var remote = spots.FirstOrDefault(s => !s.AtOrigin && s.Margin > 8m)
                 ?? spots.FirstOrDefault(s => !s.AtOrigin);
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

  private static void Stabilize(PlayerTrampAgent agent, AgentContext context, CampaignWorld.Ids ids)
  {
    var entry = ids.Registry.TryGet(agent.FirmId);
    if (entry is null
        || !context.World.Ledgers.TryGetValue(agent.FirmId, out var firm)
        || !context.World.Ledgers.TryGetValue(ids.Registry.Underwriter, out var uw))
    {
      return;
    }

    var day = context.Simulation.State.Clock.Date;
    if (!entry.Insured || entry.PremiumArrearsDays > 0)
    {
      var quote = Money.From(Math.Max(14m, ids.Registry.QuoteDailyPremium(entry)));
      if (firm.Cash.Amount + 0.0001m >= quote.Amount)
      {
        firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, quote, day, "Hull insurance premium");
        uw.Post(AccountRole.Cash, AccountRole.Revenue, quote, day, "Hull insurance premium");
        entry.PremiumPaid += quote.Amount;
        entry.Insured = true;
        entry.PremiumArrearsDays = 0;
        entry.Suspended = false;
      }
    }

    if (entry.BurnedOut || entry.OverhaulDue)
    {
      var bill = Money.From(entry.BurnedOut
        ? ids.Registry.QuoteBurnoutOverhaul(entry)
        : ids.Registry.QuoteElectiveOverhaul(entry));
      if (firm.Cash.Amount >= bill.Amount + 40m)
      {
        firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, bill, day, "FTL drive overhaul");
        uw.Post(AccountRole.Cash, AccountRole.Revenue, bill, day, "FTL drive overhaul");
        entry.MaintenancePaid += bill.Amount;
        ids.Registry.ApplyOverhaul(entry);
      }
    }
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
