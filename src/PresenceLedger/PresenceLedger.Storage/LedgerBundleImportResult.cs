namespace PresenceLedger.Storage;

/// <summary>Summary returned after a ledger archive is restored.</summary>
public sealed record LedgerBundleImportResult(int FileCount, long Bytes);
