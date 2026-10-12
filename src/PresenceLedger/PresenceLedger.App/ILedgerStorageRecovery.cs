namespace PresenceLedger.App;

/// <summary>Recovers a ledger from a user-selected shared Android folder.</summary>
public interface ILedgerStorageRecovery
{
    /// <summary>Opens the host folder picker and imports legacy ledger files.</summary>
    Task<int> RequestImportAsync(CancellationToken cancellationToken = default);
}
