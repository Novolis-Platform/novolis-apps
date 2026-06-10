using System.Text.Json;

namespace ConceptStudio.Core;

internal sealed class ConceptSettings
{
    public double LeftColumnPixels { get; set; } = 280;

    public double RightColumnPixels { get; set; } = 320;

    public bool ShowWireframe { get; set; } = true;

    public bool SnapToGrid { get; set; } = true;

    public float GridStep { get; set; } = 0.5f;
}

internal sealed class ConceptSettingsStore
{
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public string DataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "Concept Studio");

    public string SettingsPath => Path.Combine(DataRoot, "settings.json");

    public string WorkspacePath => Path.Combine(DataRoot, "default-workspace");

    public string DocumentPath => Path.Combine(WorkspacePath, "concept.json");

    public ConceptSettings Settings { get; private set; } = new();

    public void Load()
    {
        Directory.CreateDirectory(DataRoot);
        if (!File.Exists(SettingsPath))
            return;

        var loaded = JsonSerializer.Deserialize<ConceptSettings>(File.ReadAllText(SettingsPath), _json);
        if (loaded is not null)
            Settings = loaded;
    }

    public void Save()
    {
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, _json));
    }
}
