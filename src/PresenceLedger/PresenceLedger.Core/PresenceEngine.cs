namespace PresenceLedger.Core;

/// <summary>Coordinates location stores, pure inference, and the semantic event ledger.</summary>
public sealed class PresenceEngine
    : IPresenceEngine
{
    readonly TimeProvider _timeProvider;
    readonly ITrackedLocationStore _locations;
    readonly IPresenceStateStore _states;
    readonly IPresenceEventStore _events;

    /// <summary>Creates an engine with explicit time and persistence dependencies.</summary>
    public PresenceEngine(
        TimeProvider timeProvider,
        ITrackedLocationStore locations,
        IPresenceStateStore states,
        IPresenceEventStore events)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        _states = states ?? throw new ArgumentNullException(nameof(states));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public async ValueTask ProcessAsync(
        Observation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var effectiveObservation = NormalizeTimestamp(observation);

        await foreach (var location in _locations.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await _states.GetAsync(location.Id, cancellationToken)
                ?? LocationPresenceState.CreateAbsent(location.Id);
            var result = PresenceInference.Apply(
                location,
                state,
                effectiveObservation);

            await _states.SaveAsync(result.State, cancellationToken);
            if (result.Event is not null)
                await _events.AppendAsync(result.Event, cancellationToken);
        }
    }

    Observation NormalizeTimestamp(Observation observation)
    {
        if (observation.At != default)
            return observation;

        var at = _timeProvider.GetUtcNow();
        return observation switch
        {
            PositionObservation position => new PositionObservation(
                at,
                position.Position,
                position.AccuracyMeters),
            WifiObservation wifi => new WifiObservation(at, wifi.ConnectedSsid),
            _ => throw new ArgumentOutOfRangeException(nameof(observation)),
        };
    }
}
