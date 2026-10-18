using System.Text.Json;

namespace FrankMoat.Game;

internal sealed class CampaignSaveStore
{
    private const string NamedSavePrefix = "save-";

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

    public void Save(CampaignState state, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var path = ResolvePath(name);
        var directory = System.IO.Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The save path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        var record = new SaveRecord
        {
            Campaign = state,
        };
        File.WriteAllText(temporary, JsonSerializer.Serialize(record, Options));
        File.Move(temporary, path, overwrite: true);
    }

    public CampaignState? TryLoad(string? name = null)
    {
        var path = ResolvePath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<SaveRecord>(
                File.ReadAllText(path),
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

    public IReadOnlyList<string> ListNames()
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (directory is null || !Directory.Exists(directory))
        {
            return [];
        }

        var extension = System.IO.Path.GetExtension(_path);
        return Directory
            .EnumerateFiles(directory, $"{NamedSavePrefix}*{extension}")
            .Select(file => System.IO.Path.GetFileNameWithoutExtension(file)[NamedSavePrefix.Length..])
            .Where(name => name.Length > 0)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Delete(string? name = null)
    {
        var path = ResolvePath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string ResolvePath(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return _path;
        }

        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The save path has no directory.");
        var extension = System.IO.Path.GetExtension(_path);
        return System.IO.Path.Combine(
            directory,
            NamedSavePrefix + NormalizeName(name) + extension);
    }

    private static string NormalizeName(string name)
    {
        var characters = name
            .Trim()
            .Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' or ' '
                    ? character
                    : '-')
            .ToArray();
        var normalized = new string(characters).Trim().Replace(' ', '-');
        return string.IsNullOrWhiteSpace(normalized)
            ? "checkpoint"
            : normalized[..Math.Min(normalized.Length, 48)];
    }
}
