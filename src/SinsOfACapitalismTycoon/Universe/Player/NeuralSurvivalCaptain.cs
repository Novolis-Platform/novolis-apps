using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Neural-flagged autopilot that keeps Calypso solvent for long horizons.
/// Untrained dense nets go broke; production path uses <see cref="SurvivalCaptain"/>
/// (same berth verbs) and keeps the brain for evolution / future champions.
/// </summary>
internal static class NeuralSurvivalCaptain
{
  /// <summary>True when an order was queued this tick.</summary>
  public static bool Tick(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state)
  {
    var brain = state.NeuralBrain
                ?? throw new InvalidOperationException("NeuralSurvivalCaptain requires NeuralBrain.");

    // Champions (imitated / evolved) may drive; otherwise SurvivalCaptain owns the hull.
    if (brain.AllowNetworkControl)
    {
      return TickNetwork(agent, context, ids, state, brain);
    }

    var queued = SurvivalCaptain.Tick(agent, context, ids, state);
    brain.LastDecision = queued ? "neural · survival-rule" : "neural · carrier-fallback";
    return queued;
  }

  static bool TickNetwork(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state,
    NeuralCaptainBrain brain)
  {
    SurvivalCaptain.StabilizeHull(agent, context, ids);

    var world = context.World;
    var firm = agent.FirmId;
    if (world.Shipments.Any(s => !s.IsLegacy && s.FirmId.Equals(firm) && s.Status == ShipmentStatus.InTransit)
        || world.PendingPlanShipments.Any(p => p.FirmId.Equals(firm))
        || world.PendingPlanRepositions.Any(p => p.FirmId.Equals(firm))
        || state.Orders.Count > 0)
    {
      brain.LastDecision = "neural · busy";
      return state.Orders.Count > 0;
    }

    if (!ids.Registry.CanOperate(firm))
    {
      brain.LastDecision = "neural · rescue premium";
      return SurvivalCaptain.EnqueuePremiumRescue(agent, context, ids, state);
    }

    var cash = world.Ledgers.TryGetValue(firm, out var led) ? led.Cash.Amount : 0m;
    // Solvency governor — hand back to rule captain when thin.
    if (cash < 1_200m || state.SoftFailRaised)
    {
      brain.LastDecision = "neural · solvency→rule";
      return SurvivalCaptain.Tick(agent, context, ids, state);
    }

    var hub = ResolveSystemId(ids, agent.CurrentHub);
    var board = CaptainJobBoard.ListSpot(
      context.Simulation, ids, state.DefaultProfile, hub, dockOnly: true, mesh: ids.Mesh);
    var live = CaptainJobBoard.ListLiveFreight(
      context.Simulation, ids, state.DefaultProfile, hub, take: 24);
    var (offers, _) = BerthOfferBoard.Build(board, live, dockBoardOnly: true);

    var hasLocal = offers.Any(o => o.Kind == BerthOfferKind.Local && o.Spot is not null);
    var hasRumor = offers.Any(o => o.Kind == BerthOfferKind.Rumor && o.Spot is not null);
    var hasManifest = state.Manifest.Used >= 1m;

    Span<double> inputs = stackalloc double[NeuralCaptainBrain.InputSize];
    NeuralCaptainCodec.Encode(inputs, context.Simulation, ids, state, agent, offers);
    var eval = brain.Network.Evaluate(inputs);
    var action = NeuralCaptainCodec.PickLegalAction(
      eval.Output,
      canDepart: hasManifest,
      canAcceptLocal: hasLocal,
      canSteamRumor: hasRumor);

    switch (action)
    {
      case NeuralCaptainCodec.ActionKind.Depart:
        state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.DepartManifest));
        brain.LastDecision = "neural · Depart";
        return true;

      case NeuralCaptainCodec.ActionKind.AcceptBestLocal:
      {
        var spot = offers.First(o => o.Kind == BerthOfferKind.Local && o.Spot is not null).Spot!;
        state.Orders.Enqueue(new PlayerOrder(
          PlayerOrderKind.CommitSpot,
          OriginSystemId: spot.OriginSystemId,
          DestSystemId: spot.DestSystemId,
          SkuLabel: spot.SkuLabel,
          Quantity: spot.Quantity,
          LiftLimit: spot.LiftLimit,
          DestBid: spot.DestBid,
          Profile: spot.Profile));
        brain.LastDecision = $"neural · Accept {spot.SkuLabel}";
        return true;
      }

      case NeuralCaptainCodec.ActionKind.SteamBestRumor:
      {
        var spot = offers.First(o => o.Kind == BerthOfferKind.Rumor && o.Spot is not null).Spot!;
        state.Orders.Enqueue(new PlayerOrder(
          PlayerOrderKind.TravelTo,
          DestSystemId: spot.OriginSystemId,
          Profile: state.DefaultProfile));
        brain.LastDecision = $"neural · Steam → {spot.OriginName}";
        return true;
      }

      case NeuralCaptainCodec.ActionKind.Stabilize:
        state.Orders.Enqueue(new PlayerOrder(PlayerOrderKind.PayPremium));
        brain.LastDecision = "neural · PayPremium";
        return true;

      default:
        brain.LastDecision = "neural · survival-rule";
        return SurvivalCaptain.Tick(agent, context, ids, state);
    }
  }

  static string ResolveSystemId(CampaignWorld.Ids ids, TransportHubId hubId)
  {
    foreach (var hub in ids.Bridge.Hubs)
    {
      if (hub.HubId.Equals(hubId))
      {
        return hub.SystemId;
      }
    }

    return "sol";
  }
}
