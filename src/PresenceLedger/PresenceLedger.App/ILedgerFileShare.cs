namespace PresenceLedger.App;

/// <summary>Sends the current ledger files through the host share sheet.</summary>
public interface ILedgerFileShare
{
    /// <summary>Shares every ledger file that exists now, including files added since the last share.</summary>
    Task ShareAsync(CancellationToken cancellationToken = default);
}
