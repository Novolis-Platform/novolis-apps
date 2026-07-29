using System.Text.Json;

namespace DraftStudio.Core;

internal sealed class DraftSettings
{
    public double LeftColumnPixels { get; set; } = 260;

    public double RightColumnPixels { get; set; } = 280;

    public bool SnapToGrid { get; set; } = true;

    public float GridStep { get; set; } = 0.5f;

    public string ViewMode { get; set; } = "draft";

    /// <summary>UI display unit; document coords remain meters.</summary>
    public string DisplayUnit { get; set; } = DraftUnits.Meter;

    /// <summary>Last opened/saved .cadjson path (null = default workspace file).</summary>
    public string? LastDocumentPath { get; set; }

    /// <summary>World-Y elevation of the active drawing plane (plan is XZ).</summary>
    public float DrawElevation { get; set; }

    /// <summary>When true, Line tool chains from the previous endpoint.</summary>
    public bool ContinuousLine { get; set; }

    /// <summary>When true, only entities near the draw elevation are hit-tested / fully bright.</summary>
    public bool IsolateLevel { get; set; } = true;

    /// <summary>Meters tolerance when matching entity elevation to draw level.</summary>
    public float LevelTolerance { get; set; } = 0.05f;
}

internal sealed class DraftSettingsStore
{
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public DraftSettingsStore(string? dataRoot = null)
    {
        DataRoot = string.IsNullOrWhiteSpace(dataRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "Draft Studio")
            : dataRoot;
    }

    public string DataRoot { get; }

    public string SettingsPath => Path.Combine(DataRoot, "settings.json");

    public string WorkspacePath => Path.Combine(DataRoot, "default-workspace");

    public string DocumentPath => Path.Combine(WorkspacePath, "draft.cadjson");

    public string PhysDocumentPath => Path.Combine(WorkspacePath, "draft.cadphys.json");

    public DraftSettings Settings { get; private set; } = new();

    public void Load()
    {
        Directory.CreateDirectory(DataRoot);
        if (!File.Exists(SettingsPath))
            return;

        var loaded = JsonSerializer.Deserialize<DraftSettings>(File.ReadAllText(SettingsPath), _json);
        if (loaded is not null)
            Settings = loaded;
    }

    public void Save()
    {
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, _json));
    }
}
