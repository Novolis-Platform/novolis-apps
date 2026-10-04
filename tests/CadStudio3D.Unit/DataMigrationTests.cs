namespace CadStudio3D.Unit;

public sealed class DataMigrationTests
{
    [Test]
    public async Task LegacyFiles_AreCopiedWithoutOverwriteOrConversion()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "novolis-cadstudio-migration-" + Guid.NewGuid().ToString("N"));
        var consolidated = Path.Combine(root, "CAD Studio 3D");
        var legacy = Path.Combine(root, "Ship Designer");
        Directory.CreateDirectory(legacy);
        try
        {
            var existing = Path.Combine(legacy, "existing.shipjson");
            var newFile = Path.Combine(legacy, "new.cadjson");
            await File.WriteAllTextAsync(existing, "legacy-ship");
            await File.WriteAllTextAsync(newFile, "legacy-cad");

            var existingTarget = Path.Combine(
                consolidated,
                "migrations",
                "Ship Designer",
                "existing.shipjson");
            Directory.CreateDirectory(Path.GetDirectoryName(existingTarget)!);
            await File.WriteAllTextAsync(existingTarget, "newer-copy");

            var report = CadStudio3D.CadStudioDataMigration.Run(
                consolidated,
                [legacy]);

            await Assert.That(report.Completed).IsTrue();
            await Assert.That(report.FilesCopied).IsEqualTo(1);
            await Assert.That(report.Conflicts).IsEqualTo(1);
            await Assert.That(File.ReadAllText(existingTarget)).IsEqualTo("newer-copy");
            await Assert.That(
                File.Exists(Path.Combine(
                    consolidated,
                    "migrations",
                    "Ship Designer",
                    "new.cadjson"))).IsTrue();
            await Assert.That(File.Exists(existing)).IsTrue();
            await Assert.That(File.Exists(newFile)).IsTrue();
            await Assert.That(File.Exists(report.ManifestPath)).IsTrue();
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
