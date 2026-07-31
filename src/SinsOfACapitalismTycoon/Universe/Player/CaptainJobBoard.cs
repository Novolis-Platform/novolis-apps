using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Novolis.Simulation.Mesh;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

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
    string DistanceHint,
    string LogicalKey = "")
  {
    /// <summary>Gross firm pay / dest sell (DestBid × qty).</summary>
    public decimal ContractPay =>
      Math.Round(DestBid * Quantity, 2, MidpointRounding.AwayFromZero);

    /// <summary>Cash to lift cargo at origin (LiftLimit × qty).</summary>
    public decimal LiftCost =>
      Math.Round(LiftLimit * Quantity, 2, MidpointRounding.AwayFromZero);
  }

  public sealed record CharterCandidate(
    string Id,
    string Kind,
    string Label,
    string Detail,
    string? OriginSystemId,
    bool CanAcceptHere,
    bool CanRefuse,
    string? DestSystemId = null,
    string? SkuLabel = null,
    decimal Quantity = 0m,
    decimal LiftLimit = 0m,
    decimal DestBid = 0m,
    TransitProfile Profile = TransitProfile.StandardCommercial,
    decimal Margin = 0m)
  {
    /// <summary>Firm escrow / delivery pay (DestBid × qty). Zero for standby.</summary>
    public decimal ContractPay =>
      Math.Round(DestBid * Quantity, 2, MidpointRounding.AwayFromZero);

    public decimal LiftCost =>
      Math.Round(LiftLimit * Quantity, 2, MidpointRounding.AwayFromZero);
  }

  /// <summary>Local dock HubOrder tape — buy asks / sell bids for Calypso inventory.</summary>
  public sealed record MarketLot(
    Guid OrderId,
    string SideLabel,
    bool IsAsk,
    string SkuLabel,
    ProductId ProductId,
    FirmId Counterparty,
    decimal Quantity,
    decimal UnitPrice,
    string Summary);

  public static bool IsFreightSku(string skuLabel) =>
    skuLabel.Equals("Raw", StringComparison.OrdinalIgnoreCase)
    || skuLabel.Equals("Capital", StringComparison.OrdinalIgnoreCase)
    || skuLabel.Equals("Ore", StringComparison.OrdinalIgnoreCase)
    || skuLabel.Equals("Parts", StringComparison.OrdinalIgnoreCase);

  public static bool IsGoodsSku(string skuLabel) =>
    skuLabel.Equals("Final", StringComparison.OrdinalIgnoreCase)
    || skuLabel.Equals("Goods", StringComparison.OrdinalIgnoreCase);

  /// <summary>
  /// Mesh (or dock-filtered) <b>freight</b> spot spreads (Raw / Capital).
  /// Dock = live <see cref="BuildSpot"/>; mesh = mesh <c>Commerce.Spot</c> digests only (FTL lag).
  /// Accept still requires <see cref="SpotCandidate.AtOrigin"/>.
  /// </summary>
  public static IReadOnlyList<SpotCandidate> ListSpot(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string currentSystemId,
    bool dockOnly = false,
    int take = 16,
    MeshState? mesh = null)
  {
    IReadOnlyList<SpotCandidate> raw;
    if (dockOnly)
    {
      HashSet<string>? allowOrigins = null;
      if (!string.IsNullOrEmpty(currentSystemId))
      {
        allowOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentSystemId };
      }

      raw = BuildSpot(sim, ids, profile, currentSystemId, allowOrigins, take: take * 2, lane: SpotLane.Freight);
    }
    else
    {
      var state = mesh ?? ids.Mesh;
      raw = ListSpotFromMeshDigests(state, currentSystemId, take: take * 2);
    }

    return raw
      .Where(s => IsFreightSku(s.SkuLabel))
      .OrderByDescending(j => j.AtOrigin)
      .ThenByDescending(j => j.Margin)
      .Take(take)
      .ToList();
  }

  /// <summary>Live freight spreads (ignores mesh lag) — dock acts / autopilot navigation.</summary>
  public static IReadOnlyList<SpotCandidate> ListLiveFreight(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string currentSystemId,
    int take = 24) =>
    BuildSpot(sim, ids, profile, currentSystemId, allowOrigins: null, take, SpotLane.Freight);

  /// <summary>Live market snapshot used to author mesh digests (not the delayed mesh board).</summary>
  public static IReadOnlyList<SpotCandidate> BuildSpotSnapshot(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    int take = 64) =>
    BuildSpot(sim, ids, profile, currentSystemId: "sol", allowOrigins: null, take, SpotLane.All);

  /// <summary>Charters: ugly standby + Goods/Final contract hauls at current dock.</summary>
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

    var goods = BuildSpot(
      sim, ids, player.DefaultProfile, currentSystemId,
      allowOrigins: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentSystemId },
      take: 8,
      lane: SpotLane.Goods);

    foreach (var spot in goods.Where(s => s.AtOrigin && s.Margin > 0m).Take(4))
    {
      var pay = spot.ContractPay;
      var lift = spot.LiftCost;
      list.Add(new CharterCandidate(
        $"goods:{spot.OriginSystemId}:{spot.DestSystemId}:{spot.SkuLabel}",
        "goods-charter",
        $"{spot.OriginName} → {spot.DestName} · {spot.SkuLabel} ×{spot.Quantity:0}",
        $"Firm escrow · pays {pay:0} on delivery · lift {lift:0} · net Δ{spot.Margin:0.#} · [{spot.Profile}]",
        spot.OriginSystemId,
        CanAcceptHere: spot.AtOrigin,
        CanRefuse: false,
        DestSystemId: spot.DestSystemId,
        SkuLabel: spot.SkuLabel,
        Quantity: spot.Quantity,
        LiftLimit: spot.LiftLimit,
        DestBid: spot.DestBid,
        Profile: spot.Profile,
        Margin: spot.Margin));
    }

    return list;
  }

  /// <summary>Dock HubOrder tape (asks to buy into, bids to sell into) at the current system.</summary>
  public static IReadOnlyList<MarketLot> ListMarket(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    string currentSystemId,
    int take = 24)
  {
    if (!ids.Sites.TryGetValue(currentSystemId, out var site))
    {
      return [];
    }

    var loc = site.Hub.LocationId;
    var list = new List<MarketLot>();
    foreach (var o in SnapshotHubOrders(sim.State.World))
    {
      if (o.IsFilled || o.FirmId.Equals(ids.Carrier) || !o.LocationId.Equals(loc))
      {
        continue;
      }

      if (!(o.ProductId.Equals(ids.Ore) || o.ProductId.Equals(ids.Parts) || o.ProductId.Equals(ids.Goods)))
      {
        continue;
      }

      var sku = CampaignWorld.SkuLabel(o.ProductId, ids);
      var isAsk = o.Side == HubOrderSide.Sell;
      list.Add(new MarketLot(
        o.Id,
        isAsk ? "ASK" : "BID",
        isAsk,
        sku,
        o.ProductId,
        o.FirmId,
        o.Remaining.Value,
        o.LimitPrice.Amount,
        $"{(isAsk ? "ASK" : "BID")} {sku} ×{o.Remaining.Value:0} @ {o.LimitPrice.Amount:0.##}"));
    }

    return list
      .OrderBy(l => l.IsAsk ? 0 : 1)
      .ThenBy(l => l.SkuLabel, StringComparer.OrdinalIgnoreCase)
      .ThenBy(l => l.IsAsk ? l.UnitPrice : -l.UnitPrice)
      .Take(take)
      .ToList();
  }

  /// <summary>Calypso inventory quantity at a hub (freight SKUs, not fuel).</summary>
  public static decimal DockStock(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    string systemId)
  {
    if (!ids.Sites.TryGetValue(systemId, out var site))
    {
      return 0m;
    }

    var inv = sim.State.World.Inventory;
    var loc = site.Hub.LocationId;
    var sum = 0m;
    foreach (var p in new[] { ids.Ore, ids.Parts, ids.Goods })
    {
      sum += inv.GetQuantity(new InventoryKey(ids.Carrier, loc, p)).Value;
    }

    return sum;
  }

  private enum SpotLane
  {
    Freight,
    Goods,
    All,
  }

  public static IReadOnlyList<SpotCandidate> ListSpotFromMeshDigests(
    MeshState mesh,
    string currentSystemId,
    int take = 16,
    MeshNodeId? evaluationNode = null)
  {
    var node = evaluationNode
               ?? (mesh.TryGetMailbox(MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId), out var box)
                 ? box.LocationNodeId
                 : MeshNodeId.From(currentSystemId));

    var jobs = new List<SpotCandidate>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var packet in MeshCaptainInbox.SpotDigestsInInbox(mesh))
    {
      foreach (var spot in SpotDigestCodec.ParseBody(packet.Body, currentSystemId))
      {
        var key = SpotJobKeys.ForOffer(spot);
        if (mesh.IsRetractedAt(key, node))
        {
          continue;
        }

        if (!seen.Add(key))
        {
          continue;
        }

        jobs.Add(spot with { LogicalKey = key });
      }
    }

    return jobs
      .OrderByDescending(j => j.AtOrigin)
      .ThenByDescending(j => j.Margin)
      .Take(take)
      .ToList();
  }

  private static IReadOnlyList<SpotCandidate> BuildSpot(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string currentSystemId,
    HashSet<string>? allowOrigins,
    int take,
    SpotLane lane = SpotLane.All)
  {
    var world = sim.State.World;
    var wage = world.Policy.WageRatePerHour;
    var fuelCost = world.TransportFuelUnitCost;
    var siteByLoc = ids.Sites.Values.ToDictionary(s => s.Hub.LocationId);
    var freight = lane switch
    {
      SpotLane.Freight => new[] { ids.Ore, ids.Parts },
      SpotLane.Goods => new[] { ids.Goods },
      _ => new[] { ids.Ore, ids.Parts, ids.Goods },
    };
    var sells = new Dictionary<ProductId, List<HubOrder>>();
    var buys = new Dictionary<ProductId, List<HubOrder>>();

    // Snapshot: HubOrders mutates on the sim thread while the desk refreshes on the UI thread.
    foreach (var o in SnapshotHubOrders(world))
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

    var cash = world.Ledgers.TryGetValue(ids.Carrier, out var carrierLed)
      ? carrierLed.Cash.Amount
      : 0m;
    var calypso = ids.Registry.TryGet(ids.Carrier);
    var premium = calypso is null ? 0m : ids.Registry.QuotePremiumDue(calypso);
    var cashGate = premium > 0m && cash < premium * 2m + (calypso?.PremiumPayable ?? 0m);

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
            ? "AT DOCK"
            : $"{EstimateLyHint(ids, currentSystemId, origin.Hub.SystemId)} · steam risk";
          if (cashGate)
          {
            distHint += " · cash gate";
          }

          var summary =
            $"pay {qty * buy.LimitPrice.Amount:0} · lift {qty * lift:0} · Δ{margin:0.#} haul {est.TotalVariableCost.Amount:0} [{haulProfile}]";
          if (cashGate)
          {
            summary += " · cash gate";
          }

          if (!atOrigin)
          {
            summary += " · steam risk";
          }

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
            summary,
            atOrigin,
            distHint,
            SpotJobKeys.ForOffer(
              origin.Hub.SystemId, dest.Hub.SystemId, sku, qty, lift, buy.LimitPrice.Amount)));
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

  /// <summary>
  /// Concurrent List mutation (sim MatchHubOrders vs UI desk) can yield null slots in ToArray.
  /// Never iterate HubOrders without this on a UI/cross-thread path.
  /// </summary>
  internal static HubOrder[] SnapshotHubOrders(EconomyWorld world)
  {
    HubOrder[] snap;
    try
    {
      snap = world.HubOrders.ToArray();
    }
    catch (ArgumentException)
    {
      snap = world.HubOrders.Where(static o => o is not null).ToArray()!;
    }
    catch (IndexOutOfRangeException)
    {
      snap = world.HubOrders.Where(static o => o is not null).ToArray()!;
    }

    if (snap.Length == 0)
    {
      return snap;
    }

    var nulls = 0;
    for (var i = 0; i < snap.Length; i++)
    {
      if (snap[i] is null)
      {
        nulls++;
      }
    }

    if (nulls == 0)
    {
      return snap;
    }

    var clean = new HubOrder[snap.Length - nulls];
    var w = 0;
    foreach (var o in snap)
    {
      if (o is not null)
      {
        clean[w++] = o;
      }
    }

    return clean;
  }

  // Back-compat shim for older call sites.
  public static IReadOnlyList<SpotCandidate> ListJobs(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    TransitProfile profile,
    string? localSystemId = null,
    bool localOnly = false,
    int take = 12) =>
    ListSpot(sim, ids, profile, localSystemId ?? "sol", dockOnly: localOnly, take: take);
}
