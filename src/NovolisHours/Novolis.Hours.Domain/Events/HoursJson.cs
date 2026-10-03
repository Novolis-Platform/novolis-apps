using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Hours.Domain;

/// <summary>Shared deterministic serialization settings for JSON-backed journal payloads.</summary>
public static class HoursJson
{
    /// <summary>Gets the immutable options used for journal persistence and API payloads.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };
}
