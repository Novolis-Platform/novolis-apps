using Novolis.Avalonia.Mobile;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;
using NovolisMaps;

namespace NovolisMaps.Unit;

public sealed class MapStoreTests
{
    [Test]
    public async Task Pins_round_trip_through_the_local_store()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "NovolisMaps-Unit",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new MapPinStore(new TestAppDataPaths(root));
            var pins = new[]
            {
                new MapPin(
                    Guid.NewGuid(),
                    "Home",
                    new GeoCoordinate(58.14623, 7.99517)),
                new MapPin(
                    Guid.NewGuid(),
                    "Harbor",
                    new GeoCoordinate(58.147, 7.997)),
            };

            await store.WriteAsync(pins);
            var loaded = await store.ReadAsync();

            await Assert.That(loaded).Count().IsEqualTo(2);
            await Assert.That(loaded[0]).IsEqualTo(pins[0]);
            await Assert.That(loaded[1]).IsEqualTo(pins[1]);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Map_catalog_pairs_imagery_with_search_and_separate_caches()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "NovolisMaps-Unit",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var catalog = new MapSourceCatalog(
                new HttpClient(),
                new TestAppDataPaths(root));

            await Assert.That(catalog.Providers).Count().IsEqualTo(2);
            await Assert.That(catalog.Default.Id).IsEqualTo("topographic");
            await Assert.That(catalog.Providers[0].Search).IsTypeOf<GeonorgeAddressSearch>();
            await Assert.That(catalog.Providers[1].Search).IsTypeOf<NominatimPlaceSearch>();
            await Assert.That(
                    catalog.Providers[0].RasterSource.Template.Name)
                .IsNotEqualTo(catalog.Providers[1].RasterSource.Template.Name);
            await Assert.That(
                    Directory.Exists(Path.Combine(root, "cache", "maps", "topographic")))
                .IsTrue();
            await Assert.That(
                    Directory.Exists(Path.Combine(root, "cache", "maps", "street")))
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
        public string ProductName => "NovolisMaps.Unit";
        public string RootDirectory => root;
        public string WorkspaceDirectory => Path.Combine(root, "workspace");
        public string CacheDirectory => Path.Combine(root, "cache");
    }
}
