namespace SinsOfACapitalismTycoon.Cli;

internal enum AppMode
{
    Headless,
    Avalonia
}

internal sealed record RunOptions(
    AppMode Mode,
    Sim.ScenarioKind Scenario,
    int Periods,
    ulong Seed,
    int LogEvery,
    bool Quiet)
{
    public static RunOptions Default { get; } = new(
        AppMode.Headless,
        Sim.ScenarioKind.LogisticsBind,
        Periods: 100,
        Seed: 42,
        LogEvery: 0,
        Quiet: false);

    public static RunOptions Parse(string[] args)
    {
        var mode = AppMode.Headless;
        var scenario = Default.Scenario;
        var periods = Default.Periods;
        var seed = Default.Seed;
        var logEvery = Default.LogEvery;
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

            if (a.Equals("--scenario", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                scenario = Sim.ScenarioKindParser.Parse(args[++i]);
                continue;
            }

            if (a.StartsWith("--scenario=", StringComparison.OrdinalIgnoreCase))
            {
                scenario = Sim.ScenarioKindParser.Parse(a["--scenario=".Length..]);
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

        return new RunOptions(mode, scenario, periods, seed, logEvery, quiet);
    }

    private static AppMode ParseMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "headless" or "cli" => AppMode.Headless,
            "avalonia" or "ui" or "gui" => AppMode.Avalonia,
            _ => throw new ArgumentException(
                $"Unknown --mode '{value}'. Use headless or avalonia.")
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

              --mode headless|avalonia   Run shell (default: headless)
              --scenario NAME            Scenario (default: logistics_bind)
                                         baseline|logistics_bind|working_capital|
                                         credit_cycle|fiscal_stress|shock
              --periods N                Periods to advance (default: 100)
              --seed U                   Deterministic seed (default: 42)
              --log-every N              Sample period log every N (0 = auto)
              --quiet / -q               Suppress stderr progress on long runs
              --help                     Show this help

            Examples:
              --mode headless --scenario logistics_bind --periods 300 --seed 42
              --scenario working_capital --periods 200
              --scenario baseline --periods 100 --quiet
              --mode avalonia --scenario shock --periods 300
            """);
    }
}
