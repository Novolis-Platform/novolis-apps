namespace PresenceLedger.Core;

/// <summary>
/// Reapplies the current place definitions to retained samples. A place has no
/// start date: the latest definition explains every earlier sample.
/// </summary>
public static class PresenceReplay
{
    /// <summary>
    /// Replays samples in time order. A position is applied first, then a connected
    /// network, so the network has the last word for that sample.
    /// </summary>
    public static PresenceReplayResult Replay(
        IEnumerable<TrackedLocation> locations,
        IEnumerable<PresenceObservationRecord> observations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(observations);

        var current = locations
            .GroupBy(location => location.Id)
            .Select(group => group.Last())
            .ToArray();
        var states = current.ToDictionary(
            location => location.Id,
            location => LocationPresenceState.CreateAbsent(location.Id));
        var events = new List<PresenceEvent>();

        foreach (var record in observations.OrderBy(item => item.At))
        {
            foreach (var observation in Interpret(record))
            {
                foreach (var location in current)
                {
                    var result = PresenceInference.Apply(
                        location,
                        states[location.Id],
                        observation);
                    states[location.Id] = result.State;
                    if (result.Event is not null)
                        events.Add(result.Event);
                }
            }
        }

        return new PresenceReplayResult(events, states.Values.ToArray());
    }

    static IEnumerable<Observation> Interpret(PresenceObservationRecord record)
    {
        if (record.Position is { } position)
        {
            yield return new PositionObservation(
                record.At,
                position,
                record.AccuracyMeters ?? 0);
        }

        if (record.WifiStatus == RecordedWifiStatus.Available
            && !string.IsNullOrWhiteSpace(record.ConnectedSsid))
        {
            yield return new WifiObservation(record.At, record.ConnectedSsid);
        }
    }
}
