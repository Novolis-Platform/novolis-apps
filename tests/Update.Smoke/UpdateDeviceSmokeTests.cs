using Novolis.Registry.Primitives.Updates;
using Novolis.Registry.Updates;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Windows;
using TUnit.Core;

namespace Novolis.Apps.Update.Smoke;

/// <summary>
/// Gated Appium smoke coverage for direct-release product builds. CI remains
/// deterministic until the corresponding environment variables are supplied.
/// </summary>
public sealed class UpdateDeviceSmokeTests
{
    [Test]
    public async Task Direct_release_contract_has_sideload_targets_only()
    {
        var manifest = new UpdateManifest
        {
            AppId = "Novolis.Smoke",
            DisplayName = "Update smoke",
            Version = "2026.1.2.0",
            PublishedAt = DateTimeOffset.UtcNow,
            Provenance = new UpdateProvenance
            {
                AppId = "Novolis.Smoke",
                Repository = new Uri("https://github.com/Novolis-Platform/novolis-apps"),
                Tag = "v2026.1.2.0",
                ReleaseUri = new Uri(
                    "https://github.com/Novolis-Platform/novolis-apps/releases/tag/v2026.1.2.0"),
            },
            Artifacts =
            [
                Artifact(UpdateArtifactKind.WindowsInstaller, UpdatePlatform.Windows, "setup.exe"),
                Artifact(UpdateArtifactKind.WindowsPortable, UpdatePlatform.Windows, "portable.zip"),
                Artifact(UpdateArtifactKind.LinuxTarGz, UpdatePlatform.Linux, "client.tar.gz"),
                Artifact(UpdateArtifactKind.AndroidApk, UpdatePlatform.Android, "client.apk"),
            ],
        };

        await Assert.That(UpdateManifestValidator.Validate(manifest)).IsEmpty();
        await Assert.That(manifest.Artifacts.Any(
                artifact => artifact.Kind == UpdateArtifactKind.AndroidApk))
            .IsTrue();
        await Assert.That(manifest.DistributionMode)
            .IsEqualTo(UpdateDistributionMode.DirectGithub);
    }

    [Test]
    public async Task Windows_update_surface_is_visible_when_device_gate_is_enabled()
    {
        Skip.Unless(
            IsEnabled("NOVOLIS_UPDATE_WINDOWS_SMOKE"),
            "Set NOVOLIS_UPDATE_WINDOWS_SMOKE=1 to run Windows Appium smoke.");
        using var driver = StartWindows(RequiredPath("NOVOLIS_UPDATE_WINDOWS_APP"));
        var status = driver.FindElement(MobileBy.AccessibilityId("UpdateStatusView"));
        await Assert.That(status.Displayed).IsTrue();
    }

    [Test]
    public async Task Maui_windows_update_surface_is_visible_when_device_gate_is_enabled()
    {
        Skip.Unless(
            IsEnabled("NOVOLIS_UPDATE_MAUI_WINDOWS_SMOKE"),
            "Set NOVOLIS_UPDATE_MAUI_WINDOWS_SMOKE=1 to run MAUI Windows Appium smoke.");
        using var driver = StartWindows(RequiredPath("NOVOLIS_UPDATE_MAUI_WINDOWS_APP"));
        var status = driver.FindElement(MobileBy.AccessibilityId("UpdateStatusView"));
        await Assert.That(status.Displayed).IsTrue();
    }

    [Test]
    public async Task Android_sideload_update_surface_is_visible_when_device_gate_is_enabled()
    {
        Skip.Unless(
            IsEnabled("NOVOLIS_UPDATE_ANDROID_SMOKE"),
            "Set NOVOLIS_UPDATE_ANDROID_SMOKE=1 to run Android sideload smoke.");
        var apk = RequiredPath("NOVOLIS_UPDATE_ANDROID_APK");
        using var driver = StartAndroid(
            apk,
            Environment.GetEnvironmentVariable("NOVOLIS_UPDATE_ANDROID_PACKAGE")
            ?? throw new InvalidOperationException(
                "Set NOVOLIS_UPDATE_ANDROID_PACKAGE for Android smoke."));
        var status = driver.FindElement(MobileBy.Id("UpdateStatusView"));
        await Assert.That(status.Displayed).IsTrue();
    }

    [Test]
    public async Task Store_managed_build_does_not_contact_direct_release_source()
    {
        var source = new CountingSource();
        await using var coordinator = new UpdateCoordinator(
            new UpdateRequest
            {
                AppId = "Novolis.StoreApp",
                Repository = new Uri(
                    "https://github.com/Novolis-Platform/novolis-apps"),
                CurrentVersion = "2026.1.1.0",
                Platform = UpdatePlatform.Windows,
                ArtifactKind = UpdateArtifactKind.WindowsInstaller,
                DistributionMode = UpdateDistributionMode.StoreManaged,
            },
            source,
            new EmptyStateStore(),
            new UnusedDownloader());

        var snapshot = await coordinator.CheckAsync(force: true);

        await Assert.That(snapshot.ErrorCode).IsEqualTo("store-managed");
        await Assert.That(source.Calls).IsEqualTo(0);
    }

    private static UpdateArtifact Artifact(
        UpdateArtifactKind kind,
        UpdatePlatform platform,
        string name) =>
        new()
        {
            Name = name,
            Kind = kind,
            Target = new UpdateTarget
            {
                Platform = platform,
                RuntimeIdentifier = platform switch
                {
                    UpdatePlatform.Windows => "win-x64",
                    UpdatePlatform.Linux => "linux-x64",
                    _ => null,
                },
                Architecture = platform == UpdatePlatform.Android ? null : "x64",
            },
            DownloadUri = new Uri($"https://github.com/Novolis-Platform/novolis-apps/{name}"),
            Length = 1,
            Sha256 = new string('a', 64),
        };

    private static WindowsDriver StartWindows(string app) =>
        new(new AppiumOptions
        {
            PlatformName = "Windows",
            AutomationName = "windows",
            App = app,
        });

    private static AndroidDriver StartAndroid(string apk, string packageName)
    {
        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UIAutomator2",
            App = apk,
        };
        options.AddAdditionalAppiumOption("appPackage", packageName);
        options.AddAdditionalAppiumOption(
            "appActivity",
            Environment.GetEnvironmentVariable("NOVOLIS_UPDATE_ANDROID_ACTIVITY")
            ?? $"{packageName}.MainActivity");
        return new AndroidDriver(options);
    }

    private static bool IsEnabled(string name) =>
        string.Equals(
            Environment.GetEnvironmentVariable(name),
            "1",
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            Environment.GetEnvironmentVariable(name),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static string RequiredPath(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Set {name} for gated update smoke.");
        var path = Path.GetFullPath(value);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"Update smoke app does not exist: {path}", path);
    }

    private sealed class CountingSource : IAutoUpdateSource
    {
        public int Calls { get; private set; }

        public ValueTask<UpdateSourceResult> CheckAsync(
            UpdateRequest request,
            UpdateSourceCheckpoint checkpoint,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("The direct source must not be contacted.");
        }
    }

    private sealed class EmptyStateStore : IUpdateStateStore
    {
        public ValueTask<UpdatePersistedState?> LoadAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<UpdatePersistedState?>(null);

        public ValueTask SaveAsync(
            UpdatePersistedState state,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class UnusedDownloader : IArtifactDownloader
    {
        public ValueTask<DownloadedUpdateArtifact> DownloadAsync(
            UpdateReleaseCandidate candidate,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A store-managed build must not download.");
    }
}
