using System.Text.Json;

namespace FrankMoat.Game;

internal sealed class CampaignSaveStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public CampaignSaveStore(string? path = null)
    {
        _path = path
            ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Novolis",
                "frank-moat",
                "save.json");
    }

    public string Path => _path;

    public void Save(CampaignState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The save path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        var record = new SaveRecord
        {
            Campaign = state,
        };
        File.WriteAllText(temporary, JsonSerializer.Serialize(record, Options));
        File.Move(temporary, _path, overwrite: true);
    }

    public CampaignState? TryLoad()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<SaveRecord>(
                File.ReadAllText(_path),
                Options);
            return record?.Campaign;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Delete()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
