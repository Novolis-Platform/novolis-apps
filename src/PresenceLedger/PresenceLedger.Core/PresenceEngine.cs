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
        Guid? exclusiveLocationId = null;

        await foreach (var location in _locations.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exclusiveLocationId is { } alreadyArrived
                && alreadyArrived != location.Id)
            {
                continue;
            }

            var effectiveLocation = PresencePolicyDefaults.UpgradeLegacyLocationOnly(location);
            var state = await _states.GetAsync(effectiveLocation.Id, cancellationToken)
                ?? LocationPresenceState.CreateAbsent(effectiveLocation.Id);
            var result = PresenceInference.Apply(
                effectiveLocation,
                state,
                effectiveObservation);
            if (result.Event is { Transition: PresenceTransition.Arrived } arrival)
            {
                exclusiveLocationId = effectiveLocation.Id;
                await CloseOtherLocationsAsync(
                    effectiveLocation,
                    arrival,
                    cancellationToken);
            }

            await _states.SaveAsync(result.State, cancellationToken);
            if (result.Event is not null)
                await _events.AppendAsync(result.Event, cancellationToken);
        }
    }

    async Task CloseOtherLocationsAsync(
        TrackedLocation arrivedLocation,
        PresenceEvent arrival,
        CancellationToken cancellationToken)
    {
        await foreach (var location in _locations.ReadAsync(cancellationToken))
        {
            var effectiveLocation = PresencePolicyDefaults.UpgradeLegacyLocationOnly(location);
            if (effectiveLocation.Id == arrivedLocation.Id)
                continue;

            var state = await _states.GetAsync(
                    effectiveLocation.Id,
                    cancellationToken)
                ?? LocationPresenceState.CreateAbsent(effectiveLocation.Id);
            if (state.State == PresenceState.Present)
            {
                await _events.AppendAsync(
                    new PresenceEvent(
                        Guid.NewGuid(),
                        effectiveLocation.Id,
                        PresenceTransition.Left,
                        arrival.At,
                        new PresenceEvidence(
                            PresenceConfidence.Confirmed,
                            effectiveLocation.Policy.DepartureDuration)),
                    cancellationToken);
            }

            await _states.SaveAsync(
                state with
                {
                    State = PresenceState.Absent,
                    CandidatePresent = null,
                    CandidateAbsent = null,
                    LastEvidenceAt = arrival.At,
                },
                cancellationToken);
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
