using System.Diagnostics;

namespace LiveStudio;

internal sealed class LiveHostProcess : IAsyncDisposable
{
    private Process? _process;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
            return;

        var startInfo = CreateStartInfo();

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to launch Novolis.Audio.Live.Host.");

        _ = DrainAsync(_process.StandardOutput, Console.Out, cancellationToken);
        _ = DrainAsync(_process.StandardError, Console.Error, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(1500))
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                else
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
        }

        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        if (TryResolvePublishedHostExecutable(out var executablePath, out var useDotNet))
        {
            var publishedStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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

        var projectPath = ResolveHostProjectPath();
        var workingDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Unable to resolve working directory for {projectPath}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Debug");
        startInfo.ArgumentList.Add("--no-launch-profile");
        return startInfo;
    }

    private static async Task DrainAsync(TextReader reader, TextWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                    break;

                await writer.WriteLineAsync($"[live-host] {line}").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string ResolveHostProjectPath()
    {
        var appsRoot = FindAncestorDirectory("novolis-apps");
        var projectPath = Path.Combine(appsRoot, "src", "LiveStudio", "host", "LiveStudio.Host.csproj");

        if (!File.Exists(projectPath))
            throw new FileNotFoundException("Unable to locate the Novolis Audio live host project.", projectPath);

        return projectPath;
    }

    private static bool TryResolvePublishedHostExecutable(out string executablePath, out bool useDotNet)
    {
        var overridePath = Environment.GetEnvironmentVariable("NOVOLIS_AUDIO_LIVE_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var candidate = Path.GetFullPath(overridePath);
            if (File.Exists(candidate))
            {
                executablePath = candidate;
                useDotNet = Path.GetExtension(candidate).Equals(".dll", StringComparison.OrdinalIgnoreCase);
                return true;
            }
        }

        var publishRoot = AppContext.BaseDirectory;
        var hostName = OperatingSystem.IsWindows() ? "Novolis.Audio.Live.Host.exe" : "Novolis.Audio.Live.Host";
        var candidateHost = Path.Combine(publishRoot, "host", hostName);
        if (File.Exists(candidateHost))
        {
            executablePath = candidateHost;
            useDotNet = false;
            return true;
        }

        var candidateDll = Path.Combine(publishRoot, "host", "Novolis.Audio.Live.Host.dll");
        if (File.Exists(candidateDll))
        {
            executablePath = candidateDll;
            useDotNet = true;
            return true;
        }

        executablePath = string.Empty;
        useDotNet = false;
        return false;
    }

    private static string FindAncestorDirectory(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (string.Equals(current.Name, name, StringComparison.OrdinalIgnoreCase))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Unable to locate the '{name}' repository root from {AppContext.BaseDirectory}.");
    }
}
