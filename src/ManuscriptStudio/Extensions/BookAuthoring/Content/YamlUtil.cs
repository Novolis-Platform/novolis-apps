using YamlDotNet.Serialization;

namespace ManuscriptStudio.Extensions.BookAuthoring.Content;

internal static class YamlUtil
{
    private static readonly IDeserializer Deserializer =
        new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

    public static Dictionary<string, object?> LoadYamlFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var raw = File.ReadAllText(path);
        var obj = Deserializer.Deserialize<Dictionary<string, object?>>(raw)
                  ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        return new Dictionary<string, object?>(obj, StringComparer.OrdinalIgnoreCase);
    }

    public static string? GetString(Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var v) && v is not null ? v.ToString()?.Trim() : null;

    public static bool GetBool(Dictionary<string, object?> dict, string key, bool defaultValue = false)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
            return defaultValue;
        if (v is bool b)
            return b;
        return bool.TryParse(v.ToString(), out var parsed) && parsed;
    }
}
