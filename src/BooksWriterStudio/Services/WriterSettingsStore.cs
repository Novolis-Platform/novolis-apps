using System.Text.Json;

namespace BooksWriterStudio.Services;

internal sealed class WriterSettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    readonly string _settingsPath;

    public WriterSettingsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "BooksWriterStudio");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public WriterSettings Settings { get; private set; } = new();

    public string AppDataRoot => Path.GetDirectoryName(_settingsPath)!;

    public void Load()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<WriterSettings>(json);
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
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    public void LoadWorkspaceOverlay(string contentRoot)
    {
        var path = WorkspaceSettingsPath(contentRoot);
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<WriterSettings>(json);
            if (loaded is null)
                return;

            if (!string.IsNullOrWhiteSpace(loaded.LastSeriesId))
                Settings.LastSeriesId = loaded.LastSeriesId;
            if (!string.IsNullOrWhiteSpace(loaded.LastBookId))
                Settings.LastBookId = loaded.LastBookId;
            if (!string.IsNullOrWhiteSpace(loaded.LastChapterId))
                Settings.LastChapterId = loaded.LastChapterId;
        }
        catch
        {
            // Ignore corrupt workspace settings.
        }
    }

    public void SaveWorkspaceOverlay(string contentRoot)
    {
        var path = WorkspaceSettingsPath(contentRoot);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var overlay = new WriterSettings
        {
            LastSeriesId = Settings.LastSeriesId,
            LastBookId = Settings.LastBookId,
            LastChapterId = Settings.LastChapterId,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(overlay, JsonOptions));
    }

    static string WorkspaceSettingsPath(string contentRoot) =>
        Path.Combine(contentRoot, ".writer", "settings.json");
}
