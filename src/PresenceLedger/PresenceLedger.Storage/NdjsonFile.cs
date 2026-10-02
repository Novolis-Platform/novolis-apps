using System.Runtime.CompilerServices;
using System.Text.Json;
using Novolis.IO.Ndjson;

namespace PresenceLedger.Storage;

/// <summary>Small serialized append-only JSON-lines file.</summary>
internal sealed class NdjsonFile
{
    readonly SemaphoreSlim _writeGate = new(1, 1);
    readonly NdjsonFileWriter _writer;

    public NdjsonFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        _writer = new NdjsonFileWriter(path);
    }

    public string Path { get; }

    public async ValueTask AppendAsync<T>(
        T value,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, NdjsonJson.Options);
            await _writer.AppendJsonAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask ReplaceAsync<T>(
        IEnumerable<T> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporary = Path + ".tmp";
            await using (var stream = new FileStream(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(stream))
            {
                foreach (var value in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync(
                        JsonSerializer.Serialize(value, NdjsonJson.Options).AsMemory(),
                        cancellationToken);
                }
            }

            File.Move(temporary, Path, overwrite: true);
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
            catch (ArgumentException)
            {
                // Constructor validation can reject a syntactically valid but
                // corrupt line without hiding later local records.
                continue;
            }

            if (value is not null)
                yield return value;
        }
    }
}
