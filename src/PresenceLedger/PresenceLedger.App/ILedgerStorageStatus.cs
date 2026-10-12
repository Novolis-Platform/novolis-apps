namespace PresenceLedger.App;

/// <summary>Describes the resolved ledger root and any one-time data import.</summary>
public interface ILedgerStorageStatus
{
    /// <summary>Resolved storage kind, such as app-specific external files.</summary>
    string Kind { get; }

    /// <summary>Number of legacy files copied into the current root.</summary>
    int ImportedFileCount { get; }

    /// <summary>Best-effort import error, when one occurred.</summary>
    string? ImportError { get; }
}
