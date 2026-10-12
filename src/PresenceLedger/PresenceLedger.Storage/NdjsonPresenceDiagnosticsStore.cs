using PresenceLedger.Core;

namespace PresenceLedger.Storage;

/// <summary>Append-only NDJSON diagnostics file for one local ledger.</summary>
public sealed class NdjsonPresenceDiagnosticsStore : IPresenceDiagnosticsStore
{
    readonly NdjsonFile _file;

    /// <summary>Creates a diagnostics store at the supplied file path.</summary>
    public NdjsonPresenceDiagnosticsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        _file = new NdjsonFile(filePath);
    }

    /// <summary>Physical path of the diagnostics NDJSON file.</summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public ValueTask AppendAsync(
        PresenceDiagnosticRecord diagnostic,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return _file.AppendAsync(diagnostic, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<PresenceDiagnosticRecord> ReadAsync(
        CancellationToken cancellationToken = default) =>
        _file.ReadAsync<PresenceDiagnosticRecord>(cancellationToken);
}
