using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Spot commodity intel + charter offers for the captain desk.</summary>
internal static class CaptainJobBoard
{
  public sealed record SpotCandidate(
    string Label,
    string OriginSystemId,
    string DestSystemId,
    string OriginName,
    string DestName,
    string SkuLabel,
    decimal Quantity,
    decimal LiftLimit,
    decimal DestBid,
    decimal Margin,
    TransitProfile Profile,
    string Summary,
    bool AtOrigin,
    string DistanceHint);

  public sealed record CharterCandidate(
    string Id,
    string Kind,
    string Label,
    string Detail,
    string? OriginSystemId,
    bool CanAcceptHere,
    bool CanRefuse);

  /// <summary>Network (or berth-filtered) spot spreads. Accept requires <see cref="SpotCandidate.AtOrigin"/>.</summary>
  public static IReadOnlyList<SpotCandidate> ListSpot(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string currentSystemId,
    bool berthOnly = false,
    int take = 16)
  {
    HashSet<string>? allowOrigins = null;
    if (berthOnly && !string.IsNullOrEmpty(currentSystemId))
    {
      allowOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentSystemId };
    }

    return BuildSpot(sim, ids, profile, currentSystemId, allowOrigins, take);
  }

  /// <summary>Charters: ugly standby + short escrow-framed suggestions near current berth.</summary>
  public static IReadOnlyList<CharterCandidate> ListCharters(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    PlayerControlState player,
    string currentSystemId)
  {
    var list = new List<CharterCandidate>();
    if (player.Opportunities?.ActiveStandbyTramp?.Equals(ids.Carrier) == true)
    {
      list.Add(new CharterCandidate(
        "standby",
        "standby",
        "Ugly standby window",
        "Completion money — refuse ≠ premium hit",
        OriginSystemId: null,
        CanAcceptHere: true,
        CanRefuse: true));
    }

    // Tutorial-shaped short charter hint: Industrial/Mining within ~8 ly of current berth.
    foreach (var spot in ListSpot(sim, ids, player.DefaultProfile, currentSystemId, berthOnly: true, take: 4)
               .Where(s => s.AtOrigin && s.Margin > 0m)
               .Take(2))
    {
      list.Add(new CharterCandidate(
        $"short:{spot.OriginSystemId}:{spot.DestSystemId}:{spot.SkuLabel}",
        "short-charter",
        $"Short charter · {spot.SkuLabel} → {spot.DestName}",
        $"Escrow-framed dock lift · Δ{spot.Margin:0.#} · {spot.Quantity:0}u",
        spot.OriginSystemId,
        CanAcceptHere: spot.AtOrigin,
        CanRefuse: false));
    }

    return list;
  }

  private static IReadOnlyList<SpotCandidate> BuildSpot(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string currentSystemId,
    HashSet<string>? allowOrigins,
    int take)
  {
    var world = sim.State.World;
    var wage = world.Policy.WageRatePerHour;
    var fuelCost = world.TransportFuelUnitCost;
    var siteByLoc = ids.Sites.Values.ToDictionary(s => s.Hub.LocationId);
    var freight = new[] { ids.Ore, ids.Parts, ids.Goods };
    var sells = new Dictionary<ProductId, List<HubOrder>>();
    var buys = new Dictionary<ProductId, List<HubOrder>>();

    foreach (var o in world.HubOrders)
    {
      if (o.IsFilled || o.FirmId.Equals(ids.Carrier) || !freight.Any(p => p.Equals(o.ProductId)))
      {
        continue;
      }

      if (o.Side == HubOrderSide.Sell)
      {
        if (!sells.TryGetValue(o.ProductId, out var list))
        {
          list = [];
          sells[o.ProductId] = list;
        }

        list.Add(o);
      }
      else
      {
        if (!buys.TryGetValue(o.ProductId, out var list))
        {
          list = [];
          buys[o.ProductId] = list;
        }

        list.Add(o);
      }
    }

    TransportHubId? currentHubId = null;
    if (ids.Sites.TryGetValue(currentSystemId, out var here))
    {
      currentHubId = here.Hub.HubId;
    }

    var jobs = new List<SpotCandidate>();
    foreach (var (product, rawSells) in sells)
    {
      if (!buys.TryGetValue(product, out var rawBuys))
      {
        continue;
      }

      foreach (var sell in rawSells.OrderBy(s => s.LimitPrice.Amount).Take(24))
      {
        if (!siteByLoc.TryGetValue(sell.LocationId, out var origin))
        {
          continue;
        }

        if (allowOrigins is not null && !allowOrigins.Contains(origin.Hub.SystemId))
        {
          continue;
        }

        foreach (var buy in rawBuys.OrderByDescending(b => b.LimitPrice.Amount).Take(16))
        {
          if (buy.LocationId.Equals(sell.LocationId)
              || buy.LimitPrice.Amount < sell.LimitPrice.Amount
              || !siteByLoc.TryGetValue(buy.LocationId, out var dest))
          {
            continue;
          }

          var qty = Math.Min(
            Math.Min(sell.Remaining.Value, buy.Remaining.Value),
            CampaignWorld.HullCargoCapacity);
          if (qty < 2m)
          {
            continue;
          }

          var haulProfile = profile;
          if (product.Equals(ids.Ore))
          {
            haulProfile = TransitProfile.SlowEconomic;
          }
          else if (profile == TransitProfile.StandardCommercial)
          {
            haulProfile = TransitChooser.ForTramp(product, ids);
          }

          if (!ItineraryPlanner.TryPlan(
                origin.Hub.HubId, dest.Hub.HubId,
                ids.Hull.CargoCapacity, ids.Hull, world.Corridors, out var itinerary, haulProfile))
          {
            continue;
          }

          if (JumpBandGate.ShouldRefuse(
                world, ids, ids.Reputation, ids.Escrow, ids.Carrier, product,
                origin.Hub.HubId, dest.Hub.HubId, haulProfile))
          {
            continue;
          }

          var est = HaulCostEstimator.Estimate(
            itinerary, world.Corridors, ids.Hull, wage, fuelCost, haulProfile);
          var lift = Math.Min(buy.LimitPrice.Amount, sell.LimitPrice.Amount * 1.12m);
          var margin = qty * buy.LimitPrice.Amount - qty * lift - est.TotalVariableCost.Amount;
          var sku = CampaignWorld.SkuLabel(product, ids);
          var atOrigin = currentHubId is { } ch && origin.Hub.HubId.Equals(ch);
          var distHint = atOrigin
            ? "AT BERTH"
            : EstimateLyHint(ids, currentSystemId, origin.Hub.SystemId);

          jobs.Add(new SpotCandidate(
            $"{sku} {origin.Hub.Name}→{dest.Hub.Name}",
            origin.Hub.SystemId,
            dest.Hub.SystemId,
            origin.Hub.Name,
            dest.Hub.Name,
            sku,
            qty,
            lift,
            buy.LimitPrice.Amount,
            margin,
            haulProfile,
            $"Δ{margin:0.#} haul {est.TotalVariableCost.Amount:0} [{haulProfile}]",
            atOrigin,
            distHint));
        }
      }
    }

    return jobs
      .OrderByDescending(j => j.AtOrigin)
      .ThenByDescending(j => j.Margin)
      .Take(take)
      .ToList();
  }

  private static string EstimateLyHint(CampaignWorld.Ids ids, string fromSystem, string toSystem)
  {
    if (string.Equals(fromSystem, toSystem, StringComparison.OrdinalIgnoreCase))
    {
      return "here";
    }

    if (!ids.Bridge.Graph.Adjacency.TryGetValue(fromSystem, out var edges))
    {
      return "remote";
    }

    foreach (var e in edges)
    {
      if (e.To.Value.Equals(toSystem, StringComparison.OrdinalIgnoreCase))
      {
        return e.DistanceLy is > 0 ? $"~{e.DistanceLy:0.#} ly" : "1 hop";
      }
    }

    return "multi-hop";
  }

  // Back-compat shim for older call sites.
  public static IReadOnlyList<SpotCandidate> ListJobs(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string? localSystemId = null,
    bool localOnly = false,
    int take = 12) =>
    ListSpot(sim, ids, profile, localSystemId ?? "sol", berthOnly: localOnly, take: take);
}
