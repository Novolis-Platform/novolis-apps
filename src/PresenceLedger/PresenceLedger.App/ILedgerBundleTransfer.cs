namespace PresenceLedger.App;

/// <summary>Connects the shared ledger UI to a host file picker.</summary>
public interface ILedgerBundleTransfer
{
    /// <summary>Opens a user-selected ledger archive for reading.</summary>
    Task<Stream?> OpenImportAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a user-selected destination for a ledger archive.</summary>
    Task<Stream?> CreateExportAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
