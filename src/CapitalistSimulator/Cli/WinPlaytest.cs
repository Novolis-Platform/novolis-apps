using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Cli;

/// <summary>Aggressive autopilot toward scenario win conditions for reachability playtests.</summary>
internal static class WinPlaytest
{
    public static int RunRetailProfit(int seed = 99)
    {
        var world = WorldFactory.Create(ScenarioId.RetailProfit, startingCash: 5_000_000, aiCount: 1, seed: seed);
        StarterBootstrap.EnsureStarterRetail(world);
        var proc = new CommandProcessor(world);

        // Second store ASAP
        TryBuildSecondRetail(world, proc);
        // #region agent log
        DebugSessionLog.Write("A", "WinPlaytest.cs:RetailStart", "retail playtest start", new
        {
            retail = world.FirmsOf(world.Player.Id).Count(f => f.Kind == FirmKind.Retail),
            target = DebugSessionLog.DescribeMoney(world.ScenarioTargetProfit),
            maxDays = world.ScenarioMaxDays,
        });
        // #endregion

        // Run full scenario horizon
        proc.Apply(new AdvanceDaysCommand(world.ScenarioMaxDays + 60));

        // #region agent log
        DebugSessionLog.Write("A", "WinPlaytest.cs:RetailEnd", "retail playtest end", new
        {
            won = world.Win.Won,
            lost = world.Win.Lost,
            message = world.Win.Message,
            day = world.Day,
            lastYearProfitEma = DebugSessionLog.DescribeMoney(world.Player.LastYearProfit),
            trailingYearProfit = DebugSessionLog.DescribeMoney(world.Player.TrailingYearProfit),
            target = DebugSessionLog.DescribeMoney(world.ScenarioTargetProfit),
            monthPnl = DebugSessionLog.DescribeMoney(world.Player.MonthRevenue - world.Player.MonthExpense),
            retail = world.FirmsOf(world.Player.Id).Count(f => f.Kind == FirmKind.Retail),
            cash = DebugSessionLog.DescribeMoney(world.Player.Cash),
        }, runId: "post-fix");
        // #endregion

        Console.WriteLine($"RetailProfit playtest: won={world.Win.Won} lost={world.Win.Lost} msg={world.Win.Message}");
        Console.WriteLine($"LYP={world.Player.LastYearProfit:N0} target={world.ScenarioTargetProfit:N0} day={world.Day}");
        return world.Win.Won ? 0 : 1;
    }

