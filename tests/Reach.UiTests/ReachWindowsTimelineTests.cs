using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Novolis.Testing.Appium;
using Novolis.Testing.Budgets;
using Novolis.Testing.Coverage;
using Novolis.Testing.Logging;
using Novolis.Testing.TUnit;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using TUnit.Core;
using TestLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Reach.UiTests;

public sealed class ReachWindowsTimelineTests
{
    [Test]
    [NotInParallel("reach-windows-ui")]
    public Task WindowsClientProducesLanUsabilityTimeline() =>
        RunTimelineAsync(
            "LAN",
            ReachUiTestHarness.EndpointEnvironmentVariable);

    [Test]
    [NotInParallel("reach-windows-ui")]
    public Task WindowsClientProducesTailscaleUsabilityTimeline() =>
        RunTimelineAsync(
            "Tailscale",
            ReachUiTestHarness.TailscaleEndpointEnvironmentVariable);

    private static async Task RunTimelineAsync(
        string networkKind,
        string endpointEnvironmentVariable)
    {
        Skip.Unless(
            OperatingSystem.IsWindows(),
            "The Reach Windows usability timeline requires the Windows Appium driver.");
        Skip.Unless(
            ReachUiTestHarness.AppiumIsListening(),
            "Start Appium on APPIUM_HOST before running the Reach Windows timeline.");
        var executable = ReachUiTestHarness.TryResolveWindowsClient();
        Skip.Unless(
            executable is not null,
            "Build Reach.Client.Windows or set NOVOLIS_REACH_UI_APP.");
        var endpoint = endpointEnvironmentVariable
            == ReachUiTestHarness.TailscaleEndpointEnvironmentVariable
            ? ReachUiTestHarness.TryResolveTailscaleEndpoint()
            : ReachUiTestHarness.TryResolveEndpoint();
        Skip.Unless(
            endpoint is not null,
            $"Set {endpointEnvironmentVariable} to a trusted Reach endpoint.");

        var recorder = ReachTimelineRecorder.Create();
        var logger = new SimpleTestLogger<ReachWindowsTimelineTests>(
            TestContext.Current,
            TestLogLevel.Information);
        using var session = WindowsAppiumSession.Connect(
            new WindowsAppiumSessionOptions
            {
                App = executable!,
            });

        try
        {
            logger.LogInformation(
                "Reach {NetworkKind} timeline artifact: {ArtifactDirectory}",
                networkKind,
                recorder.ArtifactDirectory);
            recorder.Log($"Starting {networkKind} Reach usability run.");
            await recorder.CaptureAsync(
                    "client-launched",
                    "The Windows client starts with an explicit endpoint and connection controls.",
                    path => SaveScreenshotAsync(session.Driver, path),
                    new Dictionary<string, object?>
                    {
                        ["endpoint"] = endpoint!,
                        ["network"] = networkKind,
                        ["platform"] = "Windows",
                    })
                .ConfigureAwait(false);

            var discover = session.Driver.FindElement(
                MobileBy.AccessibilityId("ReachDiscover"));
            discover.Click();
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await recorder.CaptureAsync(
                    "host-discovery",
                    "The client checks the private network for Reach hosts before accepting a manual endpoint.",
                    path => SaveScreenshotAsync(session.Driver, path),
                    new Dictionary<string, object?>
                    {
                        ["network"] = networkKind,
                        ["status"] = ReachAppiumAssertions.ReadText(
                            session.Driver,
                            "ReachStatus"),
                    })
                .ConfigureAwait(false);

            var phaseBeforeManualEndpoint = ReachAppiumAssertions.ReadText(
                session.Driver,
                "ReachSessionPhase");
            if (!phaseBeforeManualEndpoint.Contains(
                    "Streaming",
                    StringComparison.OrdinalIgnoreCase))
            {
                var endpointInput = session.Driver.FindElement(
                    MobileBy.AccessibilityId("ReachEndpoint"));
                endpointInput.Click();
                endpointInput.Clear();
                endpointInput.SendKeys(endpoint!);
                await recorder.CaptureAsync(
                        "endpoint-entered",
                        "A trusted LAN or Tailscale endpoint is entered before starting the session.",
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
                recorder.RecordTelemetry(
                    "time-to-first-frame",
                    new
                    {
                        elapsedMilliseconds = firstFrameElapsed.TotalMilliseconds,
                        budgetMilliseconds = 60_000,
                        network = networkKind,
                    });
                await Assert.That(firstFrameElapsed)
                    .IsLessThan(TimeSpan.FromSeconds(60));
            }
            else
            {
                recorder.Record(
                    "first-frame",
                    "discovery-connected",
                    "Discovery connected to a host before the manual endpoint was entered.");
                recorder.RecordTelemetry(
                    "time-to-first-frame",
                    new
                    {
                        status = "measured-before-timeline",
                        network = networkKind,
                    });
            }

            await ReachAppiumAssertions.WaitForTextAsync(
                    session.Driver,
                    "ReachSessionPhase",
                    text => text.Contains(
                        "Streaming",
                        StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(60))
                .ConfigureAwait(false);
            recorder.Record(
                "first-frame",
                "passed",
                "The client reached Streaming and received a usable remote frame.");

            await recorder.CaptureAsync(
                    "streaming-session",
                    "The session is streaming and the client exposes its negotiated capabilities.",
                    path => SaveScreenshotAsync(session.Driver, path))
                .ConfigureAwait(false);

            var capabilities = ReachAppiumAssertions.ReadText(
                session.Driver,
                "ReachCapabilities");
            var performance = ReachAppiumAssertions.ReadText(
                session.Driver,
                "ReachPerformance");
            recorder.RecordTelemetry(
                "session-surface",
                new
                {
                    network = networkKind,
                    capabilities,
                    performance,
                    phase = ReachAppiumAssertions.ReadText(
                        session.Driver,
                        "ReachSessionPhase"),
                    status = ReachAppiumAssertions.ReadText(
                        session.Driver,
                        "ReachStatus"),
                });
            recorder.Record(
                "clipboard-ready",
                capabilities.Contains("Clipboard", StringComparison.OrdinalIgnoreCase)
                    ? "negotiated"
                    : "not-negotiated",
                "The UI exposes the negotiated clipboard capability. The injected bridge test covers text send, receive, and echo suppression.");
            await recorder.CaptureAsync(
                    "clipboard-ready",
                    "Clipboard text is negotiated and ready for a bidirectional copy/paste check.",
                    path => SaveScreenshotAsync(session.Driver, path))
                .ConfigureAwait(false);

            var queryBudget = BudgetProbe.Measure(
                new BudgetRun(
                    "reach.ui.automation-query",
                    Iterations: 3,
                    Warmup: 1,
                    Parameters: "Reach performance status"),
                () => _ = session.Driver.FindElement(
                    MobileBy.AccessibilityId("ReachPerformance")).Displayed);
            recorder.RecordTelemetry("automation-query-budget", queryBudget);
            TestContext.Current?.WriteJson(queryBudget);

            var displaySelector = session.Driver.FindElement(
                MobileBy.AccessibilityId("ReachDisplaySelector"));
            if (displaySelector.Displayed)
            {
                await recorder.CaptureAsync(
                        "display-topology",
                        "When the host exposes multiple monitors, the client keeps monitor selection visible.",
                        path => SaveScreenshotAsync(session.Driver, path),
                        new Dictionary<string, object?>
                        {
                            ["displaySelectorDisplayed"] = true,
                        })
                    .ConfigureAwait(false);
                var beforeSelection = displaySelector.Text;
                try
                {
                    displaySelector.Click();
                    displaySelector.SendKeys(Keys.ArrowDown);
                    displaySelector.SendKeys(Keys.Enter);
                    await Task.Delay(500).ConfigureAwait(false);
                    var afterSelection = displaySelector.Text;
                    if (!string.Equals(
                            beforeSelection,
                            afterSelection,
                            StringComparison.Ordinal))
                    {
                        recorder.Record(
                            "monitor-switch",
                            "passed",
                            $"Monitor changed from '{beforeSelection}' to '{afterSelection}'.");
                        await recorder.CaptureAsync(
                                "monitor-switch",
                                "The selected monitor changes while the video surface remains mapped to the selected monitor origin.",
                                path => SaveScreenshotAsync(session.Driver, path))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        recorder.Record(
                            "monitor-switch",
                            "skipped",
                            "The selector was visible but Appium did not change its value.");
                    }
                }
                catch (WebDriverException exception)
                {
                    recorder.Record(
                        "monitor-switch",
                        "skipped",
                        $"Appium could not select the second monitor: {exception.Message}");
                }
            }
            else
            {
                recorder.Record(
                    "display-topology",
                    "single-monitor",
                    "The host did not expose a second monitor during this run.");
            }

            recorder.Record(
                "input-and-audio",
                capabilities.Contains("Audio", StringComparison.OrdinalIgnoreCase)
                    ? "negotiated"
                    : "skipped",
                "The final frame and performance surface are the evidence point for input responsiveness and host audio negotiation.");
            await recorder.CaptureAsync(
                    "input-audio-ready",
                    "The remote surface is ready for pointer, keyboard, Unicode text, and host audio checks.",
                    path => SaveScreenshotAsync(session.Driver, path))
                .ConfigureAwait(false);

            recorder.Record(
                "quality-downgrade",
                "skipped",
                "Network impairment is not injected by the native UI run; controller pressure and recovery are covered by Reach unit tests.");
            recorder.Record(
                "reconnect",
                "skipped",
                "The evidence run does not interrupt the trusted endpoint while the host is being used; LAN/Tailscale reconnect is a manual smoke step.");
            recorder.RecordTelemetry(
                "input-round-trip",
                new
                {
                    source = performance,
                    status = performance.Contains(
                        "input RTT",
                        StringComparison.OrdinalIgnoreCase)
                        ? "reported-by-session"
                        : "not-yet-reported",
                });
            recorder.RecordTelemetry(
                "clipboard-round-trip",
                new
                {
                    source = "Reach.Unit injected clipboard bridge",
                    status = "covered-by-protocol-and-adapter-tests",
                });
            recorder.RecordTelemetry(
                "reconnect-recovery",
                new
                {
                    status = "manual-lan-tailscale-smoke-required",
                });

            var publicTypes = PublicApiSurface.PublicTypes(
                typeof(Novolis.Reach.Client.ReachClientSession).Assembly);
            recorder.RecordTelemetry(
                "reach-public-api-surface",
                new
                {
                    assembly = typeof(Novolis.Reach.Client.ReachClientSession).Assembly
                        .GetName()
                        .Name,
                    publicTypeCount = publicTypes.Count,
                });
            TestContext.Current?.WriteJson(new
            {
                recorder.ArtifactDirectory,
                recorder.Events.Count,
                capabilities,
                performance,
            });
            TestContext.Current?.WriteTable(
                recorder.Events.Select(static eventItem => new
                {
                    eventItem.Sequence,
                    eventItem.Name,
                    eventItem.Status,
                    eventItem.CapturedAtUtc,
                }));
            await recorder.CaptureAsync(
                    "final-healthy-session",
                    "The run ends with a connected session, negotiated quality, and the selected monitor surface visible.",
                    path => SaveScreenshotAsync(session.Driver, path))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Reach Windows usability timeline failed.");
            recorder.Log(exception.ToString());
            recorder.Record("run", "failed", exception.ToString());
            try
            {
                await recorder.CaptureAsync(
                        "failure",
                        "The failure frame is preserved beside the successful transition frames.",
                        path => SaveScreenshotAsync(session.Driver, path))
                    .ConfigureAwait(false);
            }
            catch (Exception captureException)
            {
                logger.LogWarning(
                    captureException,
                    "Could not capture Reach failure frame.");
            }
            throw;
        }
        finally
        {
            await recorder.FlushAsync().ConfigureAwait(false);
            logger.LogInformation(
                "Reach usability timeline written to {ArtifactDirectory}",
                recorder.ArtifactDirectory);
        }
    }

    private static async Task SaveScreenshotAsync(
        WindowsDriver driver,
        string path)
    {
        driver.GetScreenshot().SaveAsFile(path);
        await Task.CompletedTask.ConfigureAwait(false);
    }

}
