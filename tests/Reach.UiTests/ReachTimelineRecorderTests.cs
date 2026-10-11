using System.Net;
using TUnit.Core;

namespace Reach.UiTests;

public sealed class ReachTimelineRecorderTests
{
    [Test]
    public async Task RecorderWritesImageBackedTimelineArtifacts()
    {
        var recorder = ReachTimelineRecorder.Create(
            "Reach timeline renderer fixture");
        await recorder.CaptureAsync(
                "client-launched",
                "The deterministic fixture shows the client launch state before a host connection.",
                path => WriteFixtureFrameAsync(
                    path,
                    "Reach client",
                    "Client launched",
                    "Trusted endpoint controls are visible.",
                    "#6fe4df"),
                new Dictionary<string, object?>
                {
                    ["network"] = "LAN",
                    ["platform"] = "Windows",
                },
                frameExtension: "svg")
            .ConfigureAwait(false);
        recorder.Record(
            "client-launched",
            "passed",
            "The fixture captured a readable launch state.");
        recorder.RecordTelemetry(
            "time-to-first-frame",
            new
            {
                elapsedMilliseconds = 842,
                budgetMilliseconds = 60_000,
                source = "deterministic fixture",
            });
        recorder.Log("Fixture started with a trusted LAN endpoint.");

        await recorder.CaptureAsync(
                "connecting",
                "The controller is connecting to the selected private-network endpoint.",
                path => WriteFixtureFrameAsync(
                    path,
                    "Reach client",
                    "Connecting",
                    "Negotiating video, input, clipboard, and display capabilities.",
                    "#f0c36d"),
                frameExtension: "svg")
            .ConfigureAwait(false);
        recorder.Record(
            "connecting",
            "passed",
            "The connection state is visible instead of appearing stalled.");
        recorder.Log("Connection negotiation entered the streaming path.");

        await recorder.CaptureAsync(
                "streaming-session",
                "The remote session is streaming with negotiated quality and input readiness.",
                path => WriteFixtureFrameAsync(
                    path,
                    "Reach client",
                    "Streaming",
                    "1280 × 720 · H.264 · input RTT 18 ms",
                    "#83df9c"),
                new Dictionary<string, object?>
                {
                    ["capabilities"] = "Clipboard, Audio, Display topology",
                    ["monitor"] = "Primary",
                },
                frameExtension: "svg")
            .ConfigureAwait(false);
        recorder.Record(
            "first-frame",
            "passed",
            "The client reached Streaming and received a usable remote frame.");
        recorder.RecordTelemetry(
            "session-surface",
            new
            {
                phase = "Streaming",
                quality = "balanced",
                monitor = "Primary",
            });

        await recorder.CaptureAsync(
                "clipboard-display-ready",
                "Clipboard and monitor controls are visible for the usability handoff.",
                path => WriteFixtureFrameAsync(
                    path,
                    "Reach client",
                    "Ready",
                    "Clipboard synced · Monitor 1 selected · Audio negotiated",
                    "#b8a2ff"),
                frameExtension: "svg")
            .ConfigureAwait(false);
        recorder.Record(
            "clipboard-ready",
            "negotiated",
            "The fixture exposes the negotiated clipboard capability.");
        recorder.Record(
            "monitor-switch",
            "single-monitor",
            "The fixture represents a host with one monitor.");
        recorder.Record(
            "reconnect",
            "skipped",
            "Reconnect is covered by the protocol and session test suites.");
        recorder.Log("Fixture completed with clipboard and monitor evidence.");
        await recorder.FlushAsync().ConfigureAwait(false);

        await Assert.That(
                File.Exists(Path.Combine(recorder.ArtifactDirectory, "timeline.json")))
            .IsTrue();
        await Assert.That(
                File.Exists(Path.Combine(recorder.ArtifactDirectory, "timeline.md")))
            .IsTrue();
        await Assert.That(
                File.Exists(Path.Combine(recorder.ArtifactDirectory, "timeline.html")))
            .IsTrue();
        await Assert.That(
                File.Exists(Path.Combine(
                    recorder.ArtifactDirectory,
                    "frames",
                    "01-client-launched.svg")))
            .IsTrue();
        var html = await File.ReadAllTextAsync(
                Path.Combine(recorder.ArtifactDirectory, "timeline.html"))
            .ConfigureAwait(false);
        await Assert.That(html).Contains("All events (");
        await Assert.That(html).Contains("time-to-first-frame");
        await Assert.That(html).Contains("reach-ui.log");
        await Assert.That(html).Contains("data:image/svg+xml;base64,");
        await Assert.That(html).Contains("Streaming");
    }

    private static Task WriteFixtureFrameAsync(
        string path,
        string title,
        string state,
        string detail,
        string accent)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="1440" height="900" viewBox="0 0 1440 900">
              <rect width="1440" height="900" fill="#07131c"/>
              <rect x="42" y="42" width="1356" height="816" rx="18" fill="#0d202b" stroke="#28566a" stroke-width="2"/>
              <rect x="42" y="42" width="1356" height="84" rx="18" fill="#12303d"/>
              <circle cx="92" cy="84" r="14" fill="{{accent}}"/>
              <text x="125" y="93" fill="#eef8fb" font-family="Segoe UI, sans-serif" font-size="30" font-weight="600">{{Encode(title)}}</text>
              <text x="108" y="220" fill="#a8c3cc" font-family="Segoe UI, sans-serif" font-size="24">Session state</text>
              <text x="108" y="300" fill="{{accent}}" font-family="Segoe UI, sans-serif" font-size="64" font-weight="700">{{Encode(state)}}</text>
              <rect x="108" y="350" width="1224" height="2" fill="#28566a"/>
              <text x="108" y="430" fill="#eef8fb" font-family="Segoe UI, sans-serif" font-size="30">{{Encode(detail)}}</text>
              <rect x="108" y="520" width="360" height="112" rx="12" fill="#12303d"/>
              <text x="138" y="565" fill="#a8c3cc" font-family="Segoe UI, sans-serif" font-size="20">Endpoint</text>
              <text x="138" y="605" fill="#eef8fb" font-family="Segoe UI, sans-serif" font-size="25">100.64.0.12:4827</text>
              <rect x="500" y="520" width="360" height="112" rx="12" fill="#12303d"/>
              <text x="530" y="565" fill="#a8c3cc" font-family="Segoe UI, sans-serif" font-size="20">Network</text>
              <text x="530" y="605" fill="#eef8fb" font-family="Segoe UI, sans-serif" font-size="25">Trusted LAN / Tailscale</text>
              <rect x="892" y="520" width="440" height="112" rx="12" fill="#12303d"/>
              <text x="922" y="565" fill="#a8c3cc" font-family="Segoe UI, sans-serif" font-size="20">Controls</text>
              <text x="922" y="605" fill="#eef8fb" font-family="Segoe UI, sans-serif" font-size="25">Connect · Clipboard · Display</text>
              <text x="108" y="790" fill="#6fe4df" font-family="ui-monospace, Consolas, monospace" font-size="18">REACH EVIDENCE FIXTURE · NOT A LIVE APPIUM CAPTURE</text>
            </svg>
            """;
        return File.WriteAllTextAsync(path, svg);
    }

    private static string Encode(string value) =>
        WebUtility.HtmlEncode(value);
}
