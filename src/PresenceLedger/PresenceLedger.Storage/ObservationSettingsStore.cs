using System.Text.Json;
using System.Text.Json.Serialization;
using PresenceLedger.Core;

namespace PresenceLedger.Storage;

/// <summary>Loads and saves <see cref="ObservationSettings"/> beside the ledger root.</summary>
public sealed class ObservationSettingsStore
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    readonly object _gate = new();
    ObservationSettings _current = ObservationSettings.Default;

    /// <summary>Creates a settings store under the ledger root.</summary>
    public ObservationSettingsStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        Directory.CreateDirectory(rootDirectory);
        FilePath = Path.Combine(rootDirectory, "observation-settings.json");
        _current = LoadOrDefault(FilePath);
    }

    /// <summary>Absolute path of the settings file.</summary>
    public string FilePath { get; }

    /// <summary>Latest settings in memory.</summary>
    public ObservationSettings Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <summary>Raised after settings are saved successfully.</summary>
    public event EventHandler? Changed;

    /// <summary>Persists settings and notifies listeners.</summary>
    public async Task SaveAsync(
        ObservationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var json = JsonSerializer.Serialize(settings, Json);
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporary, json, cancellationToken);
        File.Copy(temporary, FilePath, overwrite: true);
        File.Delete(temporary);

        lock (_gate)
            _current = settings;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    static ObservationSettings LoadOrDefault(string path)
    {
        try
        {
            if (!File.Exists(path))
                return ObservationSettings.Default;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ObservationSettings>(json, Json)
                ?? ObservationSettings.Default;
        }
        catch
        {
            return ObservationSettings.Default;
        }
    }
}
