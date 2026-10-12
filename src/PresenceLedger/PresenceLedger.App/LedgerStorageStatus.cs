namespace PresenceLedger.App;

/// <summary>Storage status supplied by a platform host.</summary>
public sealed class LedgerStorageStatus : ILedgerStorageStatus
{
    /// <summary>Creates the initial storage status.</summary>
    public LedgerStorageStatus(
        string kind,
        int importedFileCount,
        string? importError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind;
        ImportedFileCount = importedFileCount;
        ImportError = importError;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public int ImportedFileCount { get; private set; }

    /// <inheritdoc />
    public string? ImportError { get; private set; }

    /// <summary>Records a later user-triggered recovery import.</summary>
    public void RecordImport(int filesCopied, string? errorMessage)
    {
        if (filesCopied > 0)
            ImportedFileCount += filesCopied;
        if (!string.IsNullOrWhiteSpace(errorMessage))
            ImportError = errorMessage;
    }
}
