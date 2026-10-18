namespace Novolis.NMule.Core;

/// <summary>App-private locations used by the nMule session.</summary>
public sealed record NmuleStoragePaths(
    string ServerListPath,
    string KadNodesPath,
    string CompletedDirectory)
{
    /// <summary>Creates the standard layout below an app-private root.</summary>
    public static NmuleStoragePaths FromRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var fullRoot = Path.GetFullPath(root);
        return new NmuleStoragePaths(
            Path.Combine(fullRoot, "server.met"),
            Path.Combine(fullRoot, "nodes.dat"),
            Path.Combine(fullRoot, "completed"));
    }
}
