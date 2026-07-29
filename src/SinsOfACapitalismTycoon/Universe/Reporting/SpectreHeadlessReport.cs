using Novolis.Economy;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Logistics;
using Novolis.Economy.Markets;
using Novolis.Economy.Simulation;
using Novolis.Economy.Simulation.Extensions;
using Spectre.Console;

namespace SinsOfACapitalismTycoon.Universe;

internal static class DurationArg
{
  public static bool TryParse(string? text, out long hours)
  {
    hours = 0;
    if (string.IsNullOrWhiteSpace(text))
    {
      return false;
    }

    text = text.Trim();
    var suffix = text[^1];
    var numberPart = char.IsLetter(suffix) ? text[..^1] : text;
    if (!decimal.TryParse(numberPart, System.Globalization.NumberStyles.Number,
          System.Globalization.CultureInfo.InvariantCulture, out var value)
        || value <= 0m)
    {
      return false;
    }

    hours = suffix switch
    {
      'd' or 'D' => (long)Math.Ceiling(value * SimulationHour.HoursPerDay),
      'h' or 'H' => (long)Math.Ceiling(value),
      _ when !char.IsLetter(suffix) => (long)Math.Ceiling(value),
      _ => 0,
    };
    return hours > 0;
  }

  public static string Format(long hours) =>
    hours % SimulationHour.HoursPerDay == 0
      ? $"{hours / SimulationHour.HoursPerDay}d ({hours}h)"
      : $"{hours}h (~{hours / (double)SimulationHour.HoursPerDay:0.#}d)";
}

