using Novolis.Apps.Manifest;

namespace Novolis.Apps.Manifest.Unit;

public sealed class CiMatrixPlannerTests
{
    [Test]
    public async Task FullManifestProducesOnlyRealPlatformRows()
    {
        var document = LoadManifest();
        var plan = Program.CreateCiMatrix(document, [], forceAll: false, fullCoverage: false);

        await Assert.That(document.Apps).Count().IsEqualTo(15);
        await Assert.That(plan.Linux).Count().IsEqualTo(15);
        await Assert.That(plan.Android).Count().IsEqualTo(4);
        await Assert.That(plan.Windows).Count().IsEqualTo(0);
        await Assert.That(plan.Summary.EstimatedCheckouts).IsEqualTo(20);
        await Assert.That(plan.Summary.EstimatedWorkloadInstalls).IsEqualTo(4);
    }

    [Test]
    public async Task DocumentationOnlyChangesProduceNoRows()
    {
        var plan = Program.CreateCiMatrix(
            LoadManifest(),
            ["docs/ci.md"],
            forceAll: false,
            fullCoverage: false);

        await Assert.That(plan.Any).IsFalse();
        await Assert.That(plan.SkipBuild).IsTrue();
        await Assert.That(plan.Summary.SelectedApps).IsEqualTo(0);
    }

    [Test]
    public async Task FastRootCoverageChoosesOneAppPerStack()
    {
        var plan = Program.CreateCiMatrix(
            LoadManifest(),
            [".github/workflows/merge.yml"],
            forceAll: false,
            fullCoverage: false);

        await Assert.That(plan.Summary.SelectedApps).IsEqualTo(3);
        await Assert.That(plan.Linux).Count().IsEqualTo(3);
        await Assert.That(plan.Android).Count().IsEqualTo(2);
        await Assert.That(plan.Linux.Select(row => row.Stack).Contains("avalonia-desktop")).IsTrue();
        await Assert.That(plan.Linux.Select(row => row.Stack).Contains("avalonia-mobile")).IsTrue();
        await Assert.That(plan.Linux.Select(row => row.Stack).Contains("maui")).IsTrue();
    }

    [Test]
    public async Task FullRootCoverageRetainsAllApps()
    {
        var plan = Program.CreateCiMatrix(
            LoadManifest(),
            [".github/workflows/merge.yml"],
            forceAll: false,
            fullCoverage: true);

        await Assert.That(plan.Summary.SelectedApps).IsEqualTo(15);
        await Assert.That(plan.Linux).Count().IsEqualTo(15);
        await Assert.That(plan.Android).Count().IsEqualTo(4);
    }

    [Test]
    public async Task AndroidRowsUseCanonicalAndroidProject()
    {
        var document = LoadManifest();
        var plan = Program.CreateCiMatrix(document, [], forceAll: false, fullCoverage: false);
        var readAloud = plan.Android.Single(row => row.Key == "read-aloud");

        await Assert.That(readAloud.Project)
            .IsEqualTo("src/ReadAloud/ReadAloud.Android/ReadAloud.Android.csproj");
        await Assert.That(readAloud.IsMaui).IsFalse();
    }

    [Test]
    public async Task WindowsRowsRequireExplicitWindowsValidation()
    {
        var document = new AppsManifestDocument
        {
            Apps =
            [
                new AppEntry
                {
                    Key = "windows-sample",
                    Choice = "WindowsSample",
                    Stack = "avalonia-desktop",
                    Projects = new ProjectSet
                    {
                        PublishWindows = "src/WindowsSample/WindowsSample.csproj",
                    },
                    Validation = new ValidationConfig
                    {
                        LinuxCi = false,
                        WindowsCi = true,
                    },
                },
            ],
        };

        var plan = Program.CreateCiMatrix(document, [], forceAll: false, fullCoverage: false);

        await Assert.That(plan.Windows).Count().IsEqualTo(1);
        await Assert.That(plan.Windows[0].Project)
            .IsEqualTo("src/WindowsSample/WindowsSample.csproj");
    }

    [Test]
    public async Task GeneratedLinuxSolutionsExcludePlatformHeads()
    {
        var root = FindRepoRoot();
        var booksLinux = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BooksMobile",
            "BooksMobile.Linux.slnx"));
        var merglyphLinux = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Merglyph",
            "Merglyph.Linux.slnx"));

        await Assert.That(booksLinux).DoesNotContain(".Android.csproj");
        await Assert.That(merglyphLinux).DoesNotContain("Merglyph/Merglyph.csproj");
    }

    private static AppsManifestDocument LoadManifest() =>
        Program.Load(Path.Combine(FindRepoRoot(), "build", "apps.json"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "build", "apps.json")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find novolis-apps repository root.");
    }
}
