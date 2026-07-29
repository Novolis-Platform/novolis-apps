using Novolis.Astro.Abstractions;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.StarMap;
using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Calypso-centric desk projection: voyage + spot intel + charters + manifest.</summary>
internal sealed class CaptainDeskModel
{
  public required string SubtitleLine { get; init; }
  public required string HashLine { get; init; }
  public required string VoyageLine { get; init; }
  public required string HoldLine { get; init; }
  public required string HullLine { get; init; }
  public required string StandingLine { get; init; }
  public required string CashLine { get; init; }
  public required string DecisionLine { get; init; }
  public required string SoftFailLine { get; init; }
  public required string SurvivalLine { get; init; }
  public required string CurrentHubSystemId { get; init; }
  public required string CurrentHubName { get; init; }
  public required bool Underway { get; init; }
  public required TransitProfile Profile { get; init; }
  public required IReadOnlyList<StarMapPoint> MapPoints { get; init; }
  public required IReadOnlyList<StarMapEdge> MapEdges { get; init; }
  public required IReadOnlyDictionary<string, CampaignBriefingModel.HubDetail> HubDetails { get; init; }
  public required IReadOnlyList<FeedLine> Feed { get; init; }
  public required IReadOnlyList<ScorecardRow> Scorecard { get; init; }
  public required string ScorecardTitle { get; init; }
  public required IReadOnlyList<CaptainJobBoard.SpotCandidate> SpotJobs { get; init; }
  public required IReadOnlyList<CaptainJobBoard.CharterCandidate> Charters { get; init; }
  public required IReadOnlyList<string> ManifestLines { get; init; }
  public required decimal ManifestUsed { get; init; }
  public required IReadOnlyList<MetricRow> RegistryRows { get; init; }
  public required IReadOnlyList<MetricRow> MoneyRows { get; init; }
  public required IReadOnlyList<MetricRow> AgentRows { get; init; }
  public required bool StandbyOffer { get; init; }
  public required bool SoftFail { get; init; }
  public required int Day { get; init; }
  public required bool Complete { get; init; }
  public required bool DockedIdle { get; init; }

  // Compat for older binders
  public IReadOnlyList<CaptainJobBoard.SpotCandidate> Jobs => SpotJobs;

