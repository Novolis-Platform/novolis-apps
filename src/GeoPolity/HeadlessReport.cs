using System.Diagnostics;
using GeoPolity.Agent;
using GeoPolity.Session;
using Novolis.Agent.Surface;
using Novolis.Geopolitics.Core;

namespace GeoPolity;

internal static class HeadlessReport
{
    public static async Task RunAsync(GeoSession session, int years, bool attachAgent)
    {
        AgentSurface? surface = null;
        if (attachAgent)
        {
            surface = AgentSurface.AttachAll(
                new GeoPolityAgentHost(session),
                GeoPolitySessionContract.Definition,
                new AgentAttachOptions
                {
                    EnableIpc = true,
                    EnableHttp = true,
                    EnableTcp = true,
                    EnableRpc = false,
                    HttpPort = 18857,
                    TcpPort = 18858,
                });
            if (surface?.HttpBaseUrl is { } url)
            {
                Console.Error.WriteLine($"GeoPolity Agent Surface: {url}");
            }
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var totalDays = years * WorldState.DaysPerYear;
            var remaining = totalDays;
            var lastPct = -1;
            var stepDays = WorldState.DaysPerMonth;
            while (remaining > 0)
            {
                var step = Math.Min(stepDays, remaining);
                session.AdvanceDays(step);
                remaining -= step;
                var done = totalDays - remaining;
                var pct = (int)(done * 100.0 / totalDays);
                if (pct != lastPct && pct % 10 == 0)
                {
                    lastPct = pct;
                    Console.Error.WriteLine($"… year {session.World.Year} ({pct}%)");
                }
            }

            sw.Stop();
            Write(session, years, sw.Elapsed);
        }
        finally
        {
            if (surface is not null)
            {
                await surface.DisposeAsync();
            }
        }
    }

    public static void Write(GeoSession session, int years, TimeSpan elapsed)
    {
        var world = session.World;
        var sim = session.Simulation;
        var churn = session.OwnershipChurn();
        var insolvent = world.Polities.Count(p => p.Treasury < 0);
        var activeWars = world.ActiveWars.Count();
        var treaties = world.ActiveTreaties().Count();

        Console.WriteLine("=== GeoPolity headless report ===");
        Console.WriteLine($"Years: {years}  Elapsed: {elapsed.TotalSeconds:0.00}s");
        Console.WriteLine($"Polities: {world.Polities.Count}  Provinces: {world.Provinces.Count}");
        Console.WriteLine($"Day: {world.Day} (Y{world.Year})");
        Console.WriteLine($"Wars started/ended: {sim.Stats.WarsStarted}/{sim.Stats.WarsEnded}  active: {activeWars}");
        Console.WriteLine($"Provinces captured: {sim.Stats.ProvincesCaptured}  ownership churn: {churn}");
        Console.WriteLine($"Treaties signed: {sim.Stats.TreatiesSigned}  active: {treaties}");
        Console.WriteLine($"Orgs: {world.ActiveOrgs.Count()} (joins {sim.Stats.OrgJoins}/leaves {sim.Stats.OrgLeaves})");
        Console.WriteLine($"Common markets: {world.CountActiveTreatiesOfKind(TreatyKind.CommonMarket)}  volume {sim.Stats.CommonMarketVolume:0}");
        Console.WriteLine($"World market volume: {sim.Stats.WorldMarketVolume:0}");
        Console.WriteLine($"EP GDP boost (cum): {sim.Stats.EconomicPartnershipGdpBoost:0}");
        Console.WriteLine($"Embargoes: {world.CountActiveTreatiesOfKind(TreatyKind.EconomicEmbargo)}  aid transferred: {sim.Stats.EconomicAidTransferred:0}");
        Console.WriteLine($"Resource shortage ticks: {sim.Stats.ResourceShortageEvents}");
        Console.WriteLine($"Budget crises: {sim.Stats.BudgetCrises}  insolvent now: {insolvent}");
        Console.WriteLine($"Mean legitimacy/approval: {sim.Stats.MeanLegitimacy:0.00}/{sim.Stats.MeanApproval:0.00}");
        Console.WriteLine($"Tech advances: {sim.Stats.TechAdvances}");
        Console.WriteLine("Supranationals:");
        foreach (var o in world.ActiveOrgs)
        {
            Console.WriteLine($"  {o.Name}: members={o.MemberIds.Count} treaties={o.LinkedTreatyIds.Count}");
        }

        Console.WriteLine("Top 5 power:");
        foreach (var p in world.Polities.OrderByDescending(x => x.PowerScore).Take(5))
        {
            Console.WriteLine(
                $"  {p.Name}: power={p.PowerScore:0} gdp={p.Gdp:0} mil={p.Military.Total:0} tech={p.TechLevel:0.0} " +
                $"gov={p.Government} L={p.Civic.Legitimacy:0.00} A={p.Civic.Approval:0.00}");
        }
    }
}
