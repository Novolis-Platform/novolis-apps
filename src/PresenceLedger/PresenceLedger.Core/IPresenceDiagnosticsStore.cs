namespace PresenceLedger.Core;

/// <summary>Persists operational diagnostics separately from presence evidence.</summary>
public interface IPresenceDiagnosticsStore
{
    /// <summary>Appends one diagnostic entry.</summary>
    ValueTask AppendAsync(
        PresenceDiagnosticRecord diagnostic,
        CancellationToken cancellationToken = default);

    /// <summary>Reads all diagnostics in append order.</summary>
    IAsyncEnumerable<PresenceDiagnosticRecord> ReadAsync(
        CancellationToken cancellationToken = default);
}
