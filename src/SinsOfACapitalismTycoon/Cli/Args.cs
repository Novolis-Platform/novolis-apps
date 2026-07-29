namespace SinsOfACapitalismTycoon.Cli;

internal enum AppMode
{
    Headless,
    Avalonia,
    /// <summary>Interactive / scripted text captain desk (agent- and human-playable).</summary>
    Captain
}

internal enum EngineKind
{
    Campaign,
    Core
}

internal enum JobBoardScope
{
    /// <summary>Live local dock tape for the current system.</summary>
    Dock,
    /// <summary>Mesh digests / retractions (FTL-delayed).</summary>
    Mesh
}

internal sealed record RunOptions(
    AppMode Mode,
    EngineKind Engine,
    Sim.ScenarioKind Scenario,
    int Periods,
    long DaysHours,
    ulong Seed,
    int LogEvery,
    bool Quiet,
    bool Drama,
    bool Story,
    bool Player,
    bool Autopilot,
    bool PlayerBot,
    JobBoardScope Board,
    string? Commands,
    bool Playtest,
    bool LastTramp = false,
    string? LoadSave = null)
{
    public static RunOptions Default { get; } = new(
        AppMode.Headless,
        EngineKind.Campaign,
        Sim.ScenarioKind.LogisticsBind,
        Periods: 100,
        DaysHours: 10L * 24,
        Seed: 1001,
        LogEvery: 0,
        Quiet: false,
        Drama: true,
        Story: false,
        Player: false,
        Autopilot: false,
        PlayerBot: false,
        Board: JobBoardScope.Mesh,
        Commands: null,
        Playtest: false,
        LastTramp: false,
        LoadSave: null);

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
        var drama = true;
        var story = false;
        bool? player = null;
        var autopilot = false;
        var playerBot = false;
        var board = JobBoardScope.Mesh;
        string? commands = null;
        var playtest = false;
        var lastTramp = false;
        string? loadSave = null;

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

            if (a.Equals("--drama", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                drama = ParseDrama(args[++i]);
                continue;
            }

            if (a.StartsWith("--drama=", StringComparison.OrdinalIgnoreCase))
            {
                drama = ParseDrama(a["--drama=".Length..]);
                continue;
            }

            if (a is "--story")
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    story = ParseDrama(args[++i]);
                }
                else
                {
                    story = true;
                }

                continue;
            }

            if (a.StartsWith("--story=", StringComparison.OrdinalIgnoreCase))
            {
                story = ParseDrama(a["--story=".Length..]);
                continue;
            }

            if (a is "-q" or "--quiet")
            {
                quiet = true;
                continue;
            }

            if (a.Equals("--player", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                player = ParseOnOff(args[++i], "--player");
                continue;
            }

            if (a.StartsWith("--player=", StringComparison.OrdinalIgnoreCase))
            {
                player = ParseOnOff(a["--player=".Length..], "--player");
                continue;
            }

            if (a.Equals("--autopilot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                autopilot = ParseOnOff(args[++i], "--autopilot");
                continue;
            }

            if (a.StartsWith("--autopilot=", StringComparison.OrdinalIgnoreCase))
            {
                autopilot = ParseOnOff(a["--autopilot=".Length..], "--autopilot");
                continue;
            }

            if (a.Equals("--player-bot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                playerBot = ParseOnOff(args[++i], "--player-bot");
                continue;
            }

            if (a.StartsWith("--player-bot=", StringComparison.OrdinalIgnoreCase))
            {
                playerBot = ParseOnOff(a["--player-bot=".Length..], "--player-bot");
                continue;
            }

            if (a.Equals("--board", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                board = ParseBoard(args[++i]);
                continue;
            }

            if (a.StartsWith("--board=", StringComparison.OrdinalIgnoreCase))
            {
                board = ParseBoard(a["--board=".Length..]);
                continue;
            }

            if (a.Equals("--commands", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                commands = args[++i];
                continue;
            }

            if (a.StartsWith("--commands=", StringComparison.OrdinalIgnoreCase))
            {
                commands = a["--commands=".Length..];
                continue;
            }

            if (a is "--playtest")
            {
                playtest = true;
                mode = AppMode.Captain;
                if (daysHours == Default.DaysHours)
                {
                    daysHours = 60L * 24;
                }

                continue;
            }

            if (a is "--last-tramp" or "--lasttramp")
            {
                lastTramp = true;
                continue;
            }

            if (a is "--playtest-last-tramp" or "--playtest-lasttramp")
            {
                playtest = true;
                lastTramp = true;
                autopilot = true;
                mode = AppMode.Captain;
                if (daysHours == Default.DaysHours)
                {
                    daysHours = 120L * 24;
                }

                continue;
            }

            if (a.Equals("--load", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                loadSave = args[++i].Trim();
                player ??= true;
                continue;
            }

            if (a.StartsWith("--load=", StringComparison.OrdinalIgnoreCase))
            {
                loadSave = a["--load=".Length..].Trim();
                player ??= true;
                continue;
            }

            if (a is "-h" or "--help")
            {
                PrintHelp();
                Environment.Exit(0);
            }
        }

        // Avalonia / captain imply player unless explicitly disabled.
        var playerOn = player ?? (mode is AppMode.Avalonia or AppMode.Captain);
        if (lastTramp)
        {
            playerOn = true;
        }

        return new RunOptions(
            mode, engine, scenario, periods, daysHours, seed, logEvery, quiet, drama, story,
            playerOn, autopilot, playerBot, board, commands, playtest, lastTramp, loadSave);
    }

    private static bool ParseDrama(string value) =>
        ParseOnOff(value, "--drama");

    private static bool ParseOnOff(string value, string flag) =>
        value.Trim().ToLowerInvariant() switch
        {
            "on" or "true" or "1" or "yes" => true,
            "off" or "false" or "0" or "no" => false,
            _ => throw new ArgumentException($"{flag} must be on or off.")
        };

    private static JobBoardScope ParseBoard(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "dock" or "local" or "berth" => JobBoardScope.Dock,
            "mesh" or "network" or "all" or "global" => JobBoardScope.Mesh,
            _ => throw new ArgumentException("--board must be mesh or dock (aliases: network, berth, local).")
        };

    private static AppMode ParseMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "headless" or "cli" => AppMode.Headless,
            "avalonia" or "ui" or "gui" => AppMode.Avalonia,
            "captain" or "console" or "repl" => AppMode.Captain,
            _ => throw new ArgumentException($"Unknown --mode '{value}'. Use headless, avalonia, or captain.")
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
              --mode headless|avalonia|captain
                                       headless = Spectre report
                                       avalonia = GUI captain desk
                                       captain  = text REPL / scripted play (agent-friendly)
              --player on|off          James / ST Calypso agency (default: on in avalonia/captain)
              --autopilot on|off       AI hauls when player queue empty (default: off)
              --board mesh|dock        Spot intel filter (default: mesh; accept still requires dock)
              --commands "a;b;c"       Captain script (status|jobs|accept N|wait|refuse|…)
              --playtest               60d captain acceptance (travel/haul/dock gate)
              --last-tramp             Last-tramp victory mode (rival pressure; Calypso sole operable)
              --playtest-last-tramp    120d captain autopilot last-tramp acceptance
              --load latest|<guid>     Resume campaign checkpoint (Novolis.Storage.Json)
              --quiet / -q             Hide progress
              --drama on|off           Campaign shocks (default: on)
              --story [on|off]         Live vox tickers + overture (default: off; headless)

            Core smoke only:
              --engine core --scenario NAME --periods N
              scenarios: baseline|logistics_bind|working_capital|credit_cycle|fiscal_stress|shock

            Docs: docs/README.md
            """);
    }
}
