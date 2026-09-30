using System.Diagnostics;
using System.Text;

namespace Novolis.Apps.Manifest;

internal static class ProcessRunner
{
    internal static void Run(string fileName, string workingDirectory, IReadOnlyList<string> arguments, bool echoToConsole = true)
    {
        var exitCode = RunCapture(fileName, workingDirectory, arguments, echoToConsole, out _, out _);
        if (exitCode != 0)
            throw new InvalidOperationException($"{fileName} failed with exit code {exitCode}.");
    }

    internal static int RunCapture(
        string fileName,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        bool echoToConsole,
        out string stdout,
        out string stderr)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            stdoutBuilder.AppendLine(e.Data);
            if (echoToConsole)
                Console.Error.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            stderrBuilder.AppendLine(e.Data);
            if (echoToConsole)
                Console.Error.WriteLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        stdout = stdoutBuilder.ToString();
        stderr = stderrBuilder.ToString();
        return process.ExitCode;
    }
}
