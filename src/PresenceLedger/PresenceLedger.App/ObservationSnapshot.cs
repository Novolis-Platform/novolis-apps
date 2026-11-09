using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>Compact observation health for the Today hero.</summary>
public sealed record ObservationSnapshot(
    bool IsRunning,
    string Summary,
    string PollLabel,
    string PublishLabel,
    string? LastPlacement,
    DateTimeOffset? LastPositionAt,
    DateTimeOffset? LastWifiAt,
    string? LastError)
{
    /// <summary>Builds a snapshot from the live coordinator and settings.</summary>
    public static ObservationSnapshot Capture(
        PresenceObservationCoordinator? coordinator,
        ObservationSettings settings,
        string capabilitySummary)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var running = coordinator?.IsRunning == true;
        return new ObservationSnapshot(
            running,
            capabilitySummary,
            $"Poll {settings.PollIntervalSeconds}s",
            settings.AutoPublishDownloads
                ? $"Publish ≥{settings.AutoPublishMinimumIntervalMinutes}m"
                : "Publish off",
            coordinator?.LastPlacement,
            coordinator?.LastPositionAt,
            coordinator?.LastWifiAt,
            coordinator?.LastError?.Message);
    }
}
