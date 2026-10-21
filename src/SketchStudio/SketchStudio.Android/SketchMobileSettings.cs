using System.Text.Json;
using System.Text.Json.Serialization;

namespace SketchStudio.Android;

/// <summary>App-private working copies and recent sketch metadata for Android.</summary>
internal sealed class SketchMobileSettings
{
    private const int MaxRecent = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _settingsPath;
    private readonly List<RecentSketch> _recent = [];

    public SketchMobileSettings()
    {
        DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Sketch Studio",
            "Android");
        Directory.CreateDirectory(DataRoot);
        _settingsPath = Path.Combine(DataRoot, "settings.json");
        Load();
    }

    public string DataRoot { get; }

    public string? LastWorkingPath { get; private set; }

    public IReadOnlyList<RecentSketch> Recent => _recent;

    public string CreateWorkingPath(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "untitled";

        foreach (var invalid in Path.GetInvalidFileNameChars())
            stem = stem.Replace(invalid, '_');

        return Path.Combine(
            DataRoot,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}-{stem}.sketchjson");
    }

    public void Remember(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var full = Path.GetFullPath(path);
        _recent.RemoveAll(item =>
            string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase));
        _recent.Insert(0, new RecentSketch(full, string.IsNullOrWhiteSpace(name)
            ? Path.GetFileName(full)
            : name));
        while (_recent.Count > MaxRecent)
            _recent.RemoveAt(_recent.Count - 1);

        LastWorkingPath = full;
        Save();
    }

    public void Load()
    {
        LastWorkingPath = null;
        _recent.Clear();
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            var dto = JsonSerializer.Deserialize<SettingsDto>(
                File.ReadAllText(_settingsPath),
                JsonOptions);
            if (dto is null)
                return;

            if (!string.IsNullOrWhiteSpace(dto.LastWorkingPath))
                LastWorkingPath = Path.GetFullPath(dto.LastWorkingPath);

            if (dto.Recent is not { Count: > 0 })
                return;

            foreach (var item in dto.Recent)
            {
                if (string.IsNullOrWhiteSpace(item.Path))
                    continue;

                var full = Path.GetFullPath(item.Path);
                if (_recent.Any(existing =>
                        string.Equals(existing.Path, full, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _recent.Add(new RecentSketch(
                    full,
                    string.IsNullOrWhiteSpace(item.Name)
                        ? Path.GetFileName(full)
                        : item.Name));
                if (_recent.Count >= MaxRecent)
                    break;
            }
        }
        catch
        {
            LastWorkingPath = null;
            _recent.Clear();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DataRoot);
            var dto = new SettingsDto
            {
                LastWorkingPath = LastWorkingPath,
                Recent = _recent.ToList(),
            };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch
        {
            // A working copy is still useful when metadata persistence is unavailable.
        }
    }

    private sealed class SettingsDto
    {
        public string? LastWorkingPath { get; set; }

        public List<RecentSketch>? Recent { get; set; }
    }
}
