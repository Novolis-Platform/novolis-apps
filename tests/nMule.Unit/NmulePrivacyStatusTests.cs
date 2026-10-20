using System.Net;
using System.Net.Http;
using System.Text;
using Novolis.NMule.Core;

namespace Novolis.NMule.Unit;

public sealed class NmulePrivacyStatusTests
{
    [Test]
    public async Task Bootstrap_status_does_not_echo_source_or_payload_values()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "nMule-tests",
            Guid.NewGuid().ToString("N"));
        var paths = NmuleStoragePaths.FromRoot(Path.Combine(root, "profile"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ServerListPath)!);
        await File.WriteAllTextAsync(
            paths.ServerListPath,
            "# private-search-term\n127.0.0.1 4661",
            Encoding.UTF8);

        using var httpClient = new HttpClient(new FailingHttpHandler());
        await using var session = new NmuleSession(
            new AllowAllNmuleNetworkGate(),
            paths,
            httpClient);
        var statuses = new List<string>();
        session.StatusChanged += statuses.Add;

        await session.LoadServerListAsync(
            "https://private-content.example.test/server.met");

        foreach (var status in statuses)
        {
            await Assert.That(status).DoesNotContain("private-search-term");
            await Assert.That(status).DoesNotContain(
                "private-content.example.test");
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
