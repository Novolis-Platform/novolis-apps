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
        await Assert.That(plan.Linux).Count().IsEqualTo(12);
        await Assert.That(plan.Android).Count().IsEqualTo(7);
        await Assert.That(plan.Windows).Count().IsEqualTo(5);
        await Assert.That(plan.Summary.EstimatedCheckouts).IsEqualTo(25);
        await Assert.That(plan.Summary.EstimatedWorkloadInstalls).IsEqualTo(7);
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
        await Assert.That(plan.Linux).Count().IsEqualTo(12);
        await Assert.That(plan.Android).Count().IsEqualTo(7);
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
    public async Task ReadAloudEnablesGooglePlayDeliveryWithoutChangingGitHubChannels()
    {
        var readAloud = LoadManifest().Apps.Single(app => app.Key == "read-aloud");

        await Assert.That(readAloud.Ship).Contains("android-apk");
        await Assert.That(readAloud.Release?.GithubRelease).IsTrue();
        await Assert.That(readAloud.Release?.GooglePlay?.Enabled).IsTrue();
        await Assert.That(readAloud.Android?.ApplicationId)
            .IsEqualTo("com.novolis.readaloud");
    }

    [Test]
    public async Task BrandingAssetsExistAtRepoRoot()
    {
        var root = FindRepoRoot();
        await Assert.That(File.Exists(Path.Combine(root, "icon.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, "icon.ico"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, "logo-icon.svg"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, "brand", "android", "ic_launcher.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, "src", "Merglyph", "Merglyph", "Resources", "AppIcon", "appicon.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, "src", "Merglyph", "Merglyph", "Resources", "AppIcon", "appiconfg.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, "src", "Merglyph", "Merglyph", "Resources", "Splash", "splash.png"))).IsTrue();
    }

    [Test]
    public async Task NovolisPdfReaderShipsWindowsAndAndroid()
    {
        var reader = LoadManifest().Apps.Single(app => app.Key == "novolis-pdf-reader");
        await Assert.That(reader.Stack).IsEqualTo("maui");
        await Assert.That(reader.Ship.Contains("windows-inno")).IsTrue();
        await Assert.That(reader.Ship.Contains("android-apk")).IsTrue();
        await Assert.That(reader.Android?.ApplicationId).IsEqualTo("com.novolis.pdfreader");
        await Assert.That(reader.Data?.AppDataRoot).Contains("pdf-reader");
        await Assert.That(reader.Windows?.FileAssociationsAllowed).IsTrue();
    }

    [Test]
    public async Task MerglyphShipsWindowsAndAndroid()
    {
        var merglyph = LoadManifest().Apps.Single(app => app.Key == "merglyph");
        await Assert.That(merglyph.Stack).IsEqualTo("maui");
        await Assert.That(merglyph.Ship.Contains("windows-inno")).IsTrue();
        await Assert.That(merglyph.Ship.Contains("android-apk")).IsTrue();
        await Assert.That(merglyph.Projects.PublishWindows).IsEqualTo("src/Merglyph/Merglyph/Merglyph.csproj");
        await Assert.That(merglyph.Projects.Maui).IsEqualTo("src/Merglyph/Merglyph/Merglyph.csproj");
        await Assert.That(merglyph.Windows?.ExeName).IsEqualTo("Merglyph.exe");
        await Assert.That(merglyph.Windows?.FileAssociationsAllowed).IsTrue();
    }

    [Test]
    public async Task ReachShipsAllDayOneClientChannels()
    {
        var reach = LoadManifest().Apps.Single(app => app.Key == "reach");

        await Assert.That(reach.Ship.Contains("windows-inno")).IsTrue();
        await Assert.That(reach.Ship.Contains("linux-tar")).IsTrue();
        await Assert.That(reach.Ship.Contains("android-apk")).IsTrue();
        await Assert.That(reach.Linux?.Project)
            .IsEqualTo("src/Reach/Reach.Client.Linux/Reach.Client.Linux.csproj");
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
    public async Task EveryProductTreeHasAManifestRow()
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src");
        var document = LoadManifest();
        var missing = Directory.GetDirectories(src)
            .Where(dir => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any())
            .Select(Path.GetFileName)
            .Where(name => document.Apps.All(app =>
                !string.Equals(app.SourceRoot, $"src/{name}", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        await Assert.That(missing).IsEmpty();
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

        var pdfLinux = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NovolisPdfReader",
            "NovolisPdfReader.Linux.slnx"));

        await Assert.That(booksLinux).DoesNotContain(".Android.csproj");
        await Assert.That(merglyphLinux).DoesNotContain("Merglyph/Merglyph.csproj");
        await Assert.That(pdfLinux).DoesNotContain("NovolisPdfReader.csproj");
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
