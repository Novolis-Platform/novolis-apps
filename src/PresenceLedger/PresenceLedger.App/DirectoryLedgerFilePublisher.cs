using PresenceLedger.Storage;

namespace PresenceLedger.App;

/// <summary>Copies the ledger into a normal directory, such as the user Downloads folder.</summary>
public sealed class DirectoryLedgerFilePublisher : ILedgerFilePublisher
{
    readonly string _sourceRoot;
    readonly string _destinationRoot;

    /// <summary>Creates a publisher that copies from the private root into <paramref name="destinationRoot"/>.</summary>
    public DirectoryLedgerFilePublisher(string sourceRoot, string destinationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        _sourceRoot = sourceRoot;
        _destinationRoot = destinationRoot;
    }

    /// <summary>Copies into <c>Downloads/PresenceLedger</c> under the user profile.</summary>
    public static DirectoryLedgerFilePublisher ForDownloads(string sourceRoot) =>
        new(
            sourceRoot,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "PresenceLedger"));

    /// <inheritdoc />
    public Task<string> PublishAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LedgerFiles.CopyTo(_sourceRoot, _destinationRoot);
        return Task.FromResult(_destinationRoot);
    }
}
