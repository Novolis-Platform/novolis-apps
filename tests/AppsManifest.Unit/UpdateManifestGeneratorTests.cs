using System.Text.Json;

namespace Novolis.Apps.Manifest.Unit;

public sealed class UpdateManifestGeneratorTests
{
    [Test]
    public async Task Generator_emits_deterministic_catalog_with_sizes_hashes_and_urls()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novolis-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(
                Path.Combine(root, "Example-2026.1.2.0-win-x64.zip"),
                [1, 2, 3, 4]);
            File.WriteAllBytes(
                Path.Combine(root, "ExampleSetup-2026.1.2.0-win-x64.exe"),
                [5, 6, 7]);
            var metadataPath = Path.Combine(root, "matrix.json");
            File.WriteAllText(
                metadataPath,
                """
                {"include":[
                  {
                    "key":"example",
                    "display_name":"Example",
                    "artifact_prefix":"Example",
                    "update_app_id":"Novolis.Example",
                    "update_repository":"https://github.com/Novolis-Platform/novolis-apps",
                    "update_channel":"stable",
                    "update_distribution":"direct-github",
                    "declared_update_artifacts":["windowsInstaller","windowsPortable"]
                  }
                ]}
                """);
            var output = Path.Combine(root, "Novolis.Update.json");
            var args = Arguments(metadataPath, root, output);

            await Assert.That(UpdateManifestGenerator.Generate(args)).IsEqualTo(0);
            var first = File.ReadAllText(output);
            await Assert.That(UpdateManifestGenerator.Generate(args)).IsEqualTo(0);
            var second = File.ReadAllText(output);

            await Assert.That(second).IsEqualTo(first);
            using var json = JsonDocument.Parse(first);
            await Assert.That(json.RootElement.GetProperty("updates").GetArrayLength()).IsEqualTo(1);
            await Assert.That(json.RootElement
                    .GetProperty("updates")[0]
                    .GetProperty("artifacts")
                    .GetArrayLength())
                .IsEqualTo(2);
            await Assert.That(first).Contains("\"sha256\"");
            await Assert.That(first).Contains("releases/download/v2026.1.2.0");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Generator_rejects_declared_artifacts_that_are_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novolis-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var metadataPath = Path.Combine(root, "matrix.json");
            File.WriteAllText(
                metadataPath,
                """
                {"include":[
                  {
                    "key":"example",
                    "display_name":"Example",
                    "artifact_prefix":"Example",
                    "update_app_id":"Novolis.Example",
                    "update_channel":"stable",
                    "update_distribution":"direct-github",
                    "declared_update_artifacts":["androidApk"]
                  }
                ]}
                """);

            await Assert.That(() => UpdateManifestGenerator.Generate(
                    Arguments(metadataPath, root, Path.Combine(root, "out.json"))))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static string[] Arguments(string metadata, string assets, string output) =>
    [
        "--metadata", metadata,
        "--assets-dir", assets,
        "--version", "2026.1.2.0",
        "--tag", "v2026.1.2.0",
        "--release-url", "https://github.com/Novolis-Platform/novolis-apps/releases/tag/v2026.1.2.0",
        "--output", output,
        "--built-at", "2026-10-03T00:00:00.0000000Z",
    ];
}