/// <summary>Spectre-formatted campaign report (Ops vs Core never summed).</summary>
internal static class SpectreHeadlessReport
{
  public static void Write(IAnsiConsole console, CampaignRunner.Result result)
  {
    var sim = result.Sim;
    var ids = result.Ids;
    var credits = result.Credits;
    var agents = result.Agents;
    var world = sim.State.World;
    var hh = world.Cohorts.Sum(c => c.BudgetRemaining.Amount);
    var delivered = world.TransportStats.CargoDelivered.Value;
    var day = sim.State.Clock.Date.DayIndex;
    var liquidDelta = credits.LiquidStock - result.OpeningLiquid;
    var worldSnap = world.ToReportSnapshot();

    console.Write(new FigletText("Sins").Color(Color.Orange1));
    console.MarkupLine("[bold]Sins of a Capitalism Tycoon[/] — campaign report");
    console.Write(new Rule(
        $"day {day} · {DurationArg.Format(result.RequestedHours)} · hash {sim.State.Hash:X16} · drama {(result.DramaEnabled ? "on" : "off")}")
      .RuleStyle("grey"));

    console.Write(new Rule("Radio overture").RuleStyle("orange1"));
    foreach (var line in VoxBank.SessionOverture(
               sim.State.Seed, result.RequestedHours, result.DramaEnabled))
    {
      console.MarkupLine($"[grey]{Markup.Escape(VoxBank.Format(line))}[/]");
    }

    var header = new Table().Border(TableBorder.Rounded).AddColumns("Field", "Value");
    header.AddRow("Wall clock", $"{result.Wall.TotalSeconds:0.#}s");
    header.AddRow("Hubs / corridors", $"{ids.Bridge.Hubs.Count} / {world.Corridors.Count}");
    header.AddRow("Roles", ids.RoleSummary);
    var lowDeg = ids.Bridge.Hubs.Count(h =>
      ids.Bridge.Graph.Adjacency.TryGetValue(h.SystemId, out var e) && e.Count <= 2);
    header.AddRow("Low-degree hubs", $"{lowDeg}");
    header.AddRow(
      "Registries",
      $"ship {ids.Desk.Ships.Entries.Count} · firm {ids.Desk.Firms.Count} · license {ids.Desk.Licenses.Count}");
    console.Write(header);

    var money = new Table().Border(TableBorder.Simple).Title("Ops money (FirmLedgers)").AddColumns("Metric", "Amount");
    money.AddRow("Ops liquid", $"{credits.LiquidStock:0}");
    money.AddRow("Opening / Δ", $"{result.OpeningLiquid:0} / {liquidDelta:0}");
    money.AddRow("Household budgets", $"{hh:0}");
    money.AddRow("Inv book $", $"{credits.InventoryBookValue:0}");
    money.AddRow("Wages → hh", $"{credits.WagesDistributed:0}");
    money.AddRow("Imports / Exports", $"{credits.ImportSpend:0} / {credits.ExportRevenue:0}");
    money.AddRow("Tolls → treasury", $"{credits.TollsToTreasury:0.##}");
    money.AddRow("Claims paid (UW)", $"{ids.Registry.ClaimsPaid:0}");
    money.AddRow("Actuarial load", $"{ids.Registry.ActuarialLoad:0.##}");
    money.AddRow("Escrow open / released", $"{ids.Escrow.OpenCount} / {ids.Escrow.ReleasedTotal:0}");
    money.AddRow("Escrow fees / skim", $"{ids.Escrow.IssuerFeesTotal:0} / {ids.Escrow.ContractorSkimTotal:0}");
    money.AddRow("Escrow clawed", $"{ids.Escrow.ClawedTotal:0}");
    console.Write(money);

    console.Write(new Rule("Ops vs Core (never summed)").RuleStyle("yellow"));
    var books = new Table().Border(TableBorder.Simple).AddColumns("Layer", "Cash", "Notes");
    books.AddRow(
      "Ops",
      $"{worldSnap.Ops.Ledgers.OpsTotalCash.Amount:0.####}",
      $"firms {worldSnap.Ops.Ledgers.FirmCount} · invoice AR {worldSnap.Ops.Ledgers.InvoiceOpenReceivables.Amount:0}");
    if (worldSnap.Core is { } core)
    {
      books.AddRow(
        "Core",
        $"{core.Snapshot.TotalCash.Amount:0.####}",
        $"deposits {core.Snapshot.TotalDeposits.Amount:0} · broad {core.Snapshot.BroadMoney.Amount:0}");
      var projected = world.CoreState.ProjectedAccounts();
      books.AddRow(
        "Core NW",
        $"{projected.AggregateNetWorth.Amount:0.####}",
        $"unpriced qty {projected.AggregateHoldingsUnpricedQuantity:0.#}");
    }
    else
    {
      books.AddRow("Core", "—", "(no Core entities)");
    }

    console.Write(books);

    var waiting = world.Shipments.Count(s => !s.IsLegacy && s.Phase == ShipmentPhase.WaitingBerth);
    var loading = world.Shipments.Count(s => !s.IsLegacy && s.Phase == ShipmentPhase.Loading);
    var logistics = new Table().Border(TableBorder.Simple).Title("Logistics").AddColumns("Metric", "Value");
    logistics.AddRow("Shipments", $"{worldSnap.Ops.Logistics.ShipmentCount}");
    logistics.AddRow("Cargo in flight", $"{worldSnap.Ops.Logistics.CargoQuantityInFlight:0.####}");
    logistics.AddRow("WaitingBerth / Loading", $"{waiting} / {loading}");
    logistics.AddRow("Delivered / Departed", $"{delivered:0} / {credits.Departed}");
    logistics.AddRow("Fuel burned", $"{world.TransportStats.FuelBurned.Value:0.#}");
    logistics.AddRow("Drive wear", $"{world.TransportStats.DriveWearAccumulated:0.#}");
    logistics.AddRow("Plan fails", $"{world.TransportStats.FailedPlans}");
    console.Write(logistics);

    var mesh = ids.Mesh;
    var meshTable = new Table().Border(TableBorder.Simple).Title("Mesh (visibility, not delivery)")
      .AddColumns("Metric", "Value");
    meshTable.AddRow("Nodes / edges", $"{mesh.Nodes.Count} / {mesh.Edges.Length}");
    meshTable.AddRow("Packets / drones / pending",
      $"{mesh.Packets.Count} / {mesh.Drones.Length} / {mesh.Pending.Length}");
    meshTable.AddRow(
      "Publishes D/I/F",
      $"{mesh.Stats.DirectedPublishes} / {mesh.Stats.IdentityPublishes} / {mesh.Stats.FeedPublishes}");
    meshTable.AddRow(
      "Launched / arrived / lost",
      $"{mesh.Stats.DronesLaunched} / {mesh.Stats.DronesArrived} / {mesh.Stats.DronesLost}");
    meshTable.AddRow(
      "Cache / mailbox / feed / emergency",
      $"{mesh.Stats.CacheCredits} / {mesh.Stats.MailboxPushes} / {mesh.Stats.FeedPulls} / {mesh.Stats.EmergencyForced}");
    meshTable.AddRow("Mailboxes / subscriptions",
      $"{mesh.Mailboxes.Count} / {mesh.Subscriptions.Count}");
    meshTable.AddRow("Bandwidth deferred", $"{mesh.Stats.BandwidthDeferred}");
    meshTable.AddRow("Mesh hour", $"{mesh.HourIndex}");
    console.Write(meshTable);

    console.Write(new Rule("FTL profiles & ship registry").RuleStyle("cyan"));
    var profiles = new Table().Border(TableBorder.Simple).Title("Transit (active hulls)").AddColumns("Hull", "Profile", "Wear");
    foreach (var ship in world.Shipments.Where(s => !s.IsLegacy && s.Status == ShipmentStatus.InTransit)
               .OrderBy(s => s.FirmId.Value).Take(8))
    {
      var name = ids.Registry.TryGet(ship.FirmId)?.RegistryName ?? ship.FirmId.Value.ToString("N")[..8];
      profiles.AddRow(Markup.Escape(name), ship.TransitProfile.ToString(), $"{ship.DriveWearAccrued:0.#}");
    }

    if (profiles.Rows.Count == 0)
    {
      profiles.AddRow("—", "(none underway)", "—");
    }

    console.Write(profiles);

    var reg = new Table().Border(TableBorder.Simple).Title("Registry standing")
      .AddColumns("Ship", "Standing", "Rep", "Life%", "OH", "Lien", "P/S/Std");
    foreach (var e in ids.Registry.Entries.OrderBy(x => x.RegistryName))
    {
      reg.AddRow(
        Markup.Escape(e.RegistryName),
        e.StandingLabel,
        $"{ids.Reputation.Get(e.FirmId):0}",
        $"{e.LifeFraction * 100m:0}",
        $"{e.OverhaulCount}",
        $"{e.LienPrincipal:0}",
        $"{e.PriorityLegs}/{e.SlowLegs}/{e.StandardLegs}");
    }

    console.Write(reg);

    if (result.Survival is not null || result.Player?.LastTrampMode == true)
    {
      var snap = result.Survival ?? TrampSurvival.Capture(ids);
      console.Write(new Rule("Last tramp").RuleStyle("green"));
      var lt = new Table().Border(TableBorder.Simple).AddColumns("Field", "Value");
      lt.AddRow("Mode", result.Player?.LastTrampMode == true ? "on" : "off");
      lt.AddRow("Won", result.LastTrampWon ? "yes" : "no");
      lt.AddRow("Lost", result.LastTrampLost ? "yes" : "no");
      lt.AddRow("Operable", $"{snap.OperableCount}/{snap.TotalLightCommercial}");
      lt.AddRow("Calypso", snap.CalypsoOperable ? "OK" : "DOWN");
      lt.AddRow("Names", Markup.Escape(string.Join(", ", snap.OperableNames)));
      console.Write(lt);
    }

    var firmReg = new Table().Border(TableBorder.Simple).Title("Firm registry")
      .AddColumns("Firm", "Standing", "Lien");
    foreach (var f in ids.Desk.Firms.Entries.OrderBy(x => x.RegistryName))
    {
      firmReg.AddRow(Markup.Escape(f.RegistryName), f.StandingLabel, $"{f.LienPrincipal:0}");
    }

    console.Write(firmReg);

    var licReg = new Table().Border(TableBorder.Simple).Title("License registry (sample)")
      .AddColumns("License", "Scope", "Standing");
    foreach (var l in ids.Desk.Licenses.Entries.OrderBy(x => x.RegistryName).Take(8))
    {
      licReg.AddRow(Markup.Escape(l.RegistryName), Markup.Escape(l.Scope), l.StandingLabel);
    }

    console.Write(licReg);

    var tierSample = new Table().Border(TableBorder.Simple).Title("Port tiers (sample)")
      .AddColumns("Hub", "Role", "Tier");
    foreach (var site in ids.Sites.Values
               .OrderBy(s => s.Hub.SystemId, StringComparer.Ordinal)
               .Take(8))
    {
      tierSample.AddRow(
        Markup.Escape(site.Hub.Name),
        site.Hub.Role.ToString(),
        PortTier.Tag(site.Hub.Role));
    }

    console.Write(tierSample);

    console.Write(new Rule("Life moments").RuleStyle("orange1"));
    var lifeHits = LifeMoments.Count(result.Milestones);
    var score = new Table().Border(TableBorder.Simple).Title($"Scorecard ({lifeHits}/{LifeMoments.Kinds.Length})")
      .AddColumns("Moment", "Hits", "Hook");
    foreach (var (kind, hits, hook) in LifeMoments.Score(result.Milestones))
    {
      score.AddRow(kind, $"{hits}", Markup.Escape(hook));
    }

    if (score.Rows.Count == 0)
    {
      score.AddRow("—", "0", "Quiet run — which bill became less dangerous?");
    }

    console.Write(score);

    var milestones = new Table().Border(TableBorder.Simple).Title("Milestones (greppable + vox)")
      .AddColumns("Day", "Kind", "Detail");
    var lifeKinds = new HashSet<string>(LifeMoments.Kinds, StringComparer.OrdinalIgnoreCase)
    {
      "overhaul", "overhaul-forced", "overhaul-due", "burnout", "reinstated", "arrears",
      "escrow", "jump-refuse", "standby-pass", "lien", "berth-fee",
    };
    var forGrep = result.Milestones.Entries
      .Where(m => lifeKinds.Contains(m.Kind))
      .Concat(result.Milestones.Entries.TakeLast(36))
      .Distinct()
      .OrderBy(m => m.Day)
      .ThenBy(m => m.Kind, StringComparer.Ordinal);
    foreach (var m in forGrep)
    {
      milestones.AddRow($"{m.Day}", m.Kind, Markup.Escape(m.Detail));
      var vox = VoxBank.ForMilestone(m.Kind, m.Detail);
      console.MarkupLine(
        $"[grey]MILESTONE:[/] {m.Kind} d{m.Day} {Markup.Escape(m.Detail)}");
      console.MarkupLine($"[dim]{Markup.Escape(VoxBank.Format(vox))}[/]");
    }

    if (milestones.Rows.Count == 0)
    {
      milestones.AddRow("—", "—", "(none)");
    }

    console.Write(milestones);
    var mega = new Table().Border(TableBorder.Simple).Title("Mega-hauler biography (Bulk River)")
      .AddColumns("Day", "Note", "Qty", "Profile");
    foreach (var leg in result.Biographies.ForFirm(ids.MegaHauler).TakeLast(12))
    {
      mega.AddRow($"{leg.Day}", Markup.Escape(leg.Note), $"{leg.Qty:0.#} {leg.Product}", leg.Profile.ToString());
    }

    if (mega.Rows.Count == 0)
    {
      mega.AddRow("—", agents.MegaHauler.LastDecision, "—", "—");
    }

    console.Write(mega);

    var trampBios = ids.Registry.Entries
      .Where(e => e.OwnerMaster)
      .Select(e => (Entry: e, Legs: result.Biographies.ForFirm(e.FirmId).Count()))
      .OrderByDescending(x => x.Legs)
      .Take(2)
      .ToList();
    foreach (var (entry, _) in trampBios)
    {
      var panel = new Table().Border(TableBorder.Simple)
        .Title($"Owner-master biography ({entry.RegistryName})")
        .AddColumns("Day", "Note", "Qty", "Profile");
      foreach (var leg in result.Biographies.ForFirm(entry.FirmId).TakeLast(8))
      {
        panel.AddRow(
          $"{leg.Day}", Markup.Escape(leg.Note), $"{leg.Qty:0.#} {leg.Product}", leg.Profile.ToString());
      }

      if (panel.Rows.Count == 0)
      {
        panel.AddRow("—", "(no legs yet)", "—", "—");
      }

      console.Write(panel);
    }

    var tape = new Table().Border(TableBorder.Simple).Title("Market tape sample").AddColumns("SKU", "Last", "Trades", "Trend");
    foreach (var p in new[] { ids.Ore, ids.Parts, ids.Goods, ids.Fuel })
    {
      if (world.MarketBook.TryGetTape(p, out var snap))
      {
        tape.AddRow(
          CampaignWorld.SkuLabel(p, ids),
          $"{snap.LastPrice.Amount:0.##}",
          $"{snap.TradeCount}",
          world.MarketBook.Trend(p).ToString());
      }
    }

    if (tape.Rows.Count == 0)
    {
      tape.AddRow("—", "—", "0", "Unknown");
    }

    console.Write(tape);

    var activity = new Table().Border(TableBorder.Simple).Title("Activity").AddColumns("Metric", "Value");
    activity.AddRow("Produced", $"{credits.Produced:0}");
    activity.AddRow("Retail sold", $"{credits.RetailSold:0}");
    activity.AddRow("Book fills", $"{credits.BookFills} (qty {credits.BookFillQty:0})");
    activity.AddRow("Loans active / originated", $"{credits.ActiveLoans} / {credits.LoansOriginated}");
    activity.AddRow("Principal outstanding", $"{credits.PrincipalOutstanding:0}");
    activity.AddRow("Capacity upgrades", $"{agents.Capacity.Upgrades}");
    activity.AddRow("Ventures", $"{agents.Ventures.Count}");
    console.Write(activity);

    var agentsTable = new Table().Border(TableBorder.Simple).Title("Agents (last decision)").AddColumns("Agent", "Decision");
    agentsTable.AddRow("Mining", Markup.Escape(agents.Mining.LastDecision));
    agentsTable.AddRow("Industry", Markup.Escape(agents.Industry.LastDecision));
    agentsTable.AddRow("Station", Markup.Escape(agents.Station.LastDecision));
    agentsTable.AddRow("Treasury", Markup.Escape(agents.Treasury.LastDecision));
    agentsTable.AddRow("Capacity", Markup.Escape(agents.Capacity.LastDecision));
    agentsTable.AddRow("Loan repay", Markup.Escape(agents.LoanRepay.LastDecision));
    agentsTable.AddRow("Sol export", Markup.Escape(agents.SolExport.LastDecision));
    agentsTable.AddRow("Mega hauler", Markup.Escape(agents.MegaHauler.LastDecision));
    agentsTable.AddRow("Ventures", Markup.Escape(agents.VenturesAgent.LastDecision));
    for (var i = 0; i < Math.Min(3, agents.Carriers.Count); i++)
    {
      var label = i == 0 ? CampaignWorld.PlayerHullName : $"Tramp{i + 1}";
      var decision = i == 0
        ? agents.CarrierPulse.LastDecision
        : agents.Carriers[i].LastDecision;
      agentsTable.AddRow(label, Markup.Escape(decision));
    }

    console.Write(agentsTable);

    console.MarkupLine(
      $"[grey]Cruise:[/] {AstroEconomyBridge.CruiseDaysPerLy:0.##} d/ly  [grey]Milestones:[/] {result.Milestones.Entries.Count}  [grey]Life:[/] {lifeHits}");
    console.Write(new Rule("Curtain").RuleStyle("orange1"));
    console.MarkupLine(Markup.Escape(
      VoxBank.Format(VoxBank.SessionCurtain(result.Milestones.Entries.Count, lifeHits))));
    console.Write(new Rule("end").RuleStyle("grey"));
  }
}
