using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Apps.Manifest;

internal static class AppPublishCommand
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    internal static int Publish(string manifestPath, string repoRoot, string[] args)
    {
        var appKey = GetRequiredOption(args, "--app");
        var channel = GetRequiredOption(args, "--channel");
        var packageVersion = GetRequiredOption(args, "--version");
        var assemblyVersion = GetRequiredOption(args, "--assembly-version");
        var fileVersion = GetRequiredOption(args, "--file-version");
        var skipInstaller = HasFlag(args, "--skip-installer");

        var doc = Program.Load(manifestPath);
        var app = doc.Apps.FirstOrDefault(a => a.Key.Equals(appKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown app key: {appKey}");

        PublishResult result = channel.ToLowerInvariant() switch
        {
            "windows-inno" => PublishWindowsInno(repoRoot, app, packageVersion, assemblyVersion, fileVersion, skipInstaller),
            "linux-tar" => PublishLinuxTar(repoRoot, app, packageVersion, assemblyVersion, fileVersion),
            _ => throw new ArgumentException("publish --channel must be windows-inno or linux-tar."),
        };

        Console.WriteLine(JsonSerializer.Serialize(result, ResultJsonOptions));
        return 0;
    }

    private static PublishResult PublishWindowsInno(
        string repoRoot,
        AppEntry app,
        string packageVersion,
        string assemblyVersion,
        string fileVersion,
        bool skipInstaller)
    {
        if (app.Windows is null || string.IsNullOrWhiteSpace(app.Projects.PublishWindows))
            throw new InvalidOperationException($"App '{app.Key}' is not configured for windows-inno.");

        var projectRelative = app.Projects.PublishWindows;
        var appProject = Path.Combine(repoRoot, projectRelative.Replace('/', Path.DirectorySeparatorChar));
        var stagingDir = Path.Combine(repoRoot, "artifacts", app.Key);
        var publishDir = Path.Combine(stagingDir, "app");
        var installerDir = Path.Combine(stagingDir, "installer");
        Directory.CreateDirectory(publishDir);
        Directory.CreateDirectory(installerDir);

        var versionArgs = BuildVersionMsBuildArgs(packageVersion, assemblyVersion, fileVersion);
        var dotnetConfigArgs = BuildNuGetConfigArgs(repoRoot);

        Console.Error.WriteLine($"Publishing {app.Key} {packageVersion} (win-x64)...");
        DotnetRestore(appProject, "win-x64", repoRoot, dotnetConfigArgs, versionArgs);

        if (app.Key.Equals("live-studio", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var extra in new[]
                     {
                         "src/LiveStudio/host/LiveStudio.Host.csproj",
                         "src/LiveStudio/launcher/LiveStudio.Launcher.csproj",
                     })
            {
                DotnetRestore(Path.Combine(repoRoot, extra.Replace('/', Path.DirectorySeparatorChar)), "win-x64", repoRoot, dotnetConfigArgs, versionArgs);
            }
        }

        DotnetPublish(appProject, "win-x64", publishDir, repoRoot, versionArgs);

        foreach (var extraProjectRelativePath in app.Windows.AdditionalProjects)
        {
            if (string.IsNullOrWhiteSpace(extraProjectRelativePath))
                continue;
            var extraProject = Path.Combine(repoRoot, extraProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Console.Error.WriteLine($"Publishing {app.Key} host component {extraProjectRelativePath}...");
            DotnetRestore(extraProject, "win-x64", repoRoot, dotnetConfigArgs, versionArgs);
            DotnetPublish(extraProject, "win-x64", publishDir, repoRoot, versionArgs);
        }

        var exeBase = Path.GetFileNameWithoutExtension(appProject);
        string zipStem;
        if (File.Exists(Path.Combine(publishDir, app.Windows.ExeName)))
            zipStem = Path.GetFileNameWithoutExtension(app.Windows.ExeName);
        else
            zipStem = exeBase;

        var zipName = $"{zipStem}-{packageVersion}-win-x64.zip";
        var zipPath = Path.Combine(stagingDir, zipName);
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(publishDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        Console.Error.WriteLine($"Portable zip: {zipPath}");

        var result = new PublishResult { ZipPath = zipPath };

        if (skipInstaller)
            return result;

        var inno = BuildInnoProfile(repoRoot, app, packageVersion, publishDir, installerDir);
        var msbuildArgs = new List<string> { "msbuild", appProject, "-t:NovolisGenerateInnoScript" };
        msbuildArgs.AddRange(FormatMsBuildProperties(inno.MsBuildArgs));
        ProcessRunner.Run("dotnet", repoRoot, msbuildArgs);

        if (app.Key.Equals("reach", StringComparison.OrdinalIgnoreCase))
            AddReachInstallerEntries(inno.ScriptPath);

        var iscc = FindIscc();
        if (iscc is null)
        {
            Console.Error.WriteLine($"ISCC.exe not found. Inno script written to {inno.ScriptPath} — install Inno Setup 6 to compile the installer.");
            return result;
        }

        ProcessRunner.Run(iscc, repoRoot, [inno.ScriptPath]);
        if (!File.Exists(inno.InstallerPath))
            throw new InvalidOperationException($"Expected installer not found: {inno.InstallerPath}");

        result.InstallerPath = inno.InstallerPath;
        Console.Error.WriteLine($"Installer: {inno.InstallerPath}");
        return result;
    }

    private static PublishResult PublishLinuxTar(
        string repoRoot,
        AppEntry app,
        string packageVersion,
        string assemblyVersion,
        string fileVersion)
    {
        if (app.Linux is null || string.IsNullOrWhiteSpace(app.Linux.Project))
            throw new InvalidOperationException($"App '{app.Key}' is not configured for linux-tar.");

        var projectRelative = app.Linux.Project;
        var project = Path.Combine(repoRoot, projectRelative.Replace('/', Path.DirectorySeparatorChar));
        var stagingDir = Path.Combine(repoRoot, "artifacts", app.Key, "linux");
        var publishDir = Path.Combine(stagingDir, "app");
        Directory.CreateDirectory(publishDir);

        var versionArgs = BuildVersionMsBuildArgs(packageVersion, assemblyVersion, fileVersion);
        var dotnetConfigArgs = BuildNuGetConfigArgs(repoRoot);

        Console.Error.WriteLine($"Publishing {app.Key} {packageVersion} (linux-x64)...");
        DotnetRestore(project, "linux-x64", repoRoot, dotnetConfigArgs, versionArgs);
        DotnetPublish(project, "linux-x64", publishDir, repoRoot, versionArgs);

        var executable = Path.Combine(publishDir, app.Linux.ExeName);
        if (!File.Exists(executable))
            throw new InvalidOperationException($"Expected Linux executable not found: {executable}");

        var tarName = $"{app.ArtifactPrefix}-{packageVersion}-linux-x64.tar.gz";
        var tarPath = Path.Combine(stagingDir, tarName);
        if (File.Exists(tarPath))
            File.Delete(tarPath);

        ProcessRunner.Run("tar", repoRoot, ["-C", publishDir, "-czf", tarPath, "."]);
        Console.Error.WriteLine($"Linux tarball: {tarPath}");

        return new PublishResult { TarPath = tarPath };
    }

    private static void DotnetRestore(
        string projectPath,
        string runtime,
        string repoRoot,
        IReadOnlyList<string> configArgs,
        IReadOnlyList<string> versionArgs)
    {
        var arguments = new List<string> { "restore", projectPath, "-r", runtime };
        arguments.AddRange(configArgs);
        arguments.AddRange(versionArgs);
        ProcessRunner.Run("dotnet", repoRoot, arguments);
    }

    private static void DotnetPublish(
        string projectPath,
        string runtime,
        string outputDir,
        string repoRoot,
        IReadOnlyList<string> versionArgs)
    {
        var arguments = new List<string>
        {
            "publish", projectPath,
            "-c", "Release",
            "-r", runtime,
            "--self-contained", "true",
            "--no-restore",
            "-o", outputDir,
        };
        arguments.AddRange(versionArgs);
        ProcessRunner.Run("dotnet", repoRoot, arguments);
    }

    private static List<string> BuildVersionMsBuildArgs(string packageVersion, string assemblyVersion, string fileVersion) =>
    [
        $"-p:PackageVersion={packageVersion}",
        $"-p:AssemblyVersion={assemblyVersion}",
        $"-p:FileVersion={fileVersion}",
        $"-p:InformationalVersion={packageVersion}",
    ];

    private static List<string> BuildNuGetConfigArgs(string repoRoot)
    {
        var nugetConfig = Path.Combine(repoRoot, "nuget.config");
        return File.Exists(nugetConfig) ? ["--configfile", nugetConfig] : [];
    }

    private static InnoProfile BuildInnoProfile(
        string repoRoot,
        AppEntry app,
        string packageVersion,
        string publishDir,
        string installerDir)
    {
        var windows = app.Windows!;
        var script = Path.Combine(installerDir, windows.ScriptFile);
        var setupBase = $"{windows.SetupBase}-{packageVersion}-win-x64";
        var license = Path.Combine(repoRoot, "LICENSE");
        var icon = Path.Combine(repoRoot, "icon.ico");

        var msbuild = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NovolisInnoAppName"] = app.DisplayName,
            ["NovolisInnoAppVersion"] = packageVersion,
            ["NovolisInnoPublishDir"] = publishDir,
            ["NovolisInnoAppExeName"] = windows.ExeName,
            ["NovolisInnoOutputDir"] = installerDir,
            ["NovolisInnoAppId"] = windows.AppId,
            ["NovolisInnoDefaultGroupName"] = windows.GroupName,
            ["NovolisInnoOutputBaseFilename"] = setupBase,
            ["NovolisInnoInstallDirName"] = windows.InstallDir,
            ["NovolisInnoScriptPath"] = script,
            ["NovolisInnoAppPublisher"] = "Novolis",
            ["NovolisInnoAppPublisherURL"] = "https://github.com/Novolis-Platform",
            ["NovolisInnoAppCopyright"] = "Copyright (c) Novolis",
            ["NovolisInnoVersionInfoCompany"] = "Novolis",
            ["NovolisInnoVersionInfoDescription"] = $"{app.DisplayName} - Novolis",
            ["NovolisInnoAppSupportURL"] = "https://github.com/Novolis-Platform/novolis-apps/issues",
            ["NovolisInnoAppUpdatesURL"] = "https://github.com/Novolis-Platform/novolis-apps/releases",
            ["NovolisInnoCloseApplicationsFilter"] = windows.CloseApplicationsFilter,
        };
        if (File.Exists(license))
            msbuild["NovolisInnoLicenseFile"] = license;
        if (File.Exists(icon))
            msbuild["NovolisInnoSetupIconFile"] = icon;

        return new InnoProfile
        {
            ScriptPath = script,
            InstallerPath = Path.Combine(installerDir, $"{setupBase}.exe"),
            MsBuildArgs = msbuild,
        };
    }

    private static IEnumerable<string> FormatMsBuildProperties(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var entry in properties)
        {
            var value = entry.Value;
            if (value.Contains(';') || value.Contains('"') || value.Contains(' '))
                yield return $"-p:{entry.Key}=\"{value.Replace("\"", "\\\"")}\"";
            else
                yield return $"-p:{entry.Key}={value}";
        }
    }

    private static void AddReachInstallerEntries(string scriptPath)
    {
        var contents = File.ReadAllText(scriptPath);
        var newLine = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        const string serviceStartup =
            "Name: \"{userstartup}\\Novolis Reach Service\"; Filename: \"{app}\\Novolis.Reach.Host.Windows.Service.exe\"; WorkingDir: \"{app}\"";
        const string serviceRun =
            "Filename: \"{app}\\Novolis.Reach.Host.Windows.Service.exe\"; Description: \"Start Novolis Reach Service\"; Flags: nowait runhidden skipifsilent";

        if (!contents.Contains($"[Icons]{newLine}", StringComparison.Ordinal))
            throw new InvalidOperationException($"Reach installer script is missing the [Icons] section: {scriptPath}");
        if (!contents.Contains($"[Run]{newLine}", StringComparison.Ordinal))
            throw new InvalidOperationException($"Reach installer script is missing the [Run] section: {scriptPath}");

        contents = contents.Replace(
            $"[Icons]{newLine}",
            $"[Icons]{newLine}{serviceStartup}{newLine}",
            StringComparison.Ordinal);
        contents = contents.Replace(
            $"[Run]{newLine}",
            $"[Run]{newLine}{serviceRun}{newLine}",
            StringComparison.Ordinal);
        File.WriteAllText(scriptPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string? FindIscc()
    {
        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        foreach (var root in new[] { programFilesX86, programFiles })
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;
            var candidate = Path.Combine(root, "Inno Setup 6", "ISCC.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(segment.Trim(), "ISCC.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string GetRequiredOption(string[] args, string name)
    {
        var value = GetOption(args, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required option {name}.");
        return value;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    private sealed class InnoProfile
    {
        public required string ScriptPath { get; init; }
        public required string InstallerPath { get; init; }
        public required Dictionary<string, string> MsBuildArgs { get; init; }
    }

    private sealed class PublishResult
    {
        [JsonPropertyName("zipPath")]
        public string? ZipPath { get; set; }

        [JsonPropertyName("installerPath")]
        public string? InstallerPath { get; set; }

        [JsonPropertyName("tarPath")]
        public string? TarPath { get; set; }
    }
}
