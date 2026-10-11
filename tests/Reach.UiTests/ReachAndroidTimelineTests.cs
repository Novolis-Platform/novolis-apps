using System.Diagnostics;
using Novolis.Testing.Appium;
using Novolis.Testing.Logging;
using Novolis.Testing.TUnit;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using TUnit.Core;
using TestLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Reach.UiTests;

public sealed class ReachAndroidTimelineTests
{
    [Test]
    [NotInParallel("reach-android-ui")]
    public async Task AndroidClientProducesWindowsInteropEvidence()
    {
        Skip.Unless(
            ReachUiTestHarness.AppiumIsListening(),
            "Start Appium on APPIUM_HOST before running the Reach Android timeline.");
        var apk = ReachUiTestHarness.TryResolveAndroidApk();
        Skip.Unless(
            apk is not null,
            "Set NOVOLIS_REACH_UI_ANDROID_APK to a built Reach Android APK.");

        var recorder = ReachTimelineRecorder.Create();
        var logger = new SimpleTestLogger<ReachAndroidTimelineTests>(
            TestContext.Current,
            TestLogLevel.Information);
        using var session = AndroidAppiumSession.Connect(
            new AndroidAppiumSessionOptions
            {
                AppPath = apk!,
                NoReset = true,
            });

        try
        {
            recorder.Log("Starting Android Reach launch evidence.");
            await recorder.CaptureAsync(
                    "android-client-launched",
                    "The Android client launches through UiAutomator2 with the same stable Reach automation surface.",
                    path => SaveScreenshotAsync(session.Driver, path),
                    new Dictionary<string, object?>
                    {
                        ["platform"] = "Android",
                        ["apk"] = apk!,
                    })
                .ConfigureAwait(false);

            var endpoint = ReachUiTestHarness.TryResolveAndroidEndpoint();
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                var endpointInput = session.Driver.FindElement(
                    MobileBy.AccessibilityId("ReachEndpoint"));
                endpointInput.Click();
                endpointInput.Clear();
                endpointInput.SendKeys(endpoint);
                await recorder.CaptureAsync(
                        "android-endpoint-entered",
                        "The Android controller accepts the same trusted private-network endpoint.",
                        path => SaveScreenshotAsync(session.Driver, path))
                    .ConfigureAwait(false);

                var connect = session.Driver.FindElement(
                    MobileBy.AccessibilityId("ReachConnect"));
                await Assert.That(connect.Enabled).IsTrue();
                var firstFrameStart = Stopwatch.GetTimestamp();
                connect.Click();
                await ReachAppiumAssertions.WaitForTextAsync(
                        session.Driver,
                        "ReachSessionPhase",
                        text => text.Contains(
                            "Streaming",
                            StringComparison.OrdinalIgnoreCase),
                        TimeSpan.FromSeconds(60))
                    .ConfigureAwait(false);
                var firstFrameElapsed = Stopwatch.GetElapsedTime(firstFrameStart);
                recorder.Record(
                    "android-to-windows",
                    "passed",
                    "The Android client reached a streaming session on the configured Windows host.");
                recorder.RecordTelemetry(
                    "android-to-windows-first-frame",
                    new
                    {
                        elapsedMilliseconds = firstFrameElapsed.TotalMilliseconds,
                        budgetMilliseconds = 60_000,
                        endpoint,
                    });
                await Assert.That(firstFrameElapsed)
                    .IsLessThan(TimeSpan.FromSeconds(60));
                await recorder.CaptureAsync(
                        "android-streaming-session",
                        "Android is receiving the Windows host session and exposes the negotiated client surface.",
                        path => SaveScreenshotAsync(session.Driver, path),
                        new Dictionary<string, object?>
                        {
                            ["platform"] = "Android",
                            ["phase"] = ReachAppiumAssertions.ReadText(
                                session.Driver,
                                "ReachSessionPhase"),
                            ["capabilities"] = ReachAppiumAssertions.ReadText(
                                session.Driver,
                                "ReachCapabilities"),
                        })
                    .ConfigureAwait(false);

                var capabilities = ReachAppiumAssertions.ReadText(
                    session.Driver,
                    "ReachCapabilities");
                recorder.Record(
                    "android-capabilities",
                    capabilities.Contains(
                        "Clipboard",
                        StringComparison.OrdinalIgnoreCase)
                        ? "passed"
                        : "not-negotiated",
                    "Android reports the capabilities negotiated with the Windows host.");
                TestContext.Current?.WriteJson(new
                {
                    recorder.ArtifactDirectory,
                    platform = "Android",
                    endpoint,
                    firstFrameElapsed.TotalMilliseconds,
                    capabilities,
                });
            }
            else
            {
                recorder.Record(
                    "android-endpoint",
                    "skipped",
                    "Set NOVOLIS_REACH_UI_ENDPOINT to capture Android endpoint-entry evidence.");
            }

            TestContext.Current?.WriteJson(new
            {
                recorder.ArtifactDirectory,
                platform = "Android",
                apk,
            });
            TestContext.Current?.WriteTable(
                recorder.Events.Select(static eventItem => new
                {
                    eventItem.Sequence,
                    eventItem.Name,
                    eventItem.Status,
                    eventItem.CapturedAtUtc,
                }));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Reach Android timeline failed.");
            recorder.Log(exception.ToString());
            recorder.Record("run", "failed", exception.ToString());
            try
            {
                await recorder.CaptureAsync(
                        "android-failure",
                        "The Android failure frame is preserved for diagnosis.",
                        path => SaveScreenshotAsync(session.Driver, path))
                    .ConfigureAwait(false);
            }
            catch (Exception captureException)
            {
                logger.LogWarning(
                    captureException,
                    "Could not capture the Android failure frame.");
            }

            throw;
        }
        finally
        {
            await recorder.FlushAsync().ConfigureAwait(false);
            logger.LogInformation(
                "Reach Android timeline written to {ArtifactDirectory}",
                recorder.ArtifactDirectory);
        }
    }

    private static async Task SaveScreenshotAsync(
        AndroidDriver driver,
        string path)
    {
        driver.GetScreenshot().SaveAsFile(path);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
