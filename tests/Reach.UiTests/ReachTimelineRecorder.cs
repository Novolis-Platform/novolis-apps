using Novolis.Testing.Timeline;

namespace Reach.UiTests;

internal sealed class ReachTimelineRecorder
{
    private readonly TimelineRecorder _inner;

    private ReachTimelineRecorder(TimelineRecorder inner) =>
        _inner = inner;

    internal string ArtifactDirectory => _inner.ArtifactDirectory;

    internal IReadOnlyList<TimelineEvent> Events => _inner.Events;

    internal static ReachTimelineRecorder Create(
        string title = "Reach cross-platform usability timeline")
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "NOVOLIS_REACH_TIMELINE_ROOT");
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(FindArtifactsRoot(), "Reach", "usability-timeline")
            : Path.GetFullPath(configuredRoot);
        return new ReachTimelineRecorder(
            TimelineRecorder.Create(root, title, "reach-ui.log"));
    }

    internal Task CaptureAsync(
        string name,
        string narration,
        Func<string, Task> captureFrame,
        IReadOnlyDictionary<string, object?>? metadata = null,
        string frameExtension = "png") =>
        _inner.CaptureAsync(
            name,
            narration,
            captureFrame,
            metadata,
            frameExtension);

    internal void Record(
        string name,
        string status,
        string? detail = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => _inner.Record(name, status, detail, metadata);

    internal void RecordTelemetry(string name, object value) =>
        _inner.RecordTelemetry(name, value);

    internal void Log(string message) =>
        _inner.Log(message);

    internal async Task FlushAsync()
    {
        var configuredLog = Environment.GetEnvironmentVariable(
            "NOVOLIS_REACH_UI_LOG");
        if (!string.IsNullOrWhiteSpace(configuredLog)
            && File.Exists(configuredLog))
        {
            Log("Host/service log attachment is available.");
        }
        else
        {
            Log(
                "No NOVOLIS_REACH_UI_LOG was configured; host/service log attachment was skipped.");
        }

        await _inner.FlushAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(configuredLog)
            && File.Exists(configuredLog))
        {
            File.Copy(
                configuredLog,
                Path.Combine(ArtifactDirectory, "logs", "host-service.log"),
                overwrite: true);
        }
    }

    private static string FindArtifactsRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            var artifacts = Path.Combine(cursor.FullName, "artifacts");
            if (Directory.Exists(artifacts)
                || File.Exists(Path.Combine(cursor.FullName, "Directory.Build.props")))
            {
                return artifacts;
            }

            cursor = cursor.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "artifacts");
    }
}
