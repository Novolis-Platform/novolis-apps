namespace PresenceLedger.Core;

/// <summary>
/// Lets a configured network override a GPS fix that falls inside continuous
/// association with that network. Indoor fixes wander; the commute does not,
/// because the network changes.
/// </summary>
public static class SsidPositionAnchor
{
    /// <summary>
    /// Returns the samples in time order, with contradicted fixes moved to the
    /// matching place.
    /// </summary>
    public static IReadOnlyList<PresenceObservationRecord> Apply(
        IEnumerable<TrackedLocation> locations,
        IEnumerable<PresenceObservationRecord> observations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(observations);

        var current = locations
            .GroupBy(location => location.Id)
            .Select(group => group.Last())
            .ToArray();
        var ordered = observations.OrderBy(item => item.At).ToArray();
        var anchored = new PresenceObservationRecord[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
            anchored[index] = Anchor(current, ordered, index);

        return anchored;
    }

    static PresenceObservationRecord Anchor(
        IReadOnlyList<TrackedLocation> locations,
        IReadOnlyList<PresenceObservationRecord> ordered,
        int index)
    {
        var record = ordered[index];
        if (record.Position is null)
            return record;

        var matched = WifiPlacement.Match(record.ConnectedSsid, locations);
        if (matched is not null)
            return AtPlace(record, matched);

        var previous = NearestAssociated(ordered, index, -1);
        var next = NearestAssociated(ordered, index, 1);
        if (previous is null
            || next is null
            || !WifiPlacement.SameSsid(previous.ConnectedSsid, next.ConnectedSsid))
            return record;

        matched = WifiPlacement.Match(previous.ConnectedSsid, locations);
        if (matched is null)
            return record;

        var gap = matched.Policy.MaximumEvidenceGap;
        if (record.At - previous.At > gap || next.At - record.At > gap)
            return record;

        return AtPlace(record, matched);
    }

    static PresenceObservationRecord? NearestAssociated(
        IReadOnlyList<PresenceObservationRecord> ordered,
        int index,
        int direction)
    {
        for (var cursor = index + direction; cursor >= 0 && cursor < ordered.Count; cursor += direction)
        {
            if (ordered[cursor].WifiStatus == RecordedWifiStatus.Available
                && !string.IsNullOrWhiteSpace(ordered[cursor].ConnectedSsid))
                return ordered[cursor];
        }

        return null;
    }

    static PresenceObservationRecord AtPlace(
        PresenceObservationRecord record,
        TrackedLocation place) =>
        new(
            record.At,
            place.Area.Center,
            place.Area.RadiusMeters,
            RecordedWifiStatus.Available,
            place.Wifi!.Ssid,
            record.SchemaVersion);
}
