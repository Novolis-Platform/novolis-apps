using System.Text.Json;
using Novolis.Ndjson;

namespace Novolis.Ndjson.App.Viewer;

public sealed class NdjsonRecordDisplay
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
    };

    public NdjsonRecordDisplay(NdjsonRecord record)
    {
        Record = record;
        IsValid = record.IsValid;
        Status = IsValid ? "VALID" : "MALFORMED";
        Preview = CreatePreview(record);
        Details = CreateDetails(record);
        CopyText = record.Json is { } json ? json.GetRawText() : record.Raw ?? string.Empty;
    }

    public NdjsonRecord Record { get; }

    public long Number => Record.Number;

    public long ByteOffset => Record.ByteOffset;

    public bool IsValid { get; }

    public string Status { get; }

    public string Preview { get; }

    public string Details { get; }

    public string CopyText { get; }

    private static string CreatePreview(NdjsonRecord record)
    {
        var value = record.Json is { } json ? json.GetRawText() : record.Raw ?? string.Empty;
        return Truncate(value.ReplaceLineEndings(" "), 4_000);
    }

    private static string CreateDetails(NdjsonRecord record)
    {
        if (record.Json is not { } json)
        {
            return $"Raw input:{Environment.NewLine}{record.Raw ?? string.Empty}"
                + $"{Environment.NewLine}{Environment.NewLine}Parse error:{Environment.NewLine}{record.Error?.Message}";
        }

        return JsonSerializer.Serialize(json, PrettyJson);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : $"{value[..maximumLength]} …";
}
