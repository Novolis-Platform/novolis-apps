namespace PresenceLedger.Storage;

/// <summary>Summary returned after a ledger archive is created.</summary>
public sealed record LedgerBundleResult(int FileCount, long Bytes);
