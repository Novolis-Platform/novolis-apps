using System.Text.Json.Serialization;

namespace PresenceLedger.Storage;

/// <summary>Integrity manifest stored inside a ledger archive.</summary>
public sealed class LedgerBundleManifest
{
    [JsonPropertyName("schema")]
    public string? Schema { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; }

    [JsonPropertyName("files")]
    public List<LedgerBundleFile> Files { get; init; } = [];
}
