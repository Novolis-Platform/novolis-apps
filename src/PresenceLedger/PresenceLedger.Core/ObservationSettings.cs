using System.Text.Json.Serialization;

namespace PresenceLedger.Core;

/// <summary>User-tunable observation loop, freshness windows, and publish cadence.</summary>
public sealed record ObservationSettings
{
    /// <summary>Shipped defaults used before the user changes anything.</summary>
    public static ObservationSettings Default { get; } = new();

    /// <summary>Creates observation settings with optional overrides.</summary>
    [JsonConstructor]
    public ObservationSettings(
        int pollIntervalSeconds = 30,
        int freshFixAgeMinutes = 2,
        int freshNetworkAgeMinutes = 2,
        bool autoPublishDownloads = false,
        int autoPublishMinimumIntervalMinutes = 15,
        string? clockifyProjectName = null,
        bool clockifyBillable = false)
    {
        PollIntervalSeconds = Clamp(pollIntervalSeconds, 10, 600, 30);
        FreshFixAgeMinutes = Clamp(freshFixAgeMinutes, 1, 60, 2);
        FreshNetworkAgeMinutes = Clamp(freshNetworkAgeMinutes, 1, 60, 2);
        AutoPublishDownloads = autoPublishDownloads;
        AutoPublishMinimumIntervalMinutes = Clamp(autoPublishMinimumIntervalMinutes, 1, 24 * 60, 15);
        ClockifyProjectName = NormalizeOptional(clockifyProjectName);
        ClockifyBillable = clockifyBillable;
    }

    /// <summary>How often the observation loop samples Wi-Fi / GPS.</summary>
    public int PollIntervalSeconds { get; }

    /// <summary>Maximum age of a GPS fix the coordinator will accept.</summary>
    public int FreshFixAgeMinutes { get; }

    /// <summary>Maximum age of a heard Wi-Fi network used for placement.</summary>
    public int FreshNetworkAgeMinutes { get; }

    /// <summary>Whether completed cycles may publish the ledger into Downloads.</summary>
    public bool AutoPublishDownloads { get; }

    /// <summary>Minimum gap between automatic Downloads publishes.</summary>
    public int AutoPublishMinimumIntervalMinutes { get; }

    /// <summary>Optional Clockify project label used when exporting time entries.</summary>
    public string? ClockifyProjectName { get; }

    /// <summary>Whether Clockify CSV rows are marked billable.</summary>
    public bool ClockifyBillable { get; }

    /// <summary>Poll cadence as a <see cref="TimeSpan"/>.</summary>
    [JsonIgnore]
    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    /// <summary>GPS freshness window as a <see cref="TimeSpan"/>.</summary>
    [JsonIgnore]
    public TimeSpan FreshFixAge => TimeSpan.FromMinutes(FreshFixAgeMinutes);

    /// <summary>Wi-Fi freshness window as a <see cref="TimeSpan"/>.</summary>
    [JsonIgnore]
    public TimeSpan FreshNetworkAge => TimeSpan.FromMinutes(FreshNetworkAgeMinutes);

    /// <summary>Publish throttle as a <see cref="TimeSpan"/>.</summary>
    [JsonIgnore]
    public TimeSpan AutoPublishMinimumInterval =>
        TimeSpan.FromMinutes(AutoPublishMinimumIntervalMinutes);

    /// <summary>Returns a copy with the provided field overrides.</summary>
    public ObservationSettings With(
        int? pollIntervalSeconds = null,
        int? freshFixAgeMinutes = null,
        int? freshNetworkAgeMinutes = null,
        bool? autoPublishDownloads = null,
        int? autoPublishMinimumIntervalMinutes = null,
        string? clockifyProjectName = null,
        bool? clockifyBillable = null,
        bool clearClockifyProjectName = false) =>
        new(
            pollIntervalSeconds ?? PollIntervalSeconds,
            freshFixAgeMinutes ?? FreshFixAgeMinutes,
            freshNetworkAgeMinutes ?? FreshNetworkAgeMinutes,
            autoPublishDownloads ?? AutoPublishDownloads,
            autoPublishMinimumIntervalMinutes ?? AutoPublishMinimumIntervalMinutes,
            clearClockifyProjectName ? null : clockifyProjectName ?? ClockifyProjectName,
            clockifyBillable ?? ClockifyBillable);

    static int Clamp(int value, int min, int max, int fallback) =>
        value < min || value > max ? fallback : value;

    static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
