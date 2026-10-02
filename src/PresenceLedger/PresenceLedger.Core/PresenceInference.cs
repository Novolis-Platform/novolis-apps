using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Pure state transition logic for one configured location.</summary>
public static class PresenceInference
{
    /// <summary>Applies one observation to one location state.</summary>
    public static PresenceInferenceResult Apply(
        TrackedLocation location,
        LocationPresenceState state,
        Observation observation)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(observation);

        if (state.LocationId != location.Id)
            throw new ArgumentException("State belongs to another location.", nameof(state));

        return observation switch
        {
            PositionObservation position => ApplyPosition(location, state, position),
            WifiObservation wifi => ApplyWifi(location, state, wifi),
            _ => throw new ArgumentOutOfRangeException(
                nameof(observation),
                observation.GetType(),
                "Unsupported observation type."),
        };
    }

    static PresenceInferenceResult ApplyPosition(
        TrackedLocation location,
        LocationPresenceState state,
        PositionObservation observation)
    {
        var classification = location.Area.Classify(
            observation.Position,
            observation.AccuracyMeters);
        var inside = classification is
            GeoContainmentClassification.DefinitelyInside
            or GeoContainmentClassification.ProbablyInside;
        var outside = classification == GeoContainmentClassification.DefinitelyOutside;

        if (state.State == PresenceState.Present)
        {
            if (inside)
                return PresentEvidence(state, observation.At);

            return outside
                ? ApplyAbsenceEvidence(location, state, observation.At)
                : new PresenceInferenceResult(state, null);
        }

        if (state.State == PresenceState.CandidateAbsent)
        {
            if (inside)
            {
                return new PresenceInferenceResult(
                    state with
                    {
                        State = PresenceState.Present,
                        CandidateAbsent = null,
                        LastEvidenceAt = observation.At,
                    },
                    null);
            }

            return outside
                ? ApplyAbsenceEvidence(location, state, observation.At)
                : new PresenceInferenceResult(state, null);
        }

        if (!inside)
        {
            return outside
                ? new PresenceInferenceResult(
                    state with
                    {
                        State = PresenceState.Absent,
                        CandidatePresent = null,
                        LastEvidenceAt = observation.At,
                    },
                    null)
                : new PresenceInferenceResult(state, null);
        }

        var candidate = AddInsideEvidence(location, state.CandidatePresent, observation.At);
        return TryConfirmArrival(location, state, candidate, observation.At);
    }

    static PresenceInferenceResult ApplyWifi(
        TrackedLocation location,
        LocationPresenceState state,
        WifiObservation observation)
    {
        if (location.Wifi is null || string.IsNullOrWhiteSpace(observation.ConnectedSsid))
            return new PresenceInferenceResult(state, null);

        var matches = WifiPlacement.SameSsid(observation.ConnectedSsid, location.Wifi.Ssid);

        if (state.State == PresenceState.Present)
        {
            return matches
                ? PresentEvidence(state, observation.At)
                : ApplyAbsenceEvidence(location, state, observation.At);
        }

        if (state.State == PresenceState.CandidateAbsent)
        {
            if (matches)
            {
                return new PresenceInferenceResult(
                    state with
                    {
                        State = PresenceState.Present,
                        CandidateAbsent = null,
                        LastEvidenceAt = observation.At,
                    },
                    null);
            }

            return ApplyAbsenceEvidence(location, state, observation.At);
        }

        if (!matches)
        {
            return new PresenceInferenceResult(
                state with
                {
                    State = PresenceState.Absent,
                    CandidatePresent = null,
                    LastEvidenceAt = observation.At,
                },
                null);
        }

        var candidate = AddWifiEvidence(location, state.CandidatePresent, observation.At);
        return TryConfirmArrival(location, state, candidate, observation.At);
    }

    static PresenceCandidate AddInsideEvidence(
        TrackedLocation location,
        PresenceCandidate? existing,
        DateTimeOffset at)
    {
        var candidate = ContinueOrRestart(location, existing, at);
        return candidate with
        {
            FirstInsideAt = candidate.FirstInsideAt ?? at,
            LastEvidenceAt = at,
            PositionObservations = candidate.PositionObservations + 1,
        };
    }

    static PresenceCandidate AddWifiEvidence(
        TrackedLocation location,
        PresenceCandidate? existing,
        DateTimeOffset at)
    {
        var candidate = ContinueOrRestart(location, existing, at);
        return candidate with
        {
            FirstMatchingWifiAt = candidate.FirstMatchingWifiAt ?? at,
            LastEvidenceAt = at,
            MatchingWifiObservations = candidate.MatchingWifiObservations + 1,
        };
    }

    static PresenceCandidate ContinueOrRestart(
        TrackedLocation location,
        PresenceCandidate? existing,
        DateTimeOffset at)
    {
        if (existing is null
            || at < existing.LastEvidenceAt
            || at - existing.LastEvidenceAt > location.Policy.MaximumEvidenceGap)
        {
            return new PresenceCandidate(
                StartedAt: null,
                FirstInsideAt: null,
                FirstMatchingWifiAt: null,
                LastEvidenceAt: at,
                PositionObservations: 0,
                MatchingWifiObservations: 0);
        }

        return existing;
    }

    static PresenceInferenceResult TryConfirmArrival(
        TrackedLocation location,
        LocationPresenceState state,
        PresenceCandidate candidate,
        DateTimeOffset at)
    {
        var startedAt = candidate.StartedAt;
        if (startedAt is null && candidate.MatchingWifiObservations >= 1)
            startedAt = candidate.FirstMatchingWifiAt;
        else if (startedAt is null && location.Wifi is null)
            startedAt = candidate.FirstInsideAt;
        else if (startedAt is null && candidate.PositionObservations >= 2)
            startedAt = candidate.FirstInsideAt;

        candidate = candidate with { StartedAt = startedAt };
        // A matching network is enough to place the person. GPS remains the
        // path for places without a network, and for a network that does not match.
        var hasRequiredEvidence = location.Wifi is null
            ? candidate.PositionObservations >= 2
            : candidate.MatchingWifiObservations >= 1
                || candidate.PositionObservations >= 2;
        if (startedAt is null
            || !hasRequiredEvidence
            || at - startedAt.Value < location.Policy.ConfirmationDuration)
        {
            return new PresenceInferenceResult(
                state with
                {
                    State = PresenceState.CandidatePresent,
                    CandidatePresent = candidate,
                    CandidateAbsent = null,
                    LastEvidenceAt = at,
                },
                null);
        }

        var presenceEvent = new PresenceEvent(
            Guid.NewGuid(),
            location.Id,
            PresenceTransition.Arrived,
            startedAt.Value,
            new PresenceEvidence(
                PresenceConfidence.Confirmed,
                location.Policy.ConfirmationDuration));
        return new PresenceInferenceResult(
            new LocationPresenceState(
                location.Id,
                PresenceState.Present,
                null,
                null,
                at),
            presenceEvent);
    }

    static PresenceInferenceResult PresentEvidence(
        LocationPresenceState state,
        DateTimeOffset at) =>
        new(
            state with
            {
                State = PresenceState.Present,
                CandidateAbsent = null,
                LastEvidenceAt = at,
            },
            null);

    static PresenceInferenceResult ApplyAbsenceEvidence(
        TrackedLocation location,
        LocationPresenceState state,
        DateTimeOffset at)
    {
        var candidate = state.CandidateAbsent;
        if (candidate is null
            || at < candidate.LastEvidenceAt
            || at - candidate.LastEvidenceAt > location.Policy.MaximumEvidenceGap)
        {
            candidate = new AbsenceCandidate(at, at);
        }
        else
        {
            candidate = candidate with { LastEvidenceAt = at };
        }

        if (at - candidate.StartedAt < location.Policy.DepartureDuration)
        {
            return new PresenceInferenceResult(
                state with
                {
                    State = PresenceState.CandidateAbsent,
                    CandidatePresent = null,
                    CandidateAbsent = candidate,
                    LastEvidenceAt = at,
                },
                null);
        }

        var presenceEvent = new PresenceEvent(
            Guid.NewGuid(),
            location.Id,
            PresenceTransition.Left,
            candidate.StartedAt,
            new PresenceEvidence(
                PresenceConfidence.Confirmed,
                location.Policy.DepartureDuration));
        return new PresenceInferenceResult(
            new LocationPresenceState(
                location.Id,
                PresenceState.Absent,
                null,
                null,
                at),
            presenceEvent);
    }
}
