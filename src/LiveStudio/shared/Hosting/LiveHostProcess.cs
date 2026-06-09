using System.Diagnostics;

namespace LiveStudio.Shared.Hosting;

public sealed class LiveHostProcess : IAsyncDisposable
{
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public int? ProcessId => _process?.HasExited == false ? _process.Id : null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        if (_process is not null)
        {
            _process.Dispose();
            _process = null;
        }

        var startInfo = CreateStartInfo();

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to launch Novolis.Audio.Live.Host.");

        _ = DrainAsync(_process.StandardOutput, Console.Out, cancellationToken);
        _ = DrainAsync(_process.StandardError, Console.Error, cancellationToken);
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
            throw new InvalidOperationException("The live host has not been started.");

        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return _process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
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
        if (LiveHostPaths.TryResolvePublishedHostExecutable(AppContext.BaseDirectory, out var executablePath, out var useDotNet))
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

        var projectPath = LiveHostPaths.ResolveHostProjectPath();
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
}
