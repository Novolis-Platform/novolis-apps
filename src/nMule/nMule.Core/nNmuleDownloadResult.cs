namespace Novolis.NMule.Core;

/// <summary>Completed nMule download metadata.</summary>
public sealed record NmuleDownloadResult(
    string FileName,
    string Path,
    long Size,
    string SourceDescription);
