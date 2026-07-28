using Novolis.Astro.Abstractions;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.StarMap;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation.Extensions;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Immutable UI projection of a finished campaign run (no Economy types in controls).</summary>
internal sealed class CampaignBriefingModel
{
  public required string TitleLine { get; init; }
  public required string SubtitleLine { get; init; }
  public required string HashLine { get; init; }
  public required IReadOnlyList<StarMapPoint> MapPoints { get; init; }
  public required IReadOnlyList<StarMapEdge> MapEdges { get; init; }
  public required IReadOnlyDictionary<string, HubDetail> HubDetails { get; init; }
  public required IReadOnlyList<FeedLine> Feed { get; init; }
  public required IReadOnlyList<ScorecardRow> Scorecard { get; init; }
  public required string ScorecardTitle { get; init; }
  public required string OpsCash { get; init; }
  public required string OpsNote { get; init; }
  public required string CoreCash { get; init; }
  public required string CoreNote { get; init; }
  public required IReadOnlyList<MetricRow> RegistryRows { get; init; }
  public required IReadOnlyList<MetricRow> LogisticsRows { get; init; }
  public required IReadOnlyList<MetricRow> MoneyRows { get; init; }
  public required IReadOnlyList<MetricRow> AgentRows { get; init; }
  public required IReadOnlyList<MetricRow> MegaRows { get; init; }
  public required string CurtainLine { get; init; }
  public required string RawReport { get; init; }
  public required int LifeMomentHits { get; init; }
  public required int MilestoneCount { get; init; }

  public sealed record HubDetail(string SystemId, string Name, string Role, string ProfileHint);

  public static CampaignBriefingModel From(CampaignRunner.Result result)
  {
    var sim = result.Sim;
    var ids = result.Ids;
    var credits = result.Credits;
    var agents = result.Agents;
    var world = sim.State.World;
    var day = sim.State.Clock.Date.DayIndex;
    var worldSnap = world.ToReportSnapshot();
    var lifeHits = LifeMoments.Count(result.Milestones);
    var milestoneCount = result.Milestones.Entries.Count;

    var catalog = SinsCatalog.Load();
    var hubDetails = new Dictionary<string, HubDetail>(StringComparer.OrdinalIgnoreCase);
    var points = new List<StarMapPoint>(ids.Bridge.Hubs.Count);
    foreach (var hub in ids.Bridge.Hubs)
    {
      hubDetails[hub.SystemId] = new HubDetail(
        hub.SystemId,
        hub.Name,
        hub.Role.ToString(),
        $"Ag {hub.Profile.Potential.Agriculture:0.##} · Ind {hub.Profile.Potential.Industry:0.##} · Mine {hub.Profile.Potential.Mining:0.##}");

      if (!catalog.TryGet(new SystemId(hub.SystemId), out var star) || star is null)
        continue;

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
          continue;
        edges.Add(new StarMapEdge
        {
          FromId = a,
          ToId = b,
          BandTag = edge.BandTag,
        });
      }
    }

    var feed = new List<FeedLine>();
    foreach (var line in VoxBank.SessionOverture(sim.State.Seed, result.RequestedHours, result.DramaEnabled))
      feed.Add(new FeedLine(line.Voice, line.Text));

    foreach (var m in result.Milestones.Entries.OrderBy(e => e.Day).ThenBy(e => e.Kind))
    {
      var vox = VoxBank.ForMilestone(m.Kind, m.Detail);
      feed.Add(new FeedLine(vox.Voice, vox.Text, $"d{m.Day}"));
    }

    var curtain = VoxBank.SessionCurtain(milestoneCount, lifeHits);
    feed.Add(new FeedLine(curtain.Voice, curtain.Text, "end"));

    var scoreRows = LifeMoments.Score(result.Milestones)
      .Select(s => new ScorecardRow(s.Kind, s.Hits, s.Hook))
      .ToList();

    var opsCash = $"{worldSnap.Ops.Ledgers.OpsTotalCash.Amount:0.####}";
    var opsNote =
      $"firms {worldSnap.Ops.Ledgers.FirmCount} · invoice AR {worldSnap.Ops.Ledgers.InvoiceOpenReceivables.Amount:0}";
    string coreCash;
    string coreNote;
    if (worldSnap.Core is { } core)
    {
      coreCash = $"{core.Snapshot.TotalCash.Amount:0.####}";
      coreNote =
        $"deposits {core.Snapshot.TotalDeposits.Amount:0} · broad {core.Snapshot.BroadMoney.Amount:0}";
    }
    else
    {
      coreCash = "—";
      coreNote = "(no Core entities)";
    }

