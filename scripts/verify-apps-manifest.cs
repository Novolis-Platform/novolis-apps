// Validate apps.json and regenerate per-app solutions via AppsManifest.
//
//   dotnet run --file d:\novolis\novolis-apps\scripts\verify-apps-manifest.cs

#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Diagnostics;
using System.Runtime.CompilerServices;

var scripts = Path.GetDirectoryName(ThisFile())!;
var repoRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetParent(scripts)!.FullName;
var tool = Path.Combine(repoRoot, "tools", "AppsManifest", "AppsManifest.csproj");
if (Run("dotnet", repoRoot, "run", "--project", tool, "--no-launch-profile", "--", "validate", "--repo", repoRoot) != 0)
    return 1;
if (Run("dotnet", repoRoot, "run", "--project", tool, "--no-launch-profile", "--", "generate-solutions", "--repo", repoRoot) != 0)
    return 1;

var ci = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase);
if (ci)
{
    var slnx = Directory.EnumerateFiles(repoRoot, "*.slnx", SearchOption.AllDirectories)
        .Select(p => Path.GetRelativePath(repoRoot, p).Replace('\\', '/'))
        .ToArray();
    var gitArgs = new List<string> { "-C", repoRoot, "status", "--short", "--" };
    gitArgs.AddRange(slnx);
    var (code, stdout, _) = Capture("git", repoRoot, gitArgs.ToArray());
    if (code != 0 || !string.IsNullOrWhiteSpace(stdout.Trim()))
    {
        Console.Error.WriteLine(stdout);
        Console.Error.WriteLine("Generated solution drift detected. Commit the generated .slnx files.");
        return 1;
    }
}

Console.WriteLine("Apps manifest + generated solutions OK.");
return 0;

static string ThisFile([CallerFilePath] string path = "") => path;

static int Run(string file, string cwd, params string[] arguments)
{
    var psi = new ProcessStartInfo(file)
    {
        WorkingDirectory = cwd,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
    };
    foreach (var a in arguments)
        psi.ArgumentList.Add(a);
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {file}");
    p.WaitForExit();
    return p.ExitCode;
}

static (int Code, string Stdout, string Stderr) Capture(string file, string cwd, params string[] arguments)
{
    var psi = new ProcessStartInfo(file)
    {
        WorkingDirectory = cwd,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var a in arguments)
        psi.ArgumentList.Add(a);
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {file}");
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    return (p.ExitCode, stdout, stderr);
}
