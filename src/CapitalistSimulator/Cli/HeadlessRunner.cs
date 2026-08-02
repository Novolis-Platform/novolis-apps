using CapitalistSimulator.Persistence;
using CapitalistSimulator.Sim;
using Spectre.Console;

namespace CapitalistSimulator.Cli;

internal static class HeadlessRunner
{
    public static int Run(RunOptions options)
    {
        var store = new SaveStore();
        GameWorld world;
        if (options.LoadName is { } loadName)
        {
            world = store.Load(loadName) ?? throw new InvalidOperationException($"Save not found: {loadName}");
        }
        else
        {
            world = WorldFactory.Create(
                options.Scenario,
                options.StartingCash,
                options.AiCount,
                options.AiAggressiveness,
                options.Seed);
        }

        // Seed a playable retail loop for smoke if player has no firms
        if (!world.FirmsOf(world.Player.Id).Any())
            BootstrapRetail(world);

        var proc = new CommandProcessor(world);
        proc.Apply(new AdvanceDaysCommand(options.Days));

        if (options.SaveName is { } saveName)
            store.Save(world, saveName);

        if (!options.Quiet)
            WriteReport(world);

        return world.Win.Lost ? 1 : 0;
    }

    internal static void BootstrapRetail(GameWorld world)
    {
        var proc = new CommandProcessor(world);
        var city = world.Cities[0].Name;
        proc.Apply(new BuildFirmCommand(city, "retail_super", 1, 2, "Flagship Market"));
        var firm = world.FirmsOf(world.Player.Id).First();
        proc.Apply(new PlaceUnitCommand(firm.Id, UnitKind.Purchasing, 1, 1));
        var buys = firm.Units.Where(u => u.Kind == UnitKind.Purchasing).ToList();
        var sales = firm.Units.Where(u => u.Kind == UnitKind.Sales).ToList();
        proc.Apply(new ConfigurePurchasingCommand(firm.Id, buys[0].Id, "bread", 200, true, null, false));
        if (buys.Count > 1)
            proc.Apply(new ConfigurePurchasingCommand(firm.Id, buys[1].Id, "milk", 150, true, null, false));
        if (sales.Count > 0)
            proc.Apply(new ConfigureSalesCommand(firm.Id, sales[0].Id, "bread", 3.5m));
        if (sales.Count > 1)
            proc.Apply(new ConfigureSalesCommand(firm.Id, sales[1].Id, "milk", 4.5m));
        var ad = firm.Units.FirstOrDefault(u => u.Kind == UnitKind.Advertising);
        if (ad is not null)
            proc.Apply(new ConfigureAdvertisingCommand(firm.Id, ad.Id, "bread", "Food", 3000));
        proc.Apply(new AutoLinkCommand(firm.Id));
    }

    private static void WriteReport(GameWorld world)
    {
        var console = AnsiConsole.Console;
        console.MarkupLine("[bold]Capitalist Simulator[/] — headless report");
        console.WriteLine($"Day {world.Day}  Year {world.Year}  Scenario {world.Scenario}");
        console.WriteLine($"Cash ${world.Player.Cash:N0}  Share ${world.Player.SharePrice:N2}  NetWorth ${world.PlayerNetWorth():N0}");
        console.WriteLine($"Month Rev ${world.Player.MonthRevenue:N0}  Exp ${world.Player.MonthExpense:N0}  LastYearProfit~ ${world.Player.LastYearProfit:N0}");
        console.WriteLine($"Firms {world.FirmsOf(world.Player.Id).Count()}  Loans {world.Player.Loans.Sum(l => l.Principal):N0}");
        if (world.Win.Won) console.MarkupLine($"[green]WIN[/] {world.Win.Message}");
        if (world.Win.Lost) console.MarkupLine($"[red]LOSE[/] {world.Win.Message}");

        var table = new Table().AddColumns("Product", "Sold", "Revenue");
        foreach (var g in world.LastMonthSales.Where(s => s.CorpId.Equals(world.Player.Id))
                     .GroupBy(s => s.ProductId)
                     .OrderByDescending(g => g.Sum(x => x.Revenue))
                     .Take(12))
        {
            table.AddRow(g.Key, g.Sum(x => x.UnitsSold).ToString("N0"), g.Sum(x => x.Revenue).ToString("N0"));
        }
        console.Write(table);

        console.MarkupLine("[dim]Recent news[/]");
        foreach (var n in world.News.TakeLast(8))
            console.WriteLine($"  d{n.Day}: {n.Text}");
    }
}
