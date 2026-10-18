using Novolis.NMule.Core;

namespace Novolis.NMule.Unit;

public sealed class NmuleStorageTests
{
    [Test]
    public async Task Storage_paths_stay_below_the_supplied_app_private_root()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "nMule-tests",
            Guid.NewGuid().ToString("N"));

        var paths = NmuleStoragePaths.FromRoot(root);

        await Assert.That(paths.ServerListPath)
            .IsEqualTo(Path.Combine(Path.GetFullPath(root), "server.met"));
        await Assert.That(paths.KadNodesPath)
            .IsEqualTo(Path.Combine(Path.GetFullPath(root), "nodes.dat"));
        await Assert.That(paths.CompletedDirectory)
            .IsEqualTo(Path.Combine(Path.GetFullPath(root), "completed"));
    }

    [Test]
    public async Task Session_loads_a_text_server_list_and_creates_completed_storage()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "nMule-tests",
            Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "servers.txt");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            source,
            """
            # nMule test server list
            127.0.0.1 4661
            127.0.0.1 4661
            """);

        var paths = NmuleStoragePaths.FromRoot(Path.Combine(root, "profile"));
        await using var session = new NmuleSession(
            new AllowAllNmuleNetworkGate(),
            paths);

        var list = await session.LoadServerListAsync(source);

        await Assert.That(list.Servers).Count().IsEqualTo(1);
        await Assert.That(Directory.Exists(paths.CompletedDirectory)).IsTrue();
    }

    [Test]
    public async Task Shipped_bootstrap_sources_are_https()
    {
        await Assert.That(Uri.TryCreate(
                NmuleDefaults.ServerListUrl,
                UriKind.Absolute,
                out var serverUri))
            .IsTrue();
        await Assert.That(serverUri!.Scheme).IsEqualTo(Uri.UriSchemeHttps);
        await Assert.That(Uri.TryCreate(
                NmuleDefaults.KadNodesUrl,
                UriKind.Absolute,
                out var kadUri))
            .IsTrue();
        await Assert.That(kadUri!.Scheme).IsEqualTo(Uri.UriSchemeHttps);
    }
}
