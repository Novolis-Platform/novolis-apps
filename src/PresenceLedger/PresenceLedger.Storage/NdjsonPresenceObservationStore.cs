using System.Runtime.CompilerServices;
using PresenceLedger.Core;

namespace PresenceLedger.Storage;

/// <summary>Daily UTC NDJSON store for retained platform observations.</summary>
public sealed class NdjsonPresenceObservationStore : IPresenceObservationStore
{
    readonly string _rootDirectory;
    readonly object _gate = new();
    readonly Dictionary<DateOnly, NdjsonFile> _files = new();

    /// <summary>Creates a daily observation store beneath the supplied directory.</summary>
    public NdjsonPresenceObservationStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = System.IO.Path.Combine(rootDirectory, "observations");
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>Returns the physical path for one UTC date.</summary>
    public string GetFilePath(DateOnly utcDate) =>
        System.IO.Path.Combine(
            _rootDirectory,
            $"{utcDate:yyyy-MM-dd}.ndjson");

    /// <inheritdoc />
    public ValueTask AppendAsync(
        PresenceObservationRecord observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return GetFile(observation.At.UtcDateTime).AppendAsync(
            observation,
            cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<PresenceObservationRecord> ReadAsync(
        DateOnly utcDate,
        CancellationToken cancellationToken = default) =>
        GetFile(utcDate).ReadAsync<PresenceObservationRecord>(cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<PresenceObservationRecord> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootDirectory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(_rootDirectory, "*.ndjson")
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            await foreach (var record in new NdjsonFile(path)
                               .ReadAsync<PresenceObservationRecord>(cancellationToken))
                yield return record;
        }
    }

    NdjsonFile GetFile(DateTime utcDate) =>
        GetFile(DateOnly.FromDateTime(DateTime.SpecifyKind(utcDate, DateTimeKind.Utc)));

    NdjsonFile GetFile(DateOnly utcDate)
    {
        lock (_gate)
        {
            if (!_files.TryGetValue(utcDate, out var file))
            {
                file = new NdjsonFile(GetFilePath(utcDate));
                _files.Add(utcDate, file);
            }

            return file;
        }
    }
}
