using System.Diagnostics;
using LiveStudio.Shared.Hosting;
using LiveStudio.Shared.Launcher;

namespace LiveStudio.Launcher;

internal static class Program
{
    private const int FatalExitCode = 17;

    public static async Task<int> Main(string[] args)
    {
        var hostOnly = args.Contains("--host-only", StringComparer.OrdinalIgnoreCase);
        using var shutdown = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdown.Cancel();
        };

        await using var pipeServer = new LiveLauncherPipeServer();
        await using var watchdog = new LiveHostWatchdog();
        Process? studioProcess = null;

        pipeServer.Start();
        watchdog.StatusChanged += pipeServer.UpdateStatus;
        pipeServer.UpdateStatus(watchdog.CurrentStatus);

        var watchdogTask = watchdog.RunAsync(shutdown.Token);

        if (!hostOnly)
            studioProcess = TryStartStudio();

        try
        {
            await watchdogTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            watchdog.PublishShutdown("Launcher is shutting down.");
            pipeServer.UpdateStatus(watchdog.CurrentStatus);
            return 0;
        }

        if (watchdog.CurrentStatus.IsFatal)
        {
            var message = watchdog.CurrentStatus.Message;
            Console.Error.WriteLine();
            Console.Error.WriteLine("Novolis Audio Live launcher stopped:");
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Console.Error.WriteLine("All studio processes depending on the host should exit now.");

            if (studioProcess is { HasExited: false })
            {
                try
                {
                    studioProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }

            return FatalExitCode;
        }

        return 0;
    }

    private static Process? TryStartStudio()
    {
        try
        {
            var startInfo = CreateStudioStartInfo();
            return Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to start Live Studio UI: {ex.Message}");
            Console.Error.WriteLine("The host will continue running. Start the studio manually when ready.");
            return null;
        }
    }

    private static ProcessStartInfo CreateStudioStartInfo()
    {
        if (LiveHostPaths.TryResolvePublishedStudioExecutable(AppContext.BaseDirectory, out var executablePath, out var useDotNet))
        {
            var publishedStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            };

            if (useDotNet)
            {
                publishedStartInfo.FileName = "dotnet";
                publishedStartInfo.ArgumentList.Add(executablePath);
            }
            else
            {
                publishedStartInfo.FileName = executablePath;
            }

            return publishedStartInfo;
        }

        var projectPath = LiveHostPaths.ResolveStudioProjectPath();
        var workingDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Unable to resolve working directory for {projectPath}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");
        return startInfo;
    }
}
