using System.Text.Json;

namespace ManuscriptStudio.Core;

internal sealed class ManuscriptSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public ManuscriptSettingsStore()
    {
        var root = ManuscriptAppContext.ResolveDataRoot();
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public ManuscriptSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<ManuscriptSettings>(json);
            if (loaded is not null)
                Settings = loaded;
        }
        catch
        {
            // Ignore corrupt settings.
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public string DataRoot => Path.GetDirectoryName(_settingsPath)!;
}
