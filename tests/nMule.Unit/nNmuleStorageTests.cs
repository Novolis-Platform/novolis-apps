using System.Net;
using System.Net.Http;
using System.Text;
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

    [Test]
    public async Task Session_blocks_server_connect_before_opening_a_socket()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "nMule-tests",
            Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "servers.txt");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(source, "127.0.0.1 4661");

        await using var session = new NmuleSession(
            new DenyNetworkGate(),
            NmuleStoragePaths.FromRoot(Path.Combine(root, "profile")));
        await session.LoadServerListAsync(source);

        await Assert.That(async () => await session.ConnectServerAsync())
            .Throws<NmuleNetworkBlockedException>();
    }

    [Test]
    public async Task Failed_https_refresh_uses_the_cached_server_list()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "nMule-tests",
            Guid.NewGuid().ToString("N"));
        var paths = NmuleStoragePaths.FromRoot(Path.Combine(root, "profile"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ServerListPath)!);
        await File.WriteAllTextAsync(
            paths.ServerListPath,
            "127.0.0.1 4661",
            Encoding.UTF8);

        using var httpClient = new HttpClient(new FailingHttpHandler());
        await using var session = new NmuleSession(
            new AllowAllNmuleNetworkGate(),
            paths,
            httpClient);

        var list = await session.LoadServerListAsync(
            "https://bootstrap.example.test/server.met");

        await Assert.That(list.Servers).Count().IsEqualTo(1);
    }

    private sealed class DenyNetworkGate : INmuleNetworkGate
    {
        public bool SupportsTailscaleOverride => true;

        public bool AllowTailscale { get; set; }

        public bool SupportsUnrestrictedNetworkOverride => true;

        public bool AllowUnrestrictedNetwork { get; set; }

        public ValueTask<NmuleNetworkDecision> CheckAsync(
            IPAddress? endpointAddress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                NmuleNetworkDecision.Deny("test gate blocked network access"));
        }
    }

    private sealed class FailingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("test bootstrap unavailable"));
    }
}
