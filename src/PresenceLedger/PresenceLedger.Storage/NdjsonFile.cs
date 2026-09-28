using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PresenceLedger.Storage;

/// <summary>Serialized JSON options shared by the local ledger files.</summary>
internal static class NdjsonJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/// <summary>Small serialized append-only JSON-lines file.</summary>
internal sealed class NdjsonFile
{
    readonly SemaphoreSlim _writeGate = new(1, 1);

    public NdjsonFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    public string Path { get; }

    public async ValueTask AppendAsync<T>(
        T value,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var stream = new FileStream(
                Path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(value, NdjsonJson.Options)
                    .AsMemory(),
                cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async IAsyncEnumerable<T> ReadAsync<T>(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
            yield break;

        await using var stream = new FileStream(
            Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(line, NdjsonJson.Options);
            }
            catch (JsonException)
            {
                // One malformed physical line must not hide later ledger entries.
                continue;
            }

            if (value is not null)
                yield return value;
        }
    }
}
