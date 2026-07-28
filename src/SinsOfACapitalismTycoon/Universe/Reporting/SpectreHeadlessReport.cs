using Novolis.Economy;
using Novolis.Economy.Core.Extensions;
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
  public static void Write(IAnsiConsole console, CampaignRunner.Result result) =>
    Write(
      console,
      result.Sim,
      result.Ids,
      result.Credits,
      result.OpeningLiquid,
      result.RequestedHours,
      result.Wall,
      result.Agents);

  public static void Write(
    IAnsiConsole console,
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    CreditCirculation credits,
    decimal openingLiquid,
    long requestedHours,
    TimeSpan wall,
    SinsAgents.Bundle agents)
  {
    var world = sim.State.World;
    var hh = world.Cohorts.Sum(c => c.BudgetRemaining.Amount);
    var delivered = world.TransportStats.CargoDelivered.Value;
    var day = sim.State.Clock.Date.DayIndex;
    var liquidDelta = credits.LiquidStock - openingLiquid;
    var worldSnap = world.ToReportSnapshot();

    console.Write(new FigletText("Sins").Color(Color.Orange1));
    console.MarkupLine("[bold]Sins of a Capitalism Tycoon[/] — campaign report");
    console.Write(new Rule($"day {day} · {DurationArg.Format(requestedHours)} · hash {sim.State.Hash:X16}").RuleStyle("grey"));

    var header = new Table().Border(TableBorder.Rounded).AddColumns("Field", "Value");
    header.AddRow("Wall clock", $"{wall.TotalSeconds:0.#}s");
    header.AddRow("Hubs / corridors", $"{ids.Bridge.Hubs.Count} / {world.Corridors.Count}");
    header.AddRow("Roles", ids.RoleSummary);
    console.Write(header);

    var money = new Table().Border(TableBorder.Simple).Title("Ops money (FirmLedgers)").AddColumns("Metric", "Amount");
    money.AddRow("Ops liquid", $"{credits.LiquidStock:0}");
    money.AddRow("Opening / Δ", $"{openingLiquid:0} / {liquidDelta:0}");
    money.AddRow("Household budgets", $"{hh:0}");
    money.AddRow("Inv book $", $"{credits.InventoryBookValue:0}");
    money.AddRow("Wages → hh", $"{credits.WagesDistributed:0}");
    money.AddRow("Imports / Exports", $"{credits.ImportSpend:0} / {credits.ExportRevenue:0}");
    money.AddRow("Tolls → treasury", $"{credits.TollsToTreasury:0.##}");
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

    var logistics = new Table().Border(TableBorder.Simple).Title("Logistics").AddColumns("Metric", "Value");
    logistics.AddRow("Shipments", $"{worldSnap.Ops.Logistics.ShipmentCount}");
    logistics.AddRow("Cargo in flight", $"{worldSnap.Ops.Logistics.CargoQuantityInFlight:0.####}");
    logistics.AddRow("Corridor toll exposure", $"{worldSnap.Ops.Logistics.CorridorTollExposure.Amount:0.####}");
    logistics.AddRow("Delivered / Departed", $"{delivered:0} / {credits.Departed}");
    logistics.AddRow("Fuel burned", $"{world.TransportStats.FuelBurned.Value:0.#}");
    logistics.AddRow("Plan fails", $"{world.TransportStats.FailedPlans}");
    console.Write(logistics);

    var activity = new Table().Border(TableBorder.Simple).Title("Activity").AddColumns("Metric", "Value");
    activity.AddRow("Produced", $"{credits.Produced:0}");
    activity.AddRow("Retail sold", $"{credits.RetailSold:0}");
    activity.AddRow("Book fills", $"{credits.BookFills} (qty {credits.BookFillQty:0})");
    activity.AddRow("Loans active / originated", $"{credits.ActiveLoans} / {credits.LoansOriginated}");
    activity.AddRow("Principal outstanding", $"{credits.PrincipalOutstanding:0}");
    console.Write(activity);

    var agentsTable = new Table().Border(TableBorder.Simple).Title("Agents (last decision)").AddColumns("Agent", "Decision");
    agentsTable.AddRow("Mining", Markup.Escape(agents.Mining.LastDecision));
    agentsTable.AddRow("Industry", Markup.Escape(agents.Industry.LastDecision));
    agentsTable.AddRow("Station", Markup.Escape(agents.Station.LastDecision));
    agentsTable.AddRow("Treasury", Markup.Escape(agents.Treasury.LastDecision));
    agentsTable.AddRow("Sol export", Markup.Escape(agents.SolExport.LastDecision));
    for (var i = 0; i < Math.Min(3, agents.Carriers.Count); i++)
    {
      var label = i == 0 ? "Carrier" : $"Tramp{i + 1}";
      agentsTable.AddRow(label, Markup.Escape(agents.Carriers[i].LastDecision));
    }

    console.Write(agentsTable);

    console.MarkupLine($"[grey]Ventures launched:[/] {agents.Ventures.Count}  [grey]Cruise:[/] {AstroEconomyBridge.CruiseDaysPerLy:0.##} d/ly");
    console.Write(new Rule("end").RuleStyle("grey"));
  }
}
