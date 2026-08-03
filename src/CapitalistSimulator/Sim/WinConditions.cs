namespace CapitalistSimulator.Sim;

internal static class WinConditions
{
    public static void Evaluate(GameWorld world)
    {
        if (world.Win.Won || world.Win.Lost) return;

        if (world.Player.Cash < -50_000)
        {
            // #region agent log
            DebugSessionLog.Write("E", "WinConditions.cs:Bankrupt", "loss bankrupt", new
            {
                cash = DebugSessionLog.DescribeMoney(world.Player.Cash),
                day = world.Day,
                scenario = world.Scenario.ToString(),
            });
            // #endregion
            world.Win.Lost = true;
            world.Win.Message = "Bankrupt — cash deeply negative.";
            world.Paused = true;
            return;
        }

        if (world.Day > world.ScenarioMaxDays)
        {
            // #region agent log
            DebugSessionLog.Write("C", "WinConditions.cs:TimeExpired", "loss time expired", new
            {
                day = world.Day,
                maxDays = world.ScenarioMaxDays,
                lastYearProfit = DebugSessionLog.DescribeMoney(world.Player.LastYearProfit),
                target = DebugSessionLog.DescribeMoney(world.ScenarioTargetProfit),
                retailCount = world.FirmsOf(world.Player.Id).Count(f => f.Kind == FirmKind.Retail),
                scenario = world.Scenario.ToString(),
            });
            // #endregion
            world.Win.Lost = true;
            world.Win.Message = "Time expired.";
            world.Paused = true;
            return;
        }

        switch (world.Scenario)
        {
            case ScenarioId.Sandbox:
                // #region agent log
                if (world.Day == 2 || world.Day % 360 == 1)
                {
                    DebugSessionLog.Write("B", "WinConditions.cs:Sandbox", "sandbox has no win branch", new
                    {
                        day = world.Day,
                        scenario = "Sandbox",
                    });
                }
                // #endregion
                break;
            case ScenarioId.RetailProfit:
            {
                var retailCount = world.FirmsOf(world.Player.Id).Count(f => f.Kind == FirmKind.Retail);
                var monthly = world.Player.MonthRevenue - world.Player.MonthExpense;
                var trailing = world.Player.TrailingYearProfit;
                var gap = world.ScenarioTargetProfit - trailing;
                // #region agent log
                if (world.Day % 180 == 1 || (retailCount >= 2 && world.Day % 30 == 1))
                {
                    DebugSessionLog.Write("A", "WinConditions.cs:RetailProfit", "retail profit check", new
                    {
                        day = world.Day,
                        lastYearProfitEma = DebugSessionLog.DescribeMoney(world.Player.LastYearProfit),
                        trailingYearProfit = DebugSessionLog.DescribeMoney(trailing),
                        target = DebugSessionLog.DescribeMoney(world.ScenarioTargetProfit),
                        gap = DebugSessionLog.DescribeMoney(gap),
                        monthPnl = DebugSessionLog.DescribeMoney(monthly),
                        retailCount,
                        wouldWin = trailing >= world.ScenarioTargetProfit && retailCount >= 2,
                        runId = "post-fix",
                    });
                }
                // #endregion
                if (trailing >= world.ScenarioTargetProfit && retailCount >= 2)
                {
                    Win(world, $"Trailing-year profit ${trailing:N0} with retail chain.");
                }
                break;
            }
            case ScenarioId.WineDominance:
            {
                var wineSales = world.LastMonthSales
                    .Where(s => string.Equals(s.ProductId, "wine", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var total = wineSales.Sum(s => s.UnitsSold);
                var mine = wineSales.Where(s => s.CorpId.Equals(world.Player.Id)).Sum(s => s.UnitsSold);
                var hasFarm = world.FirmsOf(world.Player.Id).Any(f => f.Kind == FirmKind.Farm);
                var hasFactory = world.FirmsOf(world.Player.Id).Any(f =>
                    f.Kind == FirmKind.Factory && f.Units.Any(u => u.RecipeOutputId == "wine"));
                var share = total > 0 ? mine / total : 0m;
                // #region agent log
                if (world.Day % 180 == 1 || hasFarm || hasFactory)
                {
                    DebugSessionLog.Write("D", "WinConditions.cs:WineDominance", "wine check", new
                    {
                        day = world.Day,
                        hasFarm,
                        hasFactory,
                        wineTotal = DebugSessionLog.DescribeMoney(total),
                        wineMine = DebugSessionLog.DescribeMoney(mine),
                        share = DebugSessionLog.DescribeMoney(share),
                        wouldWin = hasFarm && hasFactory && total > 0 && share >= 0.55m,
                    });
                }
                // #endregion
                if (hasFarm && hasFactory && total > 0 && mine / total >= 0.55m)
                    Win(world, "Dominating wine market with vertical integration.");
                break;
            }
        }
    }

    private static void Win(GameWorld world, string message)
    {
        // #region agent log
        DebugSessionLog.Write("WIN", "WinConditions.cs:Win", "victory", new
        {
            day = world.Day,
            scenario = world.Scenario.ToString(),
            message,
            lastYearProfit = DebugSessionLog.DescribeMoney(world.Player.LastYearProfit),
            cash = DebugSessionLog.DescribeMoney(world.Player.Cash),
        });
        // #endregion
        world.Win.Won = true;
        world.Win.Message = message;
        world.Paused = true;
        world.AddNews($"Victory: {message}");
    }
}
