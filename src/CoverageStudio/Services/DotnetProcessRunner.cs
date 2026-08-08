using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CoverageStudio.Services;

internal readonly record struct DotnetProcessResult(int ExitCode, string Output);

/// <summary>Hidden <c>dotnet</c> child processes — no console window, redirected IO only.</summary>
internal static class DotnetProcessRunner
{
    private static readonly Regex TotalRe = new(@"total:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SucceededRe = new(@"succeeded:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FailedRe = new(@"failed:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<DotnetProcessResult> RunAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        await using var reg = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best-effort cancel
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }

            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout))
            sb.Append(stdout);
        if (!string.IsNullOrEmpty(stderr))
            sb.Append(stderr);
        return new DotnetProcessResult(process.ExitCode, sb.ToString());
    }

    public static (int Total, int Passed, int Failed) ParseTestCounts(string output)
    {
        var total = MatchInt(TotalRe, output);
        var passed = MatchInt(SucceededRe, output);
        var failed = MatchInt(FailedRe, output);
        return (total, passed, failed);
    }

    private static int MatchInt(Regex regex, string output)
    {
        var m = regex.Match(output);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }
}
