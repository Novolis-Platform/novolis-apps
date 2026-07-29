using Avalonia;
using Novolis.Avalonia.Agent;
using SinsOfACapitalismTycoon.Cli;
using SinsOfACapitalismTycoon.Sim;
using SinsOfACapitalismTycoon.Ui;
using SinsOfACapitalismTycoon.Universe;
using Spectre.Console;

namespace SinsOfACapitalismTycoon;

internal static class Program
{
    internal static string ReportText { get; private set; } = "";
    internal static RunOptions? UiOptions { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        CrashGuard.Install("SinsOfACapitalismTycoon");
        try
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            CrashGuard.Report(ex, "Program.Main", openEditor: true, writeMiniDump: true);
            return 1;
        }
    }

    private static async Task<int> MainAsync(string[] args)
    {
        var options = RunOptions.Parse(args);

        if (options.Engine == EngineKind.Campaign)
        {
            if (options.Mode == AppMode.Avalonia)
            {
                // UI-first: window runs the campaign and binds the briefing.
                UiOptions = options;
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                return 0;
            }

            if (options.Mode == AppMode.Captain || options.Playtest)
            {
                return await CaptainConsole.RunAsync(options);
            }

            var result = await CampaignRunner.RunAsync(
                options.Seed, options.DaysHours, options.Quiet, options.Drama, options.Story,
                playerControl: options.Player, autopilot: options.Autopilot, lastTramp: options.LastTramp);
            SpectreHeadlessReport.Write(AnsiConsole.Console, result);
            TryWriteArtifact(result, options.Quiet);
            return 0;
        }

        Action<int, int>? progress = null;
        if (!options.Quiet && options.Mode == AppMode.Headless && options.Periods >= 50)
        {
            var lastPct = -1;
            progress = (done, total) =>
            {
                var pct = done * 100 / total;
                if (pct == lastPct || (pct % 10 != 0 && done != total))
                    return;
                lastPct = pct;
                Console.Error.WriteLine($"… {done}/{total} periods ({pct}%)");
            };
        }

        var report = GameRunner.Run(
            options.Scenario, options.Seed, options.Periods, options.LogEvery, progress);
        ReportText = ReportFormatter.Format(report);

        if (options.Mode == AppMode.Headless)
        {
            Console.Write(ReportText);
            return 0;
        }

        UiOptions = options;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static void TryWriteArtifact(CampaignRunner.Result result, bool quiet)
    {
        if (quiet)
            return;
        try
        {
            var days = result.RequestedHours / 24;
            var dir = Path.Combine(AppContext.BaseDirectory, "artifacts");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"sins-report-{days}d.txt");
            File.WriteAllText(path, CampaignRunner.FormatReport(result));
            Console.Error.WriteLine($"Wrote {path}");
        }
        catch
        {
            // non-fatal
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ => CrashGuard.InstallAvalonia(Avalonia.Threading.Dispatcher.UIThread));
}
