using System.Text.Json;

namespace DraftStudio.Core;

internal sealed class DraftSettings
{
    public double LeftColumnPixels { get; set; } = 260;

    public double RightColumnPixels { get; set; } = 280;

    public bool SnapToGrid { get; set; } = true;

    public float GridStep { get; set; } = 0.5f;

    public string ViewMode { get; set; } = "draft";
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
