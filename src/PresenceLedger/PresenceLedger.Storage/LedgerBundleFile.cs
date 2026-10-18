using System.Text.Json.Serialization;

namespace PresenceLedger.Storage;

/// <summary>One NDJSON file listed by a ledger archive manifest.</summary>
public sealed class LedgerBundleFile
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("length")]
    public long Length { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;
}
