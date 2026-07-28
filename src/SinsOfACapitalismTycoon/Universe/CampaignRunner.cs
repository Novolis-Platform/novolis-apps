using System.Diagnostics;
using System.Text;
using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Spectre.Console;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Runs the 100-system campaign for a requested duration.</summary>
internal static class CampaignRunner
{
  public sealed record Result(
    EconomySimulation Sim,
    CampaignWorld.Ids Ids,
    CreditCirculation Credits,
    SinsAgents.Bundle Agents,
    MilestoneLog Milestones,
    ShipBiographyLog Biographies,
    decimal OpeningLiquid,
    long RequestedHours,
    TimeSpan Wall,
    bool DramaEnabled);

  public static async Task<Result> RunAsync(
    ulong seed,
    long hours,
    bool quiet,
    bool drama = true,
    bool story = false,
    Action<long, long>? progress = null)
  {
    ClaimsPulse.ResetSeen();
    var milestones = new MilestoneLog();
    var biographies = new ShipBiographyLog();
    var (sim, ids) = CampaignWorld.Create(seed);
    ids.Reputation = new ReputationLedger();
    ids.Escrow = new EscrowBook();
    var agents = SinsAgents.Create(sim, ids, milestones, biographies);
    var opportunities = new OpportunitiesPool(ids, milestones, ids.Reputation);
    var dramaHost = new CampaignDramaHost(ids, milestones, opportunities, ids.Reputation, drama);
    var credits = new CreditCirculation(sim);
    credits.SetFirmNames(ids.Firms);
    credits.SetSkuIds(ids.Ore, ids.Parts, ids.Goods, ids.Fuel);
    var openingLiquid = credits.LiquidStock;

    if (!quiet && story)
    {
      foreach (var line in VoxBank.SessionOverture(seed, hours, drama))
      {
        Console.Error.WriteLine(VoxBank.Format(line));
      }
    }

    var sw = Stopwatch.StartNew();
    var remaining = hours;
    var hoursPerPulse = 24L;
    var lastPct = -1;
    while (remaining > 0)
    {
      var step = (int)Math.Min(hoursPerPulse, remaining);
      InsurancePulse.TickMorningReinstate(sim, ids.Registry, milestones);
      for (var h = 0; h < step; h++)
      {
        var before = sim.State.Events.Count;
        dramaHost.TickHour(sim);
        var ctx = new AgentContext(
          sim,
          new DeterministicRandom(sim.State.Seed ^ (ulong)sim.State.Clock.HourIndex));
        AgentScheduler.TickAll(agents.PulseOrder.ToArray(), ctx);
        agents.RebuildPulse();
        await sim.AdvanceAsync(SimulationDuration.FromHours(1));
        credits.ObserveAfterPulse(before);
        ObserveDeliveries(sim, ids, biographies, milestones);
      }

      remaining -= step;
      var done = hours - remaining;
      ClaimsPulse.TickDay(sim, ids.Registry, ids, milestones, biographies);
      DriveMaintenancePulse.TickDay(sim, ids.Registry, milestones);
      ids.Escrow.TickDay(sim, ids, milestones);
      JumpBandGate.TickBerthFees(sim, ids, milestones);
      LienPulse.TickDay(sim, ids, milestones);
      InsurancePulse.TickDay(sim, ids.Registry, milestones, credits);
      ids.Reputation.TickDay(sim.State.Clock.Date.DayIndex);
      dramaHost.TickDayEnd(sim);
      ObserveFinalStockout(sim, ids, milestones);
      progress?.Invoke(done, hours);
      if (!quiet)
      {
        EmitLiveTickers(milestones, story);
        var pct = (int)(done * 100 / hours);
        if (pct != lastPct && pct % 10 == 0)
        {
          lastPct = pct;
          Console.Error.WriteLine($"… {DurationArg.Format(done)} / {DurationArg.Format(hours)} ({pct}%)");
        }
      }
    }

    sw.Stop();
    credits.CaptureFinalMilestone();
    return new Result(sim, ids, credits, agents, milestones, biographies, openingLiquid, hours, sw.Elapsed, drama);
  }

  private static void EmitLiveTickers(MilestoneLog milestones, bool story)
  {
    foreach (var m in milestones.DrainNew())
    {
      var line = VoxBank.ForMilestone(m.Kind, m.Detail);
      if (story)
      {
        Console.Error.WriteLine($"d{m.Day} {VoxBank.Format(line)}");
      }
      else
      {
        Console.Error.WriteLine($"MILESTONE: {m.Kind} d{m.Day} {m.Detail}");
      }
    }
  }

  private static void ObserveDeliveries(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    ShipBiographyLog bios,
    MilestoneLog milestones)
  {
    var day = sim.State.Clock.Date.DayIndex;
    foreach (var ev in sim.State.Events.TakeLast(40))
    {
      if (ev is not ShipmentDelivered delivered)
      {
        continue;
      }

      var origin = "?";
      var dest = "?";
      bios.Record(
        day,
        delivered.FirmId,
        origin,
        dest,
        CampaignWorld.SkuLabel(delivered.ProductId, ids),
        delivered.Quantity.Value,
        TransitProfile.StandardCommercial,
        0m,
        "delivered");
      if (delivered.ProductId.Equals(ids.Goods))
      {
        ids.Reputation.ObserveFinalDelivery(delivered.FirmId);
      }

      if (delivered.FirmId.Equals(ids.MegaHauler))
      {
        milestones.AddOnce(day, "mega",
          $"Bulk River delivered {delivered.Quantity.Value:0.#} {CampaignWorld.SkuLabel(delivered.ProductId, ids)}");
      }
    }
  }

  private static void ObserveFinalStockout(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    var day = sim.State.Clock.Date.DayIndex;
    decimal goods = 0m;
    foreach (var site in ids.Sites.Values.Where(s =>
               s.Hub.Role is SystemRole.Capital or SystemRole.Inhabited))
    {
      goods += sim.State.World.Inventory.GetQuantity(
        new InventoryKey(ids.Station, site.Hub.LocationId, ids.Goods)).Value;
    }

    if (goods < 8m)
    {
      milestones.AddOnce(day, "stockout", $"Final shelves thin ({goods:0.#})");
    }
  }

  public static string FormatReport(Result result)
  {
    var sb = new StringBuilder();
    var writer = new StringWriter(sb);
    var console = AnsiConsole.Create(new AnsiConsoleSettings
    {
      Out = new AnsiConsoleOutput(writer),
      Ansi = AnsiSupport.Detect,
      Interactive = InteractionSupport.No
    });
    SpectreHeadlessReport.Write(console, result);
    return sb.ToString();
  }
}