  public static CaptainDeskModel From(CampaignRunner.LiveSession session)
  {
    var sim = session.Sim;
    var ids = session.Ids;
    var day = sim.State.Clock.Date.DayIndex;
    var entry = ids.Registry.TryGet(ids.Carrier);
    var cash = sim.State.World.Ledgers.TryGetValue(ids.Carrier, out var led)
      ? led.Cash.Amount
      : 0m;
    var profile = session.Player.DefaultProfile;
    var currentHub = session.CurrentHubSystemId;
    var currentName = ids.Sites.TryGetValue(currentHub, out var site) ? site.Hub.Name : currentHub;

    var ship = sim.State.World.Shipments.FirstOrDefault(s =>
      !s.IsLegacy && s.FirmId.Equals(ids.Carrier) && s.Status == ShipmentStatus.InTransit);
    var underway = ship is not null;
    string voyage;
    if (ship is null)
    {
      voyage = $"BERTH · {currentName} ({currentHub})";
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

    var spot = CaptainJobBoard.ListSpot(
      sim, ids, profile, currentHub, berthOnly: session.Player.LocalBoardOnly);
    var charters = CaptainJobBoard.ListCharters(sim, ids, session.Player, currentHub);
    var manifestLines = session.Player.Manifest.Lots
      .Select(l => $"{l.SkuLabel} → {l.DestSystemId} ×{l.Quantity:0} [{l.Profile}]")
      .ToList();
    var hold =
      $"hold {session.Player.Manifest.Used:0}/{CampaignWorld.HullCargoCapacity:0} · room {session.Player.Manifest.Room:0}";

    var catalog = SinsCatalog.Load();
    var hubDetails = new Dictionary<string, CampaignBriefingModel.HubDetail>(StringComparer.OrdinalIgnoreCase);
    var points = new List<StarMapPoint>(ids.Bridge.Hubs.Count);
    foreach (var hub in ids.Bridge.Hubs)
    {
      hubDetails[hub.SystemId] = new CampaignBriefingModel.HubDetail(
        hub.SystemId,
        hub.Name,
        hub.Role.ToString(),
        $"Ag {hub.Profile.Potential.Agriculture:0.##} · Ind {hub.Profile.Potential.Industry:0.##} · Mine {hub.Profile.Potential.Mining:0.##}");

      if (!catalog.TryGet(new SystemId(hub.SystemId), out var star) || star is null)
      {
        continue;
      }

      points.Add(new StarMapPoint
      {
        Id = hub.SystemId,
        Label = $"{hub.Name} ({hub.Role})",
        X = star.Coords.X,
        Y = star.Coords.Y,
      });
    }

    var edges = new List<StarMapEdge>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var (fromId, list) in ids.Bridge.Graph.Adjacency)
    {
      foreach (var edge in list)
      {
        var a = fromId;
        var b = edge.To.Value;
        var key = string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
        if (!seen.Add(key))
        {
          continue;
        }

        edges.Add(new StarMapEdge
        {
          FromId = a,
          ToId = b,
          BandTag = edge.BandTag,
        });
      }
    }

    var feed = new List<FeedLine>();
    foreach (var m in session.Milestones.Entries.OrderBy(e => e.Day).ThenBy(e => e.Kind).TakeLast(40))
    {
      var vox = VoxBank.ForMilestone(m.Kind, m.Detail);
      feed.Add(new FeedLine(vox.Voice, vox.Text, $"d{m.Day}"));
    }

    var lifeHits = LifeMoments.Count(session.Milestones);
    var scoreRows = LifeMoments.Score(session.Milestones)
      .Select(s => new ScorecardRow(s.Kind, s.Hits, s.Hook))
      .ToList();

    var decision = session.Agents.CarrierPulse.LastDecision;
    var premium = entry is null ? 0m : ids.Registry.QuoteDailyPremium(entry);
    var hull = entry is null
      ? "—"
      : $"life {entry.LifeFraction * 100m:0}% · premium {premium:0.#}/d · lien {entry.LienPrincipal:0} · OH {entry.OverhaulCount}";
    var standing = entry?.StandingLabel ?? "—";
    var soft = session.Player.SoftFailRaised
      ? $"SOFT FAIL — grounded {session.Player.SoftFailGroundedDays}d"
      : session.Player.SoftFailGroundedDays > 0
        ? $"grounded streak {session.Player.SoftFailGroundedDays}d"
        : "";

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
      new("Escrow open", $"{ids.Escrow.OpenCount}"),
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

    return new CaptainDeskModel
    {
      SubtitleLine =
        $"{CampaignWorld.PlayerMasterLabel} · day {day} · {DurationArg.Format(session.HoursDone)}/{DurationArg.Format(session.RequestedHours)} · intel {(session.Player.LocalBoardOnly ? "berth" : "network")}",
      HashLine = $"hash {sim.State.Hash:X16} · hubs {ids.Bridge.Hubs.Count} · {CampaignWorld.PlayerFlavorId}",
      VoyageLine = voyage,
      HoldLine = hold,
      HullLine = hull,
      StandingLine = standing,
      CashLine = $"{cash:0.####}",
      DecisionLine = decision,
      SoftFailLine = soft,
      SurvivalLine = TrampSurvival.FormatLine(
        session.Player.LastSurvival ?? TrampSurvival.Capture(ids),
        session.Player.LastTrampMode,
        session.Player.LastTrampWon,
        session.Player.LastTrampLost),
      CurrentHubSystemId = currentHub,
      CurrentHubName = currentName,
      Underway = underway,
      Profile = profile,
      MapPoints = points,
      MapEdges = edges,
      HubDetails = hubDetails,
      Feed = feed,
      Scorecard = scoreRows,
      ScorecardTitle = $"Life moments {lifeHits}/{LifeMoments.Kinds.Length}",
      SpotJobs = spot,
      Charters = charters,
      ManifestLines = manifestLines,
      ManifestUsed = session.Player.Manifest.Used,
      RegistryRows = registry,
      MoneyRows = money,
      AgentRows = agentRows,
      StandbyOffer = standby,
      SoftFail = session.Player.SoftFailRaised,
      Day = day,
      Complete = session.IsComplete,
      DockedIdle = dockedIdle,
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
