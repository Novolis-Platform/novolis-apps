namespace SinsOfACapitalismTycoon.Cli;

internal enum AppMode
{
    Headless,
    Avalonia
}

internal enum EngineKind
{
    Campaign,
    Core
}

internal sealed record RunOptions(
    AppMode Mode,
    EngineKind Engine,
    Sim.ScenarioKind Scenario,
    int Periods,
    long DaysHours,
    ulong Seed,
    int LogEvery,
    bool Quiet)
{
    public static RunOptions Default { get; } = new(
        AppMode.Headless,
        EngineKind.Campaign,
        Sim.ScenarioKind.LogisticsBind,
        Periods: 100,
        DaysHours: 10L * 24,
        Seed: 1001,
        LogEvery: 0,
        Quiet: false);

    public static RunOptions Parse(string[] args)
    {
        var mode = AppMode.Headless;
        var engine = EngineKind.Campaign;
        var scenario = Sim.ScenarioKind.LogisticsBind;
        var periods = 100;
        var daysHours = Default.DaysHours;
        var seed = Default.Seed;
        var logEvery = 0;
        var quiet = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                mode = ParseMode(args[++i]);
                continue;
            }

            if (a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
            {
                mode = ParseMode(a["--mode=".Length..]);
                continue;
            }

            if (a.Equals("--engine", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                engine = ParseEngine(args[++i]);
                continue;
            }

            if (a.StartsWith("--engine=", StringComparison.OrdinalIgnoreCase))
            {
                engine = ParseEngine(a["--engine=".Length..]);
                continue;
            }

            if (a.Equals("--days", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (!Universe.DurationArg.TryParse(args[++i], out daysHours))
                    throw new ArgumentException("--days must look like 10d or 240h.");
                continue;
            }

            if (a.StartsWith("--days=", StringComparison.OrdinalIgnoreCase))
            {
                if (!Universe.DurationArg.TryParse(a["--days=".Length..], out daysHours))
                    throw new ArgumentException("--days must look like 10d or 240h.");
                continue;
            }

            if (a.Equals("--scenario", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                scenario = Sim.ScenarioKindParser.Parse(args[++i]);
                engine = EngineKind.Core;
                continue;
            }

            if (a.StartsWith("--scenario=", StringComparison.OrdinalIgnoreCase))
            {
                scenario = Sim.ScenarioKindParser.Parse(a["--scenario=".Length..]);
                engine = EngineKind.Core;
                continue;
            }

            if (a.Equals("--periods", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                periods = ParsePositiveInt(args[++i], "periods");
                continue;
            }

            if (a.StartsWith("--periods=", StringComparison.OrdinalIgnoreCase))
            {
                periods = ParsePositiveInt(a["--periods=".Length..], "periods");
                continue;
            }

            if (a.Equals("--seed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                seed = ParseULong(args[++i]);
                continue;
            }

            if (a.StartsWith("--seed=", StringComparison.OrdinalIgnoreCase))
            {
                seed = ParseULong(a["--seed=".Length..]);
                continue;
            }

            if (a.Equals("--log-every", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                logEvery = ParseNonNegativeInt(args[++i], "log-every");
                continue;
            }

            if (a.StartsWith("--log-every=", StringComparison.OrdinalIgnoreCase))
            {
                logEvery = ParseNonNegativeInt(a["--log-every=".Length..], "log-every");
                continue;
            }

            if (a is "-q" or "--quiet")
            {
                quiet = true;
                continue;
            }

            if (a is "-h" or "--help")
            {
                PrintHelp();
                Environment.Exit(0);
            }
        }

        return new RunOptions(mode, engine, scenario, periods, daysHours, seed, logEvery, quiet);
    }

    private static AppMode ParseMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "headless" or "cli" => AppMode.Headless,
            "avalonia" or "ui" or "gui" => AppMode.Avalonia,
            _ => throw new ArgumentException($"Unknown --mode '{value}'. Use headless or avalonia.")
        };

    private static EngineKind ParseEngine(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "campaign" or "universe" or "polity" or "near_sol" or "nearsol" => EngineKind.Campaign,
            "core" or "smoke" or "core-smoke" => EngineKind.Core,
            _ => throw new ArgumentException($"Unknown --engine '{value}'. Use campaign or core.")
        };

    private static int ParsePositiveInt(string value, string name)
    {
        if (!int.TryParse(value, out var n) || n < 1)
            throw new ArgumentException($"--{name} must be a positive integer.");
        return n;
    }

    private static int ParseNonNegativeInt(string value, string name)
    {
        if (!int.TryParse(value, out var n) || n < 0)
            throw new ArgumentException($"--{name} must be a non-negative integer.");
        return n;
    }

    private static ulong ParseULong(string value)
    {
        if (!ulong.TryParse(value, out var n))
            throw new ArgumentException("--seed must be an unsigned integer.");
        return n;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Sins of a Capitalism Tycoon

              --engine campaign|core   Runtime (default: campaign)
              --days Nd                Campaign duration (default: 10d)
              --seed U                 Seed (default: 1001 campaign / use with core too)
              --mode headless|avalonia Output shell (default: headless)
              --quiet / -q             Hide progress

            Core smoke only:
              --engine core --scenario NAME --periods N
              scenarios: baseline|logistics_bind|working_capital|credit_cycle|fiscal_stress|shock

            Docs: docs/README.md
            """);
    }
}
