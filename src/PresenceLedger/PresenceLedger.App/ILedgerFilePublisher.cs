namespace PresenceLedger.App;

/// <summary>
/// Publishes the private ledger to a place the user can open.
/// Each call includes files that already exist and files created since the last call.
/// </summary>
public interface ILedgerFilePublisher
{
    /// <summary>Copies the current ledger tree and returns where it was published.</summary>
    Task<string> PublishAsync(CancellationToken cancellationToken = default);
}
