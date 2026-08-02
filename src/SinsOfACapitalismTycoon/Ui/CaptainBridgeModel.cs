using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.StarMap;
using Novolis.Economy;
using Novolis.Economy.Finance;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Calypso-centric captain projection: voyage + spot intel + charters + manifest.</summary>
internal sealed class CaptainBridgeModel
{
  public required string SubtitleLine { get; init; }
  public required string HashLine { get; init; }
  public required string VoyageLine { get; init; }
  public required string HoldLine { get; init; }
  public required string HullLine { get; init; }
  public required string StandingLine { get; init; }
  public required string CashLine { get; init; }
  /// <summary>Cap+ bleed visibility: cash ÷ daily premium (~days of standing).</summary>
  public required string RunwayLine { get; init; }
  public decimal RunwayDays { get; init; }
  public decimal DailyPremium { get; init; }
  public decimal ReputationScore { get; init; }
  /// <summary>TTD clockwork: open CCA principal while underway / staged.</summary>
  public required string EscrowClockLine { get; init; }
  public decimal EscrowPending { get; init; }
  public required string DecisionLine { get; init; }
  public required string CoachLine { get; init; }
  public required string SoftFailLine { get; init; }
  public required string SurvivalLine { get; init; }
  public required string MeshLine { get; init; }
  public required string CurrentSystemId { get; init; }
  public required string CurrentSystemName { get; init; }
  public required bool Underway { get; init; }
  public required TransitProfile Profile { get; init; }
  public required IReadOnlyList<StarMapPoint> MapPoints { get; init; }
  public required IReadOnlyList<StarMapEdge> MapEdges { get; init; }
  public required IReadOnlyList<StarMapEdge> UnderwayRoute { get; init; }
  public required IReadOnlyDictionary<string, CampaignBriefingModel.HubDetail> HubDetails { get; init; }
  public required IReadOnlyList<FeedLine> Feed { get; init; }
  public required IReadOnlyList<ScorecardRow> Scorecard { get; init; }
  public required string ScorecardTitle { get; init; }
  public required IReadOnlyList<CaptainJobBoard.SpotCandidate> SpotJobs { get; init; }
  /// <summary>Never-empty berth wagers (Local / Rumor / Wait).</summary>
  public required IReadOnlyList<BerthOffer> BerthOffers { get; init; }
  public required bool MeshBoardUnlocked { get; init; }
  public required IReadOnlyList<CaptainJobBoard.CharterCandidate> Charters { get; init; }
  public required IReadOnlyList<CaptainJobBoard.MarketLot> MarketLots { get; init; }
  public required IReadOnlyList<string> ManifestLines { get; init; }
  public required decimal ManifestUsed { get; init; }
  public required decimal DockStock { get; init; }
  public required IReadOnlyList<MetricRow> RegistryRows { get; init; }
  public required IReadOnlyList<MetricRow> MoneyRows { get; init; }
  public required IReadOnlyList<MetricRow> AgentRows { get; init; }
  public required bool StandbyOffer { get; init; }
  public required bool SoftFail { get; init; }
  public required int Day { get; init; }
  public required bool Complete { get; init; }
  public required bool DockedIdle { get; init; }
  public PlayerActionResult? LastAction { get; init; }
  public string? TravelTargetSystemId { get; init; }
  /// <summary>Best empty-steam destination when the berth has no freight (coach / Travel arm).</summary>
  public string? SuggestedTravelSystemId { get; init; }
  public required IReadOnlyList<string> RouteSystemIds { get; init; }
  /// <summary>True when staged manifest lots match the current dock (can depart).</summary>
  public bool ManifestAtCurrentDock { get; init; }
  /// <summary>Origin system of the first staged lot (travel here before depart if elsewhere).</summary>
  public string? ManifestOriginSystemId { get; init; }
  public double ShipMapX { get; init; }
  public double ShipMapY { get; init; }
  public bool ShipMapVisible { get; init; }
  public required IReadOnlyList<string> IntentStackLines { get; init; }
  public required string AttentionLine { get; init; }
  public double SimSpeedScale { get; init; }
  /// <summary>Recent game hours advanced per real minute.</summary>
  public double GameHoursPerRealMinute { get; init; }
  /// <summary>Session-average game hours per real minute.</summary>
  public double SessionGameHoursPerRealMinute { get; init; }
  public required string PaceLine { get; init; }