    var registry = ids.Registry.Entries
      .OrderBy(e => e.RegistryName)
      .Select(e => new MetricRow(
        e.RegistryName,
        e.StandingLabel,
        $"life {e.LifeFraction * 100m:0}% · OH {e.OverhaulCount} · P/S/Std {e.PriorityLegs}/{e.SlowLegs}/{e.StandardLegs}"))
      .ToList();

    var waiting = world.Shipments.Count(s => !s.IsLegacy && s.Phase == ShipmentPhase.WaitingBerth);
    var loading = world.Shipments.Count(s => !s.IsLegacy && s.Phase == ShipmentPhase.Loading);
    var logistics = new List<MetricRow>
    {
      new("Shipments", $"{worldSnap.Ops.Logistics.ShipmentCount}"),
      new("Cargo in flight", $"{worldSnap.Ops.Logistics.CargoQuantityInFlight:0.####}"),
      new("WaitingBerth / Loading", $"{waiting} / {loading}"),
      new("Delivered / Departed", $"{world.TransportStats.CargoDelivered.Value:0} / {credits.Departed}"),
      new("Fuel burned", $"{world.TransportStats.FuelBurned.Value:0.#}"),
      new("Drive wear", $"{world.TransportStats.DriveWearAccumulated:0.#}"),
      new("Plan fails", $"{world.TransportStats.FailedPlans}"),
    };

    var hh = world.Cohorts.Sum(c => c.BudgetRemaining.Amount);
    var liquidDelta = credits.LiquidStock - result.OpeningLiquid;
    var money = new List<MetricRow>
    {
      new("Ops liquid", $"{credits.LiquidStock:0}", $"opening {result.OpeningLiquid:0} · Δ {liquidDelta:0}"),
      new("Household budgets", $"{hh:0}"),
      new("Inv book $", $"{credits.InventoryBookValue:0}"),
      new("Wages → hh", $"{credits.WagesDistributed:0}"),
      new("Imports / Exports", $"{credits.ImportSpend:0} / {credits.ExportRevenue:0}"),
      new("Tolls → treasury", $"{credits.TollsToTreasury:0.##}"),
      new("Claims paid (UW)", $"{ids.Registry.ClaimsPaid:0}"),
      new("Actuarial load", $"{ids.Registry.ActuarialLoad:0.##}"),
    };

    var agentRows = new List<MetricRow>
    {
      new("Mining", agents.Mining.LastDecision),
      new("Industry", agents.Industry.LastDecision),
      new("Station", agents.Station.LastDecision),
      new("Treasury", agents.Treasury.LastDecision),
      new("Capacity", agents.Capacity.LastDecision),
      new("Loan repay", agents.LoanRepay.LastDecision),
      new("Sol export", agents.SolExport.LastDecision),
      new("Mega hauler", agents.MegaHauler.LastDecision),
      new("Ventures", agents.VenturesAgent.LastDecision),
    };
    for (var i = 0; i < Math.Min(3, agents.Carriers.Count); i++)
    {
      var label = i == 0 ? "Carrier" : $"Tramp{i + 1}";
      agentRows.Add(new MetricRow(label, agents.Carriers[i].LastDecision));
    }

    var mega = result.Biographies.ForFirm(ids.MegaHauler).TakeLast(12)
      .Select(leg => new MetricRow(
        $"d{leg.Day}",
        $"{leg.Qty:0.#} {leg.Product}",
        $"{leg.Note} · {leg.Profile}"))
      .ToList();
    if (mega.Count == 0)
      mega.Add(new MetricRow("—", agents.MegaHauler.LastDecision, "no legs logged"));

    return new CampaignBriefingModel
    {
      TitleLine = "Sins of a Capitalism Tycoon",
      SubtitleLine =
        $"day {day} · {DurationArg.Format(result.RequestedHours)} · drama {(result.DramaEnabled ? "on" : "off")}",
      HashLine = $"hash {sim.State.Hash:X16} · wall {result.Wall.TotalSeconds:0.#}s · hubs {ids.Bridge.Hubs.Count}",
      MapPoints = points,
      MapEdges = edges,
      HubDetails = hubDetails,
      Feed = feed,
      Scorecard = scoreRows,
      ScorecardTitle = $"Life moments {lifeHits}/{LifeMoments.Kinds.Length}",
      OpsCash = opsCash,
      OpsNote = opsNote,
      CoreCash = coreCash,
      CoreNote = coreNote,
      RegistryRows = registry,
      LogisticsRows = logistics,
      MoneyRows = money,
      AgentRows = agentRows,
      MegaRows = mega,
      CurtainLine = VoxBank.Format(curtain),
      RawReport = CampaignRunner.FormatReport(result),
      LifeMomentHits = lifeHits,
      MilestoneCount = milestoneCount,
    };
  }
}
