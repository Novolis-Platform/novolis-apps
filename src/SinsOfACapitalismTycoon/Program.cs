using Avalonia;
using SinsOfACapitalismTycoon.Cli;
using SinsOfACapitalismTycoon.Sim;
using SinsOfACapitalismTycoon.Ui;

namespace SinsOfACapitalismTycoon;

internal static class Program
{
    internal static string ReportText { get; private set; } = "";

    [STAThread]
    public static int Main(string[] args)
    {
        RunOptions options;
        try
        {
            options = RunOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
