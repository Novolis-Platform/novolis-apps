// Static installer citizenship checks against Inno generator + apps.json.
//
//   dotnet run --file d:\novolis\novolis-apps\scripts\verify-installer-policy.cs

#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false

using System.Runtime.CompilerServices;
using System.Text.Json;

var scripts = Path.GetDirectoryName(ThisFile())!;
var repoRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetParent(scripts)!.FullName;
var errors = new List<string>();
var generator = Path.Combine(Directory.GetParent(repoRoot)!.FullName, "novolis-avalonia", "src", "Novolis.Avalonia.Packaging.Inno", "InnoScriptGenerator.cs");
if (File.Exists(generator))
{
    var src = File.ReadAllText(generator);
    foreach (var needle in new[]
    {
        "PrivilegesRequired=lowest",
        "UsePreviousAppDir=yes",
        "CloseApplications=yes",
        @"{localappdata}}\Programs\",
        "Flags: unchecked",
    })
    {
        if (!src.Contains(needle, StringComparison.Ordinal))
            errors.Add($"InnoScriptGenerator missing required fragment: {needle}");
    }
}
else
{
    Console.WriteLine($"Sibling InnoScriptGenerator not found at {generator} — skipping source contract check.");
}

using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, "build", "apps.json")));
var appIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var app in doc.RootElement.GetProperty("apps").EnumerateArray())
{
    var key = app.GetProperty("key").GetString() ?? "";
    var ships = app.TryGetProperty("ship", out var ship)
        ? ship.EnumerateArray().Select(x => x.GetString()).ToArray()
        : [];
    if (!ships.Contains("windows-inno"))
        continue;
    if (!app.TryGetProperty("windows", out var windows))
    {
        errors.Add($"{key}: windows-inno without windows metadata");
        continue;
    }

    var installDir = windows.TryGetProperty("installDir", out var idir) ? idir.GetString() ?? "" : "";
    if (!installDir.StartsWith("Novolis\\", StringComparison.Ordinal) && !installDir.StartsWith(@"Novolis\", StringComparison.Ordinal))
        errors.Add($"{key}: installDir must be under Novolis\\...");
    var appId = windows.TryGetProperty("appId", out var aid) ? aid.GetString() ?? "" : "";
    if (string.IsNullOrWhiteSpace(appId))
        errors.Add($"{key}: missing AppId");
    else if (!appIds.TryAdd(appId, key))
        errors.Add($"Duplicate AppId {appId}");
    if (!windows.TryGetProperty("closeApplicationsFilter", out var filter) || string.IsNullOrWhiteSpace(filter.GetString()))
        errors.Add($"{key}: closeApplicationsFilter required");
    var retention = app.TryGetProperty("data", out var data) && data.TryGetProperty("retention", out var ret)
        ? ret.GetString()
        : null;
    if (retention != "uninstall-preserves-user-data")
        errors.Add($"{key}: data.retention must be uninstall-preserves-user-data");
    var appDataRoot = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("appDataRoot", out var adr)
        ? adr.GetString() ?? ""
        : "";
    if (!appDataRoot.Contains(@"\Novolis\", StringComparison.Ordinal) && !appDataRoot.Contains("/Novolis/", StringComparison.Ordinal))
        errors.Add($"{key}: appDataRoot must live under Novolis");
}

if (errors.Count > 0)
{
    foreach (var e in errors)
        Console.Error.WriteLine(e);
    return 1;
}

Console.WriteLine($"Installer policy OK for {appIds.Count} windows-inno apps.");
return 0;

static string ThisFile([CallerFilePath] string path = "") => path;
