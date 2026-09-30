// Validates Android citizenship rules declared in build/apps.json against source manifests.
//
//   dotnet run --file d:\novolis\novolis-apps\scripts\verify-android-policy.cs

#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;

var scripts = Path.GetDirectoryName(ThisFile())!;
var repoRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetParent(scripts)!.FullName;
using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, "build", "apps.json")));
var errors = new List<string>();
var androidCount = 0;
const string AndroidNs = "http://schemas.android.com/apk/res/android";
const string ToolsNs = "http://schemas.android.com/tools";

foreach (var app in doc.RootElement.GetProperty("apps").EnumerateArray())
{
    if (!app.TryGetProperty("android", out var android))
        continue;
    androidCount++;
    var key = app.GetProperty("key").GetString() ?? "";
    var projectRel = android.GetProperty("project").GetString() ?? "";
    var androidProj = Path.Combine(repoRoot, projectRel.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(androidProj))
    {
        errors.Add($"Missing Android project for {key}: {projectRel}");
        continue;
    }

    var projDir = Path.GetDirectoryName(androidProj)!;
    var xmlPath = new[]
    {
        Path.Combine(projDir, "AndroidManifest.xml"),
        Path.Combine(projDir, "Platforms", "Android", "AndroidManifest.xml"),
    }.FirstOrDefault(File.Exists);
    if (xmlPath is null)
    {
        errors.Add($"No AndroidManifest.xml for {key}");
        continue;
    }

    var xml = XDocument.Load(xmlPath);
    var application = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "application");
    var allowBackup = application?.Attribute(XName.Get("allowBackup", AndroidNs))?.Value;
    if (android.TryGetProperty("allowBackup", out var ab) && ab.ValueKind == JsonValueKind.False && allowBackup == "true")
        errors.Add($"{key}: allowBackup must be false (found true in {xmlPath})");
    var cleartext = application?.Attribute(XName.Get("usesCleartextTraffic", AndroidNs))?.Value;
    if (android.TryGetProperty("usesCleartextTraffic", out var ct) && ct.ValueKind == JsonValueKind.False && cleartext == "true")
        errors.Add($"{key}: usesCleartextTraffic must be false");

    var allowed = android.TryGetProperty("permissionAllowlist", out var allow)
        ? allow.EnumerateArray().Select(x => x.GetString() ?? "").ToHashSet(StringComparer.Ordinal)
        : [];
    foreach (var node in xml.Descendants().Where(e => e.Name.LocalName == "uses-permission"))
    {
        if (node.Attribute(XName.Get("node", ToolsNs))?.Value == "remove")
            continue;
        var name = node.Attribute(XName.Get("name", AndroidNs))?.Value;
        if (!string.IsNullOrWhiteSpace(name) && !allowed.Contains(name))
            errors.Add($"{key}: permission '{name}' is not in allowlist [{string.Join(", ", allowed)}]");
    }

    var csproj = File.ReadAllText(androidProj);
    var applicationId = android.GetProperty("applicationId").GetString() ?? "";
    if (!csproj.Contains(applicationId, StringComparison.Ordinal))
        errors.Add($"{key}: applicationId '{applicationId}' not found in project file");

    var stack = app.TryGetProperty("stack", out var st) ? st.GetString() : null;
    if (stack != "maui")
    {
        var icon = application?.Attribute(XName.Get("icon", AndroidNs))?.Value;
        if (string.IsNullOrWhiteSpace(icon))
            errors.Add($"{key}: AndroidManifest application is missing android:icon");
    }

    if (android.TryGetProperty("versionCodeStrategy", out var vcs)
        && vcs.GetString() == "release-run"
        && Regex.IsMatch(csproj, @"<ApplicationVersion>\s*1\s*</ApplicationVersion>")
        && !csproj.Contains("NovolisAndroidVersionCode", StringComparison.Ordinal))
        errors.Add($"{key}: static ApplicationVersion=1 without NovolisAndroidVersionCode override path");
}

var code = AndroidVersionCode("2026.1.0.42", 42);
if (code != 2026100042)
    errors.Add($"Get-NovolisAndroidVersionCode produced unexpected value: {code} (expected 2026100042)");
var next = AndroidVersionCode("2026.1.1.0", 0);
if (next <= code)
    errors.Add($"Android versionCode is not monotonic across minor releases: {code} -> {next}");

if (errors.Count > 0)
{
    foreach (var e in errors)
        Console.Error.WriteLine(e);
    return 1;
}

Console.WriteLine($"Android policy OK for {androidCount} Android apps.");
return 0;

static int AndroidVersionCode(string packageVersion, int runNumber)
{
    var parts = packageVersion.Split('.');
    var year = int.Parse(parts[0]);
    var major = int.Parse(parts[1]);
    var minor = int.Parse(parts[2]);
    var build = parts.Length >= 4 ? int.Parse(parts[3]) : runNumber;
    if (runNumber > build)
        build = runNumber;
    return (year * 1_000_000) + (major * 100_000) + (minor * 1000) + build;
}

static string ThisFile([CallerFilePath] string path = "") => path;
