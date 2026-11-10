namespace SinsOfACapitalismTycoon.Cli;

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
    bool NeuralAutopilot,
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
        NeuralAutopilot: false,
        PlayerBot: false,
        Board: JobBoardScope.Mesh,
        Commands: null,
        Playtest: false,
        LastTramp: false,
        LoadSave: null);

    public static RunOptions Parse(string[] args)
    {
        var p = new Parser();
        p.Consume(args);
        return p.ToOptions();
    }

    private sealed class Parser
    {
        private AppMode _mode = AppMode.Headless;
        private EngineKind _engine = EngineKind.Campaign;
        private Sim.ScenarioKind _scenario = Sim.ScenarioKind.LogisticsBind;
        private int _periods = 100;
        private long _daysHours = Default.DaysHours;
        private ulong _seed = Default.Seed;
        private int _logEvery;
        private bool _quiet;
        private bool _drama = true;
        private bool _story;
        private bool? _player;
        private bool _autopilot;
        private bool _neuralAutopilot;
        private bool _playerBot;
        private JobBoardScope _board = JobBoardScope.Mesh;
        private string? _commands;
        private bool _playtest;
        private bool _lastTramp;
        private string? _loadSave;

        public void Consume(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (TryApplyValue(args, ref i))
                    continue;

                if (TryApplySwitch(args, ref i))
                    continue;

                var a = args[i];
                if (a is "-h" or "--help")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }
            }
        }

        public RunOptions ToOptions()
        {
            var playerOn = _player ?? (_mode is AppMode.Avalonia or AppMode.Captain);
            if (_lastTramp)
                playerOn = true;

            return new RunOptions(
                _mode, _engine, _scenario, _periods, _daysHours, _seed, _logEvery, _quiet, _drama, _story,
                playerOn, _autopilot, _neuralAutopilot, _playerBot, _board, _commands, _playtest, _lastTramp,
                _loadSave);
        }

        private bool TryApplyValue(string[] args, ref int i)
        {
            if (TryRead(args, ref i, "mode", out var value))
            {
                _mode = ParseMode(value);
                return true;
            }

            if (TryRead(args, ref i, "engine", out value))
            {
                _engine = ParseEngine(value);
                return true;
            }

            if (TryRead(args, ref i, "days", out value))
            {
                if (!Universe.DurationArg.TryParse(value, out _daysHours))
                    throw new ArgumentException("--days must look like 10d or 240h.");
                return true;
            }

            if (TryRead(args, ref i, "scenario", out value))
            {
                _scenario = Sim.ScenarioKindParser.Parse(value);
                _engine = EngineKind.Core;
                return true;
            }

            if (TryRead(args, ref i, "periods", out value))
            {
                _periods = ParsePositiveInt(value, "periods");
                return true;
            }

            if (TryRead(args, ref i, "seed", out value))
            {
                _seed = ParseULong(value);
                return true;
            }

            if (TryRead(args, ref i, "log-every", out value))
            {
                _logEvery = ParseNonNegativeInt(value, "log-every");
                return true;
            }

            if (TryRead(args, ref i, "drama", out value))
            {
                _drama = ParseOnOff(value, "--drama");
                return true;
            }

            if (TryRead(args, ref i, "player", out value))
            {
                _player = ParseOnOff(value, "--player");
                return true;
            }

            if (TryRead(args, ref i, "autopilot", out value))
            {
                _autopilot = ParseOnOff(value, "--autopilot");
                return true;
            }

            if (TryRead(args, ref i, "neural-autopilot", out value))
            {
                _neuralAutopilot = ParseOnOff(value, "--neural-autopilot");
                if (_neuralAutopilot)
                    _autopilot = true;
                return true;
            }

            if (TryRead(args, ref i, "player-bot", out value))
            {
                _playerBot = ParseOnOff(value, "--player-bot");
                return true;
            }

            if (TryRead(args, ref i, "board", out value))
            {
                _board = ParseBoard(value);
                return true;
            }

            if (TryRead(args, ref i, "commands", out value))
            {
                _commands = value;
                return true;
            }

            if (TryRead(args, ref i, "load", out value))
            {
                _loadSave = value.Trim();
                _player ??= true;
                return true;
            }

            return false;
        }

        private bool TryApplySwitch(string[] args, ref int i)
        {
            var a = args[i];

            if (a is "--story")
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    _story = ParseOnOff(args[++i], "--story");
                else
                    _story = true;
                return true;
            }

            if (a.StartsWith("--story=", StringComparison.OrdinalIgnoreCase))
            {
                _story = ParseOnOff(a["--story=".Length..], "--story");
                return true;
            }

            if (a is "-q" or "--quiet")
            {
                _quiet = true;
                return true;
            }

            if (a.Equals("--neural-autopilot", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--neural-autopilot=on", StringComparison.OrdinalIgnoreCase))
            {
                _neuralAutopilot = true;
                _autopilot = true;
                return true;
            }

            if (a is "--playtest")
            {
                ApplyPlaytest(days: 60L * 24);
                return true;
            }

            if (a is "--last-tramp" or "--lasttramp")
            {
                _lastTramp = true;
                return true;
            }

            if (a is "--playtest-last-tramp" or "--playtest-lasttramp")
            {
                _lastTramp = true;
                _autopilot = true;
                ApplyPlaytest(days: 120L * 24);
                return true;
            }

            return false;
        }

        private void ApplyPlaytest(long days)
        {
            _playtest = true;
            _mode = AppMode.Captain;
            if (_daysHours == Default.DaysHours)
                _daysHours = days;
        }
    }

    /// <summary>Accepts <c>--name value</c> or <c>--name=value</c>.</summary>
    private static bool TryRead(string[] args, ref int i, string name, out string value)
    {
        var a = args[i];
        var equals = "--" + name + "=";
        if (a.StartsWith(equals, StringComparison.OrdinalIgnoreCase))
        {
            value = a[equals.Length..];
            return true;
        }

        if (a.Equals("--" + name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            value = args[++i];
            return true;
        }

        value = "";
        return false;
    }

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
                                       avalonia = GUI captain bridge
                                       captain  = text REPL / scripted play (agent-friendly)
              --player on|off          James / ST Calypso agency (default: on in avalonia/captain)
              --autopilot on|off       AI hauls when player queue empty (default: off)
              --neural-autopilot on|off  Dense-net captain (implies autopilot; Novolis.MachineLearning.Neural)
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
