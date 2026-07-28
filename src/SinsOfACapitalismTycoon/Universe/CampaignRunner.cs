using System.Diagnostics;
using System.Text;
using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
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
        decimal OpeningLiquid,
        long RequestedHours,
        TimeSpan Wall);

    public static async Task<Result> RunAsync(ulong seed, long hours, bool quiet, Action<long, long>? progress = null)
    {
        var (sim, ids) = CampaignWorld.Create(seed);
        var agents = SinsAgents.Create(sim, ids);
        var credits = new CreditCirculation(sim);
        credits.SetFirmNames(ids.Firms);
        credits.SetSkuIds(ids.Ore, ids.Parts, ids.Goods, ids.Fuel);
        var openingLiquid = credits.LiquidStock;

        var sw = Stopwatch.StartNew();
        var remaining = hours;
        var hoursPerPulse = 24L;
        var lastPct = -1;
        while (remaining > 0)
        {
            var step = (int)Math.Min(hoursPerPulse, remaining);
            for (var h = 0; h < step; h++)
            {
                var before = sim.State.Events.Count;
                var ctx = new AgentContext(
                    sim,
                    new DeterministicRandom(sim.State.Seed ^ (ulong)sim.State.Clock.HourIndex));
                AgentScheduler.TickAll(agents.PulseOrder.ToArray(), ctx);
                agents.RebuildPulse();
                await sim.AdvanceAsync(SimulationDuration.FromHours(1));
                credits.ObserveAfterPulse(before);
            }

            remaining -= step;
            var done = hours - remaining;
            progress?.Invoke(done, hours);
            if (!quiet)
            {
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
        return new Result(sim, ids, credits, agents, openingLiquid, hours, sw.Elapsed);
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
