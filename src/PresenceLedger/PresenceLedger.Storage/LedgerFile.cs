namespace PresenceLedger.Storage;

/// <summary>One NDJSON file in the private ledger tree.</summary>
public readonly record struct LedgerFile(string RelativePath, string FullPath, long LengthBytes);
