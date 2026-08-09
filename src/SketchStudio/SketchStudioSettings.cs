using System.Text.Json;
using System.Text.Json.Serialization;

namespace SketchStudio;

/// <summary>Last path + MRU under %LocalAppData%\Novolis\Sketch Studio\settings.json.</summary>
internal sealed class SketchStudioSettings
{
    const int MaxRecent = 8;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    readonly string _settingsPath;

    public SketchStudioSettings()
    {
        DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Sketch Studio");
        _settingsPath = Path.Combine(DataRoot, "settings.json");
        Directory.CreateDirectory(DataRoot);
        Load();
    }

    public string DataRoot { get; }

    public string? LastDocumentPath { get; private set; }

    public IReadOnlyList<string> RecentPaths => _recent;

    readonly List<string> _recent = [];

    public void RememberDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var full = Path.GetFullPath(path);
        LastDocumentPath = full;
        _recent.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        _recent.Insert(0, full);
        while (_recent.Count > MaxRecent)
            _recent.RemoveAt(_recent.Count - 1);
        Save();
    }

    public void Load()
    {
        LastDocumentPath = null;
        _recent.Clear();
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json, JsonOptions);
            if (dto is null)
                return;

            if (!string.IsNullOrWhiteSpace(dto.LastDocumentPath))
                LastDocumentPath = Path.GetFullPath(dto.LastDocumentPath);

            if (dto.RecentPaths is { Count: > 0 })
            {
                foreach (var p in dto.RecentPaths)
                {
                    if (string.IsNullOrWhiteSpace(p))
                        continue;
                    var full = Path.GetFullPath(p);
                    if (_recent.Exists(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    _recent.Add(full);
                    if (_recent.Count >= MaxRecent)
                        break;
                }
            }
        }
        catch
        {
            // Ignore corrupt settings; start fresh.
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataRoot);
        var dto = new SettingsDto
        {
            LastDocumentPath = LastDocumentPath,
            RecentPaths = _recent.ToList()
        };
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(dto, JsonOptions));
    }

    sealed class SettingsDto
    {
        public string? LastDocumentPath { get; set; }
        public List<string>? RecentPaths { get; set; }
    }
}