    public static int RunWineDominance(int seed = 77)
    {
        var world = WorldFactory.Create(ScenarioId.WineDominance, startingCash: 8_000_000, aiCount: 0, seed: seed);
        var proc = new CommandProcessor(world);
        var city = world.Cities[0].Name;

        // Farm + factory + retail selling wine
        PlaceFirst(world, proc, city, "farm", "Vineyard");
        PlaceFirst(world, proc, city, "factory_s", "Winery");
        PlaceFirst(world, proc, city, "retail_super", "Wine Shop");

        var farm = world.FirmsOf(world.Player.Id).First(f => f.Kind == FirmKind.Farm);
        var factory = world.FirmsOf(world.Player.Id).First(f => f.Kind == FirmKind.Factory);
        var retail = world.FirmsOf(world.Player.Id).First(f => f.Kind == FirmKind.Retail);

        var extract = farm.Units.First(u => u.Kind == UnitKind.Extract);
        proc.Apply(new ConfigureExtractCommand(farm.Id, extract.Id, ExtractKind.Crop, "grapes", 200));
        var farmSales = farm.Units.FirstOrDefault(u => u.Kind == UnitKind.Sales);
        if (farmSales is not null)
            proc.Apply(new ConfigureSalesCommand(farm.Id, farmSales.Id, "grapes", 6m));

        var mfg = factory.Units.First(u => u.Kind == UnitKind.Manufacturing);
        var buy = factory.Units.First(u => u.Kind == UnitKind.Purchasing);
        var facSales = factory.Units.First(u => u.Kind == UnitKind.Sales);
        proc.Apply(new ConfigurePurchasingCommand(factory.Id, buy.Id, "grapes", 400, false, farm.Id, false));
        proc.Apply(new ConfigureManufacturingCommand(factory.Id, mfg.Id, "wine", 80));
        proc.Apply(new ConfigureSalesCommand(factory.Id, facSales.Id, "wine", 40m));
        proc.Apply(new AutoLinkCommand(factory.Id));

        // Retail buys wine from factory / seaport and sells
        while (retail.Units.Count(u => u.Kind == UnitKind.Purchasing) < 1)
            proc.Apply(new PlaceUnitCommand(retail.Id, UnitKind.Purchasing, 0, 1));
        var rBuy = retail.Units.First(u => u.Kind == UnitKind.Purchasing);
        var rSale = retail.Units.First(u => u.Kind == UnitKind.Sales);
        proc.Apply(new ConfigurePurchasingCommand(retail.Id, rBuy.Id, "wine", 200, false, factory.Id, false));
        proc.Apply(new ConfigureSalesCommand(retail.Id, rSale.Id, "wine", 48m));
        // Seed grapes/wine so chain can start
        world.GetOrCreateLot(farm, "grapes", 0.7).Quantity = 500;
        world.GetOrCreateLot(factory, "grapes", 0.7).Quantity = 200;
        world.GetOrCreateLot(factory, "wine", 0.65).Quantity = 100;
        world.GetOrCreateLot(retail, "wine", 0.65).Quantity = 80;

        // #region agent log
        DebugSessionLog.Write("D", "WinPlaytest.cs:WineStart", "wine playtest start", new
        {
            hasFarm = true,
            hasFactory = factory.Units.Any(u => u.RecipeOutputId == "wine"),
            maxDays = world.ScenarioMaxDays,
        });
        // #endregion

        proc.Apply(new AdvanceDaysCommand(Math.Min(world.ScenarioMaxDays, 365 * 8)));

        var wineSales = world.LastMonthSales.Where(s => s.ProductId == "wine").ToList();
        // #region agent log
        DebugSessionLog.Write("D", "WinPlaytest.cs:WineEnd", "wine playtest end", new
        {
            won = world.Win.Won,
            lost = world.Win.Lost,
            message = world.Win.Message,
            day = world.Day,
            wineTotal = wineSales.Sum(s => s.UnitsSold),
            wineMine = wineSales.Where(s => s.CorpId.Equals(world.Player.Id)).Sum(s => s.UnitsSold),
            farmGrapes = world.FindLot(farm, "grapes")?.Quantity ?? 0,
            factoryWine = world.FindLot(factory, "wine")?.Quantity ?? 0,
            retailWine = world.FindLot(retail, "wine")?.Quantity ?? 0,
        });
        // #endregion

        Console.WriteLine($"WineDominance playtest: won={world.Win.Won} lost={world.Win.Lost} msg={world.Win.Message}");
        Console.WriteLine($"day={world.Day} wineSold={wineSales.Sum(s => s.UnitsSold)}");
        return world.Win.Won ? 0 : 1;
    }

    private static void TryBuildSecondRetail(GameWorld world, CommandProcessor proc)
    {
        var city = world.Cities[0];
        for (var y = 2; y < city.Height - 3; y++)
        for (var x = 1; x < city.Width - 4; x++)
        {
            if (city.Tiles[x, y].FirmId is not null) continue;
            var r = proc.Apply(new BuildFirmCommand(city.Name, "retail_super", x, y, "Second Market"));
            if (!r.Ok) continue;
            var firm = world.FirmsOf(world.Player.Id).Last(f => f.Kind == FirmKind.Retail);
            StarterBootstrap.ConfigureStockedSupermarket(world, proc, firm);
            return;
        }
    }

    private static void PlaceFirst(GameWorld world, CommandProcessor proc, string cityName, string typeId, string name)
    {
        var city = world.FindCityByName(cityName)!;
        for (var y = 1; y < city.Height - 3; y++)
        for (var x = 1; x < city.Width - 4; x++)
        {
            if (city.Tiles[x, y].FirmId is not null) continue;
            var r = proc.Apply(new BuildFirmCommand(cityName, typeId, x, y, name));
            if (r.Ok) return;
        }
    }
}
