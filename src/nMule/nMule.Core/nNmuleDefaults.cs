namespace Novolis.NMule.Core;

/// <summary>Editable network bootstrap defaults shipped by nMule.</summary>
public static class NmuleDefaults
{
    /// <summary>HTTPS server.met source used by a new profile.</summary>
    public const string ServerListUrl =
        "https://upd.emule-security.org/server.met";

    /// <summary>HTTPS nodes.dat source used by a new profile.</summary>
    public const string KadNodesUrl =
        "https://upd.emule-security.org/nodes.dat";

    /// <summary>Default maximum cached bootstrap payload.</summary>
    public const int MaximumBootstrapBytes = 2 * 1024 * 1024;

    /// <summary>Default maximum number of Kad contacts loaded from a file.</summary>
    public const int MaximumKadContacts = 10_000;
}
