using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.App;

/// <summary>Builds the user-triggered export artifacts for the local ledger.</summary>
public sealed class LedgerExportService
{
    readonly NdjsonPresenceStorage _storage;
    readonly IPresenceDiagnosticsStore _diagnostics;

    /// <summary>Creates an export service over the local storage bundle.</summary>
    public LedgerExportService(
        NdjsonPresenceStorage storage,
        IPresenceDiagnosticsStore diagnostics)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Rebuilds <c>cumulative.ndjson</c> from all daily observation files.
    /// </summary>
    public async Task<int> BuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _storage.BuildCumulativeAsync(cancellationToken);
            await TryRecordAsync(
                new PresenceDiagnosticRecord(
                    DateTimeOffset.UtcNow,
                    "info",
                    "export-built",
                    $"Built cumulative observation export with {count} record(s).",
                    _storage.CumulativeFilePath));
            return count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await TryRecordAsync(
                new PresenceDiagnosticRecord(
                    DateTimeOffset.UtcNow,
                    "error",
                    "export-build-failed",
                    "Could not build the cumulative observation export.",
                    exception.ToString()));
            throw;
        }
    }

    /// <summary>Records a best-effort operational diagnostic.</summary>
    public async Task RecordAsync(
        string level,
        string code,
        string message,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        await _diagnostics.AppendAsync(
            new PresenceDiagnosticRecord(
                DateTimeOffset.UtcNow,
                level,
                code,
                message,
                detail),
            cancellationToken);
    }

    async Task TryRecordAsync(PresenceDiagnosticRecord diagnostic)
    {
        try
        {
            await _diagnostics.AppendAsync(diagnostic);
        }
        catch
        {
            // Diagnostics must never make an export failure harder to surface.
        }
    }
}
