using System.Text.Json;
using Novolis.Avalonia.Mobile;

namespace NovolisMaps;

/// <summary>Small atomic JSON store for pins kept on the current device.</summary>
internal sealed class MapPinStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    readonly string _path;
    readonly SemaphoreSlim _gate = new(1, 1);

    public MapPinStore(IAppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.RootDirectory, "pins.json");
    }

    /// <summary>Reads all saved pins, returning an empty set on first launch.</summary>
    public async Task<IReadOnlyList<MapPin>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_path))
                return [];

            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<List<MapPin>>(
                       stream,
                       JsonOptions,
                       cancellationToken)
                   ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Replaces the pin file atomically.</summary>
    public async Task WriteAsync(
        IReadOnlyList<MapPin> pins,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pins);
        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("The pin path has no directory.");
            Directory.CreateDirectory(directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    pins,
                    JsonOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
