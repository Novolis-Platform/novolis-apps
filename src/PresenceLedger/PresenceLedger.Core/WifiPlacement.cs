namespace PresenceLedger.Core;

/// <summary>Decides when a connected network stands in for a GPS fix.</summary>
public static class WifiPlacement
{
    /// <summary>Returns the configured place whose SSID matches the connected network.</summary>
    public static TrackedLocation? Match(
        string? connectedSsid,
        IEnumerable<TrackedLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        if (string.IsNullOrWhiteSpace(connectedSsid))
            return null;

        foreach (var location in locations)
        {
            if (location.Wifi is not null && SameSsid(location.Wifi.Ssid, connectedSsid))
                return location;
        }

        return null;
    }

    /// <summary>
    /// Returns the first configured place whose name is among networks that were heard.
    /// The caller orders names by preference, strongest signal first.
    /// </summary>
    public static TrackedLocation? MatchHeard(
        IEnumerable<string?> heardSsids,
        IEnumerable<TrackedLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(heardSsids);
        ArgumentNullException.ThrowIfNull(locations);
        var places = locations as IReadOnlyList<TrackedLocation> ?? locations.ToArray();
        foreach (var ssid in heardSsids)
        {
            var matched = Match(ssid, places);
            if (matched is not null)
                return matched;
        }

        return null;
    }

    /// <summary>
    /// GPS is required when the phone is not on a network that belongs to a configured place.
    /// </summary>
    public static bool ShouldRequestPositionFix(
        string? connectedSsid,
        IEnumerable<TrackedLocation> locations) =>
        Match(connectedSsid, locations) is null;

    /// <summary>Compares SSIDs after trimming and removing wrapping quotes.</summary>
    public static bool SameSsid(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    /// <summary>Trims an SSID and removes one pair of wrapping quotes.</summary>
    public static string Normalize(string ssid)
    {
        ArgumentNullException.ThrowIfNull(ssid);
        var normalized = ssid.Trim();
        if (normalized.Length >= 2
            && normalized[0] == '"'
            && normalized[^1] == '"')
            normalized = normalized[1..^1].Trim();

        return normalized;
    }
}
