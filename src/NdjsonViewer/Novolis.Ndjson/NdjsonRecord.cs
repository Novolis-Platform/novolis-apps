using System.Text.Json;

namespace Novolis.Ndjson;

public sealed record NdjsonRecord(
    long Number,
    long ByteOffset,
    JsonElement? Json,
    string? Raw,
    JsonException? Error)
{
    public bool IsValid => Error is null;
}
