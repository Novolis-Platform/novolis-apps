using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Novolis.Apps.Manifest;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: AppsManifest <validate|generate-solutions|ci-matrix|release-matrix|list> [options]");
            return 2;
        }

        var command = args[0];
        var repoRoot = GetOption(args, "--repo") ?? FindRepoRoot();
        var manifestPath = GetOption(args, "--manifest") ?? Path.Combine(repoRoot, "build", "apps.json");

        try
        {
            return command switch
            {
                "validate" => Validate(manifestPath, repoRoot),
                "generate-solutions" => GenerateSolutions(manifestPath, repoRoot, write: true),
                "ci-matrix" => EmitCiMatrix(manifestPath, repoRoot, args),
                "release-matrix" => EmitReleaseMatrix(manifestPath, args),
                "list" => ListApps(manifestPath),
                _ => Unknown(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        return 2;
    }

    private static AppsManifestDocument Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest not found: {manifestPath}");

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<AppsManifestDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse apps.json.");
    }

    private static int Validate(string manifestPath, string repoRoot)
    {
        var doc = Load(manifestPath);
        var errors = new List<string>();
        ValidateDocument(doc, repoRoot, errors);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        Console.WriteLine($"OK: {doc.Apps.Count} apps validated ({manifestPath}).");
        return 0;
    }

    private static void ValidateDocument(AppsManifestDocument doc, string repoRoot, List<string> errors)
    {
        if (doc.SchemaVersion < 1)
            errors.Add("schemaVersion must be >= 1.");

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var appIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var applicationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var choices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownChannels = new HashSet<string>(doc.Channels.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var app in doc.Apps)
        {
            if (string.IsNullOrWhiteSpace(app.Key) || !keys.Add(app.Key))
                errors.Add($"Duplicate or missing app key: '{app.Key}'.");
            if (string.IsNullOrWhiteSpace(app.Choice) || !choices.Add(app.Choice))
                errors.Add($"Duplicate or missing choice: '{app.Choice}'.");
            if (app.Ship is null || app.Ship.Count == 0)
                errors.Add($"App '{app.Key}' under src/ must declare at least one ship channel.");

            foreach (var channel in app.Ship ?? [])
            {
                if (!knownChannels.Contains(channel))
                    errors.Add($"App '{app.Key}' ships unknown channel '{channel}'.");
            }

            if (app.Ship?.Contains("windows-inno", StringComparer.OrdinalIgnoreCase) == true)
            {
                if (app.Windows is null)
                    errors.Add($"App '{app.Key}' ships windows-inno but has no windows metadata.");
                else if (string.IsNullOrWhiteSpace(app.Projects.PublishWindows))
                    errors.Add($"App '{app.Key}' ships windows-inno but publishWindows is missing.");
            }

            if (app.Ship?.Contains("android-apk", StringComparer.OrdinalIgnoreCase) == true)
            {
                if (app.Android is null)
                    errors.Add($"App '{app.Key}' ships android-apk but has no android metadata.");
            }

            if (app.Windows is not null)
            {
                if (string.IsNullOrWhiteSpace(app.Windows.AppId) || !appIds.Add(app.Windows.AppId))
                    errors.Add($"Duplicate or missing Inno AppId for '{app.Key}'.");
                if (!app.Windows.InstallDir.StartsWith("Novolis\\", StringComparison.Ordinal)
                    && !app.Windows.InstallDir.StartsWith("Novolis/", StringComparison.Ordinal))
                    errors.Add($"App '{app.Key}' installDir must be under Novolis\\…");
            }

            if (app.Android is not null)
            {
                if (string.IsNullOrWhiteSpace(app.Android.ApplicationId) || !applicationIds.Add(app.Android.ApplicationId))
                    errors.Add($"Duplicate or missing applicationId for '{app.Key}'.");
                if (app.Android.AllowBackup && app.Android.PermissionAllowlist.Count == 0
                    && string.Equals(app.Android.NetworkPolicy, "none", StringComparison.OrdinalIgnoreCase))
                {
                    // Offline/credential-ish apps should default allowBackup=false; warn as error per policy.
                    errors.Add($"App '{app.Key}' must set allowBackup=false unless backup is an explicit product feature.");
                }
            }

            foreach (var project in app.Projects.All)
            {
                var full = Path.Combine(repoRoot, project.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                    errors.Add($"Missing project for '{app.Key}': {project}");
            }

            var solutionFull = Path.Combine(repoRoot, app.Solution.Replace('/', Path.DirectorySeparatorChar));
            // Solutions are generated; only require parent directory.
            var solutionDir = Path.GetDirectoryName(solutionFull);
            if (solutionDir is null || !Directory.Exists(solutionDir))
                errors.Add($"Missing solution directory for '{app.Key}': {app.Solution}");

            foreach (var project in app.Projects.LinuxCi.Concat(app.Projects.Tests).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var full = Path.Combine(repoRoot, project.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                    errors.Add($"Missing Linux CI project for '{app.Key}': {project}");
            }

            // Linux CI must not include Android/MAUI heads.
            foreach (var project in app.Projects.LinuxCi)
            {
                if (project.Contains(".Android/", StringComparison.OrdinalIgnoreCase)
                    || project.EndsWith(".Android.csproj", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"App '{app.Key}' lists Android project in linuxCi: {project}");
                if (app.Stack.Equals("maui", StringComparison.OrdinalIgnoreCase)
                    && project.Equals(app.Projects.Maui, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"App '{app.Key}' must not put MAUI host in linuxCi.");
            }

            if (app.Validation.AndroidCompile && string.IsNullOrWhiteSpace(app.Projects.Android) && string.IsNullOrWhiteSpace(app.Projects.Maui))
                errors.Add($"App '{app.Key}' enables androidCompile without an Android/MAUI project.");

            if (app.Validation.WindowsCi && string.IsNullOrWhiteSpace(app.Projects.PublishWindows))
                errors.Add($"App '{app.Key}' enables windowsCi without a publishWindows project.");

            if (app.Android is not null
                && !string.IsNullOrWhiteSpace(app.Projects.Android)
                && !app.Android.Project.Equals(app.Projects.Android, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"App '{app.Key}' has different Android projects in projects.android and android.project.");
            }
        }

        // Every src/*/ tree must be represented.
        var srcRoot = Path.Combine(repoRoot, "src");
        if (Directory.Exists(srcRoot))
        {
            foreach (var dir in Directory.GetDirectories(srcRoot))
            {
                var name = Path.GetFileName(dir);
                var matched = doc.Apps.Any(a =>
                    a.SourceRoot.Equals($"src/{name}", StringComparison.OrdinalIgnoreCase)
                    || a.SourceRoot.Equals($"src\\{name}", StringComparison.OrdinalIgnoreCase));
                if (!matched)
                    errors.Add($"src/{name} has no apps.json row (every product tree must ship or be removed).");
            }
        }
    }

    private static int GenerateSolutions(string manifestPath, string repoRoot, bool write)
    {
        var doc = Load(manifestPath);
        var errors = new List<string>();
        ValidateDocument(doc, repoRoot, errors);
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        foreach (var app in doc.Apps)
        {
            var path = Path.Combine(repoRoot, app.Solution.Replace('/', Path.DirectorySeparatorChar));
            var solutionDir = Path.GetDirectoryName(path)!;
            var relativeProjects = app.Projects.All
                .Select(p => ToRelativePath(solutionDir, Path.Combine(repoRoot, p.Replace('/', Path.DirectorySeparatorChar))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var slnx = BuildSlnx(relativeProjects, includeSolutionItems: false, solutionItemsRelativeToRepo: false, repoRoot: repoRoot, solutionDir: solutionDir);
            if (write)
            {
                Directory.CreateDirectory(solutionDir);
                File.WriteAllText(path, slnx, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine($"Wrote {app.Solution}");
            }

            var linuxSolution = GetLinuxSolutionPath(app.Solution);
            var linuxPath = Path.Combine(repoRoot, linuxSolution.Replace('/', Path.DirectorySeparatorChar));
            var linuxDir = Path.GetDirectoryName(linuxPath)!;
            var linuxProjects = app.Projects.LinuxCi
                .Concat(app.Projects.Tests)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => ToRelativePath(linuxDir, Path.Combine(repoRoot, p.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();
            var linuxSlnx = BuildSlnx(linuxProjects, includeSolutionItems: false, solutionItemsRelativeToRepo: false, repoRoot: repoRoot, solutionDir: linuxDir);
            if (write)
            {
                Directory.CreateDirectory(linuxDir);
                File.WriteAllText(linuxPath, linuxSlnx, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine($"Wrote {linuxSolution}");
            }
        }

        // Aggregate convenience solution: Linux-safe (no Android/MAUI hosts).
        var aggregateProjects = doc.Apps
            .SelectMany(a => a.Projects.LinuxCi.Concat(a.Projects.Tests))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var aggregate = BuildSlnx(aggregateProjects, includeSolutionItems: true, solutionItemsRelativeToRepo: true, repoRoot: repoRoot, solutionDir: repoRoot);
        var aggregatePath = Path.Combine(repoRoot, "Novolis.Apps.slnx");
        if (write)
        {
            File.WriteAllText(aggregatePath, aggregate, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine("Wrote Novolis.Apps.slnx (Linux-safe aggregate; not the default CI graph)");
        }

        return 0;
    }

    private static string ToRelativePath(string fromDir, string toFile)
    {
        var fromUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(fromDir)));
        var toUri = new Uri(Path.GetFullPath(toFile));
        return Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString()).Replace('/', Path.DirectorySeparatorChar).Replace('\\', '/');
    }

    private static string AppendDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string GetLinuxSolutionPath(string solution)
    {
        var extension = Path.GetExtension(solution);
        return string.IsNullOrEmpty(extension)
            ? $"{solution}.Linux.slnx"
            : $"{solution[..^extension.Length]}.Linux{extension}";
    }

    private static string BuildSlnx(
        IReadOnlyList<string> projects,
        bool includeSolutionItems,
        bool solutionItemsRelativeToRepo,
        string repoRoot,
        string solutionDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Solution>");
        if (includeSolutionItems)
        {
            sb.AppendLine("  <Folder Name=\"/Solution Items/\">");
            foreach (var item in new[] { "Directory.Build.props", "Directory.Packages.props", "nuget.config", "build/apps.json" })
            {
                var rel = solutionItemsRelativeToRepo
                    ? item
                    : ToRelativePath(solutionDir, Path.Combine(repoRoot, item.Replace('/', Path.DirectorySeparatorChar)));
                sb.AppendLine($"    <File Path=\"{rel.Replace('\\', '/')}\" />");
            }
            sb.AppendLine("  </Folder>");
        }

        // Group by first path segment for readability.
        var groups = projects
            .GroupBy(p =>
            {
                var norm = p.Replace('\\', '/');
                if (norm.StartsWith("../../tests/", StringComparison.OrdinalIgnoreCase) || norm.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
                    return "/tests/";
                if (norm.Contains('/'))
                    return $"/{norm.Split('/')[0]}/";
                return "/src/";
            })
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            sb.AppendLine($"  <Folder Name=\"{group.Key}\">");
            foreach (var project in group.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"    <Project Path=\"{project.Replace('\\', '/')}\" />");
            sb.AppendLine("  </Folder>");
        }

        sb.AppendLine("</Solution>");
        return sb.ToString();
    }

    private static int EmitCiMatrix(string manifestPath, string repoRoot, string[] args)
    {
        var doc = Load(manifestPath);
        var changedFilesArg = GetOption(args, "--changed-files");
        var forceAll = HasFlag(args, "--all");
        var coverage = GetOption(args, "--coverage") ?? "fast";
        if (!coverage.Equals("fast", StringComparison.OrdinalIgnoreCase)
            && !coverage.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ci-matrix --coverage must be 'fast' or 'full'.");
        }

        IEnumerable<string> changedFiles = [];
        if (!string.IsNullOrWhiteSpace(changedFilesArg) && File.Exists(changedFilesArg))
            changedFiles = File.ReadAllLines(changedFilesArg).Where(l => !string.IsNullOrWhiteSpace(l));

        var fullCoverage = forceAll || coverage.Equals("full", StringComparison.OrdinalIgnoreCase);
        var selected = SelectAffectedApps(doc, changedFiles.ToList(), forceAll, fullCoverage);
        var linux = selected
            .Where(app => app.Validation.LinuxCi)
            .Select(app => new
            {
                key = app.Key,
                choice = app.Choice,
                solution = GetLinuxSolutionPath(app.Solution),
                stack = app.Stack,
                run_tests = app.Validation.RunTests && app.Projects.Tests.Count > 0,
                test_projects = string.Join(';', app.Projects.Tests),
            })
            .ToList();
        var android = selected
            .Where(app => app.Validation.AndroidCompile)
            .Select(app => new
            {
                key = app.Key,
                choice = app.Choice,
                project = app.Android?.Project ?? app.Projects.Android ?? app.Projects.Maui ?? "",
                stack = app.Stack,
                is_maui = app.Stack.Equals("maui", StringComparison.OrdinalIgnoreCase),
            })
            .ToList();
        var windows = selected
            .Where(app => app.Validation.WindowsCi)
            .Select(app => new
            {
                key = app.Key,
                choice = app.Choice,
                project = app.Projects.PublishWindows ?? "",
                stack = app.Stack,
                install_windows_workload = app.Validation.WindowsWorkload,
            })
            .ToList();
        var rowCount = linux.Count + android.Count + windows.Count;
        var summary = new
        {
            coverage = fullCoverage ? "full" : "fast",
            selected_apps = selected.Count,
            linux_rows = linux.Count,
            android_rows = android.Count,
            windows_rows = windows.Count,
            estimated_checkouts = 1 + rowCount,
            estimated_workload_installs = android.Count + windows.Count(row => row.install_windows_workload),
        };

        var payload = new
        {
            linux,
            android,
            windows,
            summary,
            any = rowCount > 0,
            skip_build = rowCount == 0,
        };
        Console.WriteLine(JsonSerializer.Serialize(payload, CompactJsonOptions));
        return 0;
    }

    private static List<AppEntry> SelectAffectedApps(
        AppsManifestDocument doc,
        List<string> changedFiles,
        bool forceAll,
        bool fullCoverage)
    {
        if (forceAll || changedFiles.Count == 0)
            return doc.Apps.ToList();

        var normalized = changedFiles.Select(f => f.Replace('\\', '/')).ToList();
        var docsOnly = normalized.All(f =>
            f.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || f.Equals("LICENSE", StringComparison.OrdinalIgnoreCase)
            || f.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase));
        if (docsOnly)
            return [];

        var rootPolicy = normalized.Any(f =>
            f.Equals("build/apps.json", StringComparison.OrdinalIgnoreCase)
            || f.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)
            || f.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
            || f.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase)
            || f.Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase)
            || f.Equals("nuget.config", StringComparison.OrdinalIgnoreCase)
            || f.Equals("global.json", StringComparison.OrdinalIgnoreCase)
            || f.StartsWith("build/", StringComparison.OrdinalIgnoreCase)
            || f.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)
            || f.StartsWith("tools/", StringComparison.OrdinalIgnoreCase));

        if (rootPolicy)
        {
            if (fullCoverage)
                return doc.Apps.ToList();

            return SelectRepresentativeApps(doc);
        }

        var selected = new List<AppEntry>();
        foreach (var app in doc.Apps)
        {
            foreach (var glob in app.Validation.ChangedPathGlobs)
            {
                var pattern = "^" + Regex.Escape(glob.Replace('\\', '/')).Replace("\\*\\*", ".*").Replace("\\*", "[^/]*") + "$";
                if (normalized.Any(f => Regex.IsMatch(f, pattern, RegexOptions.IgnoreCase)))
                {
                    selected.Add(app);
                    break;
                }
            }
        }

        return selected;
    }

    private static List<AppEntry> SelectRepresentativeApps(AppsManifestDocument doc)
    {
        var selected = new List<AppEntry>();
        var stacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in doc.Apps)
        {
            if (stacks.Add(app.Stack))
                selected.Add(app);
        }

        return selected;
    }

    private static int EmitReleaseMatrix(string manifestPath, string[] args)
    {
        var doc = Load(manifestPath);
        var appChoice = GetOption(args, "--app") ?? "All";
        var channelOverride = GetOption(args, "--channel");

        IEnumerable<AppEntry> apps = string.Equals(appChoice, "All", StringComparison.OrdinalIgnoreCase)
            ? doc.Apps
            : doc.Apps.Where(a => a.Choice.Equals(appChoice, StringComparison.OrdinalIgnoreCase)
                                  || a.Key.Equals(appChoice, StringComparison.OrdinalIgnoreCase));

        var selectedApps = apps.ToList();
        if (selectedApps.Count == 0)
            throw new InvalidOperationException($"Unknown app selection: {appChoice}");

        var include = new List<object>();
        foreach (var app in selectedApps)
        {
            var channels = app.Ship.ToList();
            if (!string.IsNullOrWhiteSpace(channelOverride))
            {
                if (!channels.Contains(channelOverride, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Channel '{channelOverride}' is not declared in ship for '{app.Key}'.");
                channels = [channelOverride];
            }

            foreach (var channel in channels)
            {
                include.Add(new
                {
                    key = app.Key,
                    choice = app.Choice,
                    channel,
                    stack = app.Stack,
                    publish_windows = app.Projects.PublishWindows,
                    android_project = app.Android?.Project ?? app.Projects.Android ?? app.Projects.Maui,
                    application_id = app.Android?.ApplicationId,
                    artifact_prefix = app.ArtifactPrefix,
                    exe_name = app.Windows?.ExeName,
                    app_id = app.Windows?.AppId,
                    install_dir = app.Windows?.InstallDir,
                    group_name = app.Windows?.GroupName,
                    setup_base = app.Windows?.SetupBase,
                    script_file = app.Windows?.ScriptFile,
                    close_applications_filter = app.Windows?.CloseApplicationsFilter,
                    display_name = app.DisplayName,
                });
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(new { include }, CompactJsonOptions));
        return 0;
    }

    private static int ListApps(string manifestPath)
    {
        var doc = Load(manifestPath);
        foreach (var app in doc.Apps)
            Console.WriteLine($"{app.Choice}\t{app.Key}\tship=[{string.Join(',', app.Ship)}]\tlocal=[{string.Join(',', app.Local)}]");
        return 0;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "build", "apps.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
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
}

internal sealed class AppsManifestDocument
{
    public int SchemaVersion { get; set; }
    public string ManifestVersion { get; set; } = "";
    public Dictionary<string, ChannelDefinition> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<AppEntry> Apps { get; set; } = [];
}

internal sealed class ChannelDefinition
{
    public string Id { get; set; } = "";
    public string Runner { get; set; } = "";
    public List<string> Artifacts { get; set; } = [];
    public string Description { get; set; } = "";
}

internal sealed class AppEntry
{
    public string Key { get; set; } = "";
    public string Choice { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SourceRoot { get; set; } = "";
    public string Solution { get; set; } = "";
    public string ArtifactPrefix { get; set; } = "";
    public string Stack { get; set; } = "";
    public string Status { get; set; } = "public";
    public ProjectSet Projects { get; set; } = new();
    public List<string> Local { get; set; } = [];
    public List<string> Ship { get; set; } = [];
    public ValidationConfig Validation { get; set; } = new();
    public WindowsConfig? Windows { get; set; }
    public AndroidConfig? Android { get; set; }
    public DataConfig? Data { get; set; }
    public ReleaseConfig? Release { get; set; }
}

internal sealed class ProjectSet
{
    public string? PublishWindows { get; set; }
    public List<string> LinuxCi { get; set; } = [];
    public string? Android { get; set; }
    public string? Maui { get; set; }
    public List<string> Tests { get; set; } = [];
    public List<string> All { get; set; } = [];
}

internal sealed class ValidationConfig
{
    public List<string> ChangedPathGlobs { get; set; } = [];
    public bool RunTests { get; set; }
    public bool AndroidCompile { get; set; }
    public bool WindowsCi { get; set; }
    public bool WindowsWorkload { get; set; }
    public bool LinuxCi { get; set; } = true;
    public List<string> FanOutStacks { get; set; } = [];
}

internal sealed class WindowsConfig
{
    public string ExeName { get; set; } = "";
    public string AppId { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string SetupBase { get; set; } = "";
    public string ScriptFile { get; set; } = "";
    public string CloseApplicationsFilter { get; set; } = "";
    public bool FileAssociationsAllowed { get; set; }
}

internal sealed class AndroidConfig
{
    public string Project { get; set; } = "";
    public string ApplicationId { get; set; } = "";
    public string VersionCodeStrategy { get; set; } = "release-run";
    public string SigningSecretKey { get; set; } = "";
    public bool AllowBackup { get; set; }
    public bool UsesCleartextTraffic { get; set; }
    public List<string> PermissionAllowlist { get; set; } = [];
    public string NetworkPolicy { get; set; } = "none";
    public string? SigningContinuityNote { get; set; }
}

internal sealed class DataConfig
{
    public string AppDataRoot { get; set; } = "";
    public string CacheRoot { get; set; } = "";
    public string CredentialsNamespace { get; set; } = "";
    public List<string> NetworkDestinations { get; set; } = [];
    public string Retention { get; set; } = "uninstall-preserves-user-data";
}

internal sealed class ReleaseConfig
{
    public bool GithubRelease { get; set; } = true;
    public bool Checksums { get; set; } = true;
}