  // Compat for older binders
  public IReadOnlyList<CaptainJobBoard.SpotCandidate> Jobs => SpotJobs;

  public static CaptainBridgeModel From(CampaignRunner.LiveSession session)
  {
    var sim = session.Sim;
    var ids = session.Ids;
    if (session.Player.Enabled)
    {
      MeshBoardUnlock.Sync(session.Player, session.Milestones);
    }

    var day = sim.State.Clock.Date.DayIndex;
    var entry = ids.Registry.TryGet(ids.Carrier);
    var cash = sim.State.World.Ledgers.TryGetValue(ids.Carrier, out var led)
      ? led.Cash.Amount
      : 0m;
    var profile = session.Player.DefaultProfile;
    var currentSystem = session.CurrentSystemId;
    var currentName = ids.Sites.TryGetValue(currentSystem, out var site) ? site.Hub.Name : currentSystem;

    var ship = sim.State.World.Shipments.FirstOrDefault(s =>
      !s.IsLegacy && s.FirmId.Equals(ids.Carrier) && s.Status == ShipmentStatus.InTransit);
    var underway = ship is not null;
    string voyage;
    if (ship is null)
    {
      voyage = $"DOCK · {currentName} ({currentSystem})";
    }
    else if (ship.Quantity.Value <= 0m)
    {
      voyage =
        $"REPOSITION · {ship.Phase} @ {HubLabel(ids, ship.CurrentHubId)} · [{ship.TransitProfile}]";
    }
    else
    {
      voyage =
        $"UNDERWAY · {CampaignWorld.SkuLabel(ship.ProductId, ids)} ×{ship.Quantity.Value:0} · {ship.Phase} @ {HubLabel(ids, ship.CurrentHubId)} · [{ship.TransitProfile}]";
    }

    if (ship is null
        && day <= CampaignWorld.LienServiceGraceDays
        && entry is { LienPrincipal: > 0.05m })
    {
      var left = Math.Max(0, CampaignWorld.LienServiceGraceDays - day);
      voyage += $" · Grace {left}d · then lien bites";
    }

    var spotBoard = CaptainJobBoard.ListSpot(
      sim, ids, profile, currentSystem, dockOnly: session.Player.DockBoardOnly, mesh: ids.Mesh);
    // Live freight for coach / barren-dock rumors (Dock filter only via BerthOfferBoard).
    var live = CaptainJobBoard.ListLiveFreight(sim, ids, profile, currentSystem, take: 24);
    var suggestedTravel = live.FirstOrDefault(s => !s.AtOrigin && s.Margin > 8m)?.OriginSystemId
                          ?? live.FirstOrDefault(s => !s.AtOrigin)?.OriginSystemId;
    var (berthOffers, spot) = BerthOfferBoard.Build(
      spotBoard, live, dockBoardOnly: session.Player.DockBoardOnly);
    // Mesh filter: never silently fill with live lots (that made Mesh≡Dock).

    var charters = CaptainJobBoard.ListCharters(sim, ids, session.Player, currentSystem);
    var market = CaptainJobBoard.ListMarket(sim, ids, currentSystem);
    var dockStock = CaptainJobBoard.DockStock(sim, ids, currentSystem);
    var meshSnap = MeshCaptainInbox.ForCaptain(ids.Mesh);
    var inFtl = MeshMailboxSync.IsInFtl(ship);
    var manifestLines = session.Player.Manifest.Lots
      .Select(l =>
        $"{l.SkuLabel} → {l.DestSystemId} ×{l.Quantity:0} · pay {l.DestBid * l.Quantity:0} · lift {l.LiftLimit * l.Quantity:0} [{l.Profile}]")
      .ToList();
    var room = Math.Max(0m, CampaignWorld.HullCargoCapacity - session.Player.Manifest.Used - dockStock);
    var hold =
      $"hold manifest {session.Player.Manifest.Used:0}/{CampaignWorld.HullCargoCapacity:0} · dock stock {dockStock:0} · room {room:0}";

    var underwayRoute = ship is null
      ? Array.Empty<StarMapEdge>()
      : RouteHighlight.FromShipment(ids, sim.State.World, ship);

    var (points, edges, hubDetails) = CaptainMapProjection.Build(ids);

    var feed = new List<FeedLine>();
    if (inFtl)
    {
      // Mesh panel freezes in FTL — no node link until in-system again.
      feed.Add(new FeedLine(
        "vox.mesh",
        "Mesh offline · FTL — no node link until in-system",
        "mesh"));
      if (session.LastBridge is { Feed: { Count: > 0 } prevFeed })
      {
        foreach (var line in prevFeed)
        {
          if (line.Voice.Equals("vox.mesh", StringComparison.OrdinalIgnoreCase)
              && line.Text.Contains("Mesh offline", StringComparison.OrdinalIgnoreCase))
          {
            continue;
          }

          feed.Add(line);
        }
      }
    }
    else
    {
      foreach (var subject in meshSnap.RecentSubjects.Take(6))
      {
        feed.Add(new FeedLine("vox.mesh", subject, "mesh"));
      }

      if (!session.Player.DockBoardOnly)
      {
        feed.Add(new FeedLine(
          "vox.mesh",
          $"Mesh intel is delayed · inbox {meshSnap.FeedInboxCount} · digests {meshSnap.SpotDigestCount}",
          "mesh"));
      }

      foreach (var m in session.Milestones.Entries.OrderBy(e => e.Day).ThenBy(e => e.Kind).TakeLast(40))
      {
        var vox = VoxBank.ForMilestone(m.Kind, m.Detail);
        feed.Add(new FeedLine(vox.Voice, vox.Text, $"d{m.Day}"));
      }
    }

    var lifeHits = LifeMoments.Count(session.Milestones);
    var scoreRows = LifeMoments.Score(session.Milestones)
      .Select(s => new ScorecardRow(s.Kind, s.Hits, s.Hook))
      .ToList();

    var decision = session.Agents.CarrierPulse.LastDecision;
    var listPremium = entry is null ? 0m : ids.Registry.QuotePremiumDue(entry);
    var operating = InsurancePulse.IsOperating(sim.State.World, ids.Carrier);
    var premium = operating
      ? listPremium
      : listPremium * CampaignWorld.IdleStandingPremiumFactor;
    var premiumHint = operating ? $"{premium:0.#}/d" : $"{premium:0.#}/d idle";
    var payable = entry?.PremiumPayable ?? 0m;
    var runwayDays = premium > 0.05m
      ? Math.Round(cash / premium, 1, MidpointRounding.AwayFromZero)
      : 999m;
    var runwayLine = premium > 0.05m
      ? $"~{runwayDays:0.#}d runway @ {premiumHint}"
      : "runway —";
    var repScore = ids.Reputation.Get(ids.Carrier);
    var escrowPending = ids.Escrow.OpenPrincipalFor(ids.Carrier);
    if (escrowPending < 0.5m && session.Player.Manifest.Lots.Count > 0)
    {
      escrowPending = session.Player.Manifest.Lots.Sum(l => l.DestBid * l.Quantity);
    }

    var escrowAge = ids.Escrow.OpenAgeDays(ids.Carrier, day);
    var escrowClock = escrowPending > 0.5m
      ? (underway
        ? $"CCA pending {escrowPending:0} · day {escrowAge} underway"
        : $"CCA staged ~{escrowPending:0} · opens on sail")
      : "";
    // LifeFraction is wear used; bridge shows remaining drive life (100% = fresh).
    var lifeRemain = entry is null ? 0m : Math.Clamp((1m - entry.LifeFraction) * 100m, 0m, 100m);
    var hull = entry is null
      ? "—"
      : payable > 0.05m
        ? $"life {lifeRemain:0}% · premium {premiumHint} · payable {payable:0.#} · lien {entry.LienPrincipal:0} · OH {entry.OverhaulCount}"
        : $"life {lifeRemain:0}% · premium {premiumHint} · lien {entry.LienPrincipal:0} · OH {entry.OverhaulCount}";
    if (!string.IsNullOrEmpty(escrowClock))
    {
      hull += $"\n{escrowClock}";
    }

    var standing = entry?.StandingLabel ?? "—";
    var advice = CaptainCoach.For(session);
    var soft = advice.SoftFailEnrichment;

    var registry = ids.Registry.Entries
      .OrderBy(e => e.RegistryName)
      .Select(e => new MetricRow(
        e.RegistryName,
        e.StandingLabel,
        $"life {e.LifeFraction * 100m:0}% · P/S/Std {e.PriorityLegs}/{e.SlowLegs}/{e.StandardLegs}"))
      .ToList();

    var money = new List<MetricRow>
    {
      new(CampaignWorld.PlayerHullName, $"{cash:0}", standing),
      new("Ops liquid", $"{session.Credits.LiquidStock:0}"),
      new("Claims / load", $"{ids.Registry.ClaimsPaid:0} / {ids.Registry.ActuarialLoad:0.##}"),
      new("Escrow open", escrowPending > 0.5m
        ? $"{ids.Escrow.OpenCount} · CCA {escrowPending:0}"
        : $"{ids.Escrow.OpenCount}"),
      new("Reputation", $"{repScore:0}"),
      new("Mesh inbox", inFtl
        ? "offline · FTL"
        : $"{meshSnap.FeedInboxCount} · em {meshSnap.EmergencyCount} · digests {meshSnap.SpotDigestCount}"),
    };

    var agentRows = new List<MetricRow>
    {
      new(CampaignWorld.PlayerHullName, decision),
      new("Mining", session.Agents.Mining.LastDecision),
      new("Industry", session.Agents.Industry.LastDecision),
      new("Station", session.Agents.Station.LastDecision),
      new("Mega", session.Agents.MegaHauler.LastDecision),
    };

    var standby = session.Player.Opportunities?.ActiveStandbyTramp?.Equals(ids.Carrier) == true;
    var dockedIdle = !underway
                     && !sim.State.World.PendingPlanShipments.Any(p => p.FirmId.Equals(ids.Carrier))
                     && !sim.State.World.PendingPlanRepositions.Any(p => p.FirmId.Equals(ids.Carrier));

    var travelTarget = session.Player.TravelTargetSystemId;
    // Selecting the current system on the map used to arm Travel → "already at dock" soft-lock.
    if (string.IsNullOrEmpty(travelTarget)
        || travelTarget.Equals(currentSystem, StringComparison.OrdinalIgnoreCase))
    {
      travelTarget = suggestedTravel;
      if (!string.IsNullOrEmpty(suggestedTravel))
      {
        session.Player.TravelTargetSystemId = suggestedTravel;
      }
    }

    var manifestOrigin = session.Player.Manifest.Lots.Count > 0
      ? session.Player.Manifest.Lots[0].OriginSystemId
      : null;
    var manifestAtDock = string.IsNullOrEmpty(manifestOrigin)
      || manifestOrigin.Equals(currentSystem, StringComparison.OrdinalIgnoreCase);
    var routeIds = new List<string>();
    if (underwayRoute.Count > 0)
    {
      foreach (var e in underwayRoute)
      {
        if (routeIds.Count == 0) routeIds.Add(e.FromId);
        routeIds.Add(e.ToId);
      }
    }
    else if (!string.IsNullOrEmpty(travelTarget))
    {
      var planned = RouteHighlight.BetweenSystems(ids, currentSystem, travelTarget);
      foreach (var e in planned)
      {
        if (routeIds.Count == 0) routeIds.Add(e.FromId);
        routeIds.Add(e.ToId);
      }
    }

    var (shipX, shipY, shipVis) = ShipMapPose.Compute(ids, sim.State.World, currentSystem, ship, points);

    return new CaptainBridgeModel
    {
      SubtitleLine =
        $"{CampaignWorld.PlayerMasterLabel} · day {day} · {DurationArg.Format(session.HoursDone)}/{DurationArg.Format(session.RequestedHours)} · {session.PaceLine} · intel {(session.Player.DockBoardOnly ? "dock" : "mesh")}",
      HashLine = session.PreferMaxSpeedThroughput
        ? $"hash — · systems {ids.Bridge.Hubs.Count} · {CampaignWorld.PlayerFlavorId}"
        : $"hash {sim.State.Hash:X16} · systems {ids.Bridge.Hubs.Count} · {CampaignWorld.PlayerFlavorId}",
      VoyageLine = voyage,
      HoldLine = hold,
      HullLine = hull,
      StandingLine = standing,
      CashLine = $"{cash:0.####}",
      RunwayLine = runwayLine,
      RunwayDays = runwayDays,
      DailyPremium = premium,
      ReputationScore = repScore,
      EscrowClockLine = escrowClock,
      EscrowPending = escrowPending,
      DecisionLine = decision,
      CoachLine = advice.CoachLine,
      SoftFailLine = soft,
      SurvivalLine = TrampSurvival.FormatLine(
        session.Player.LastSurvival ?? TrampSurvival.Capture(ids),
        session.Player.LastTrampMode,
        session.Player.LastTrampWon,
        session.Player.LastTrampLost,
        day,
        ids),
      MeshLine = inFtl
        ? "mesh offline · FTL — no node link"
        : $"mesh inbox {meshSnap.FeedInboxCount} · emergency {meshSnap.EmergencyCount} · mesh digests {meshSnap.SpotDigestCount} · push {meshSnap.MailboxPushCount}",
      CurrentSystemId = currentSystem,
      CurrentSystemName = currentName,
      Underway = underway,
      Profile = profile,
      MapPoints = points,
      MapEdges = edges,
      UnderwayRoute = underwayRoute,
      HubDetails = hubDetails,
      Feed = feed,
      Scorecard = scoreRows,
      ScorecardTitle = $"Life moments {lifeHits}/{LifeMoments.Kinds.Length}",
      SpotJobs = spot,
      BerthOffers = berthOffers,
      MeshBoardUnlocked = session.Player.MeshBoardUnlocked,
      Charters = charters,
      MarketLots = market,
      ManifestLines = manifestLines,
      ManifestUsed = session.Player.Manifest.Used,
      DockStock = dockStock,
      RegistryRows = registry,
      MoneyRows = money,
      AgentRows = agentRows,
      StandbyOffer = standby,
      SoftFail = session.Player.SoftFailRaised,
      Day = day,
      Complete = session.IsComplete,
      DockedIdle = dockedIdle,
      LastAction = session.Player.LastAction,
      TravelTargetSystemId = !manifestAtDock && !string.IsNullOrEmpty(manifestOrigin)
        ? manifestOrigin
        : travelTarget,
      SuggestedTravelSystemId = suggestedTravel,
      RouteSystemIds = routeIds,
      ManifestAtCurrentDock = manifestAtDock,
      ManifestOriginSystemId = manifestOrigin,
      ShipMapX = shipX,
      ShipMapY = shipY,
      ShipMapVisible = shipVis,
      IntentStackLines = session.Player.IntentStack.FormatLines(),
      AttentionLine = SessionClock.FormatAttention(session.Player.Attention),
      SimSpeedScale = session.Player.SimSpeedScale,
      GameHoursPerRealMinute = session.GameHoursPerRealMinute,
      SessionGameHoursPerRealMinute = session.SessionGameHoursPerRealMinute,
      PaceLine = session.PaceLine,
    };
  }

  private static string HubLabel(CampaignWorld.Ids ids, TransportHubId hubId)
  {
    foreach (var s in ids.Sites.Values)
    {
      if (s.Hub.HubId.Equals(hubId))
      {
        return s.Hub.Name;
      }
    }

    return "?";
  }
}
