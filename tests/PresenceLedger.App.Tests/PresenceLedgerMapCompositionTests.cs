using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Map;
using Novolis.IO.Maps;
using PresenceLedger.App;

namespace PresenceLedger.App.Tests;

public sealed class PresenceLedgerMapCompositionTests
{
    [Test]
    public async Task AddPresenceLedger_registers_the_shared_kartverket_map_stack()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Novolis-PresenceLedger-AppTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new ServiceCollection()
                .AddSingleton<IAppDataPaths>(new TestAppDataPaths(root))
                .AddPresenceLedger();
            using var provider = services.BuildServiceProvider();

            var raster = provider.GetRequiredService<IMapRasterSource>();
            var decoded = provider.GetRequiredService<IMapTileSource>();
            var search = provider.GetRequiredService<IMapPlaceSearch>();
            var client = provider.GetRequiredService<HttpClient>();

            await Assert.That(raster.Template).IsEqualTo(MapPresets.KartverketTopo);
            await Assert.That(decoded).IsTypeOf<RasterMapTileSource>();
            await Assert.That(((RasterMapTileSource)decoded).Attribution)
                .IsEqualTo(MapPresets.KartverketTopo.Attribution);
            await Assert.That(search).IsTypeOf<GeonorgeAddressSearch>();
            await Assert.That(client.DefaultRequestHeaders.UserAgent.ToString())
                .Contains("PresenceLedger/2026.1");
            await Assert.That(Directory.Exists(Path.Combine(root, "cache", "maps")))
                .IsTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    sealed class TestAppDataPaths(string root) : IAppDataPaths
    {
        public string ProductName => "PresenceLedger.Tests";
        public string RootDirectory => root;
        public string WorkspaceDirectory => Path.Combine(root, "workspace");
        public string CacheDirectory => Path.Combine(root, "cache");
    }
}
