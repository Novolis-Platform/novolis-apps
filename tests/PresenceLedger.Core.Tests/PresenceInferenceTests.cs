using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class PresenceInferenceTests
{
    static readonly Guid LocationId = Guid.Parse("5ad8d6e7-63f1-4d22-a5e4-0a972702b4d1");
    static readonly DateTimeOffset Morning = new(
        2026,
        9,
        28,
        8,
        0,
        0,
        TimeSpan.FromHours(2));

    [Test]
    public async Task Passing_through_location_does_not_count_as_arrival()
    {
        var location = WifiLocation();
        var state = LocationPresenceState.CreateAbsent(location.Id);

        state = Apply(location, state, Position(0)).State;
        state = Apply(location, state, Wifi(30)).State;
        state = Apply(location, state, Position(60)).State;
        var result = Apply(location, state, Outside(90));

        await Assert.That(result.Event).IsNull();
        await Assert.That(result.State.State).IsEqualTo(PresenceState.Absent);
    }

    [Test]
    public async Task Sustained_corroboration_records_the_first_matching_wifi_time()
    {
        var location = WifiLocation();
        var state = LocationPresenceState.CreateAbsent(location.Id);

        state = Apply(location, state, Position(0)).State;
        state = Apply(location, state, Wifi(17)).State;
        state = Apply(location, state, Position(63)).State;
        state = Apply(location, state, Position(142)).State;
        var result = Apply(location, state, Wifi(200));

        await Assert.That(result.Event).IsNotNull();
        await Assert.That(result.Event!.Transition).IsEqualTo(PresenceTransition.Arrived);
        await Assert.That(result.Event.At).IsEqualTo(Morning.AddSeconds(17));
        await Assert.That(result.State.State).IsEqualTo(PresenceState.Present);
    }

    [Test]
    public async Task Temporary_wifi_loss_does_not_create_departure()
    {
        var location = WifiLocation();
        var state = ArrivedState(location);

        state = Apply(location, state, Wifi(60)).State;
        var result = Apply(location, state, Position(120));

        await Assert.That(result.Event).IsNull();
        await Assert.That(result.State.State).IsEqualTo(PresenceState.Present);
    }

    [Test]
    public async Task Sustained_departure_records_the_first_credible_outside_observation()
    {
        var location = WifiLocation();
        var state = ArrivedState(location);

        state = Apply(location, state, Outside(300)).State;
        state = Apply(location, state, Outside(360)).State;
        var result = Apply(location, state, Outside(480));

        await Assert.That(result.Event).IsNotNull();
        await Assert.That(result.Event!.Transition).IsEqualTo(PresenceTransition.Left);
        await Assert.That(result.Event.At).IsEqualTo(Morning.AddSeconds(300));
        await Assert.That(result.State.State).IsEqualTo(PresenceState.Absent);
    }

    [Test]
    public async Task Candidate_can_continue_after_state_is_reloaded()
    {
        var location = WifiLocation();
        var state = LocationPresenceState.CreateAbsent(location.Id);

        state = Apply(location, state, Position(0)).State;
        state = Apply(location, state, Wifi(10)).State;
        var reloaded = state with
        {
            CandidatePresent = state.CandidatePresent! with { },
        };

        reloaded = Apply(location, reloaded, Position(130)).State;
        var result = Apply(location, reloaded, Wifi(205));

        await Assert.That(result.Event).IsNotNull();
        await Assert.That(result.Event!.Transition).IsEqualTo(PresenceTransition.Arrived);
        await Assert.That(result.Event.At).IsEqualTo(Morning.AddSeconds(10));
    }

    [Test]
    public async Task Location_only_policy_requires_repeated_qualifying_fixes()
    {
        var location = new TrackedLocation(
            LocationId,
            "Cabin",
            new GeoCircle(new GeoCoordinate(58.14623, 7.99517), 200),
            null,
            new PresencePolicy(
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(2)));
        var state = LocationPresenceState.CreateAbsent(location.Id);

        state = Apply(location, state, Position(0)).State;
        state = Apply(location, state, Position(60)).State;
        var result = Apply(location, state, Position(121));

        await Assert.That(result.Event).IsNotNull();
        await Assert.That(result.Event!.At).IsEqualTo(Morning);
        await Assert.That(result.State.State).IsEqualTo(PresenceState.Present);
    }

    static TrackedLocation WifiLocation() =>
        new(
            LocationId,
            "Office",
            new GeoCircle(new GeoCoordinate(58.14623, 7.99517), 200),
            new WifiEvidence("CorpWifi"),
            new PresencePolicy(
                TimeSpan.FromMinutes(3),
                TimeSpan.FromMinutes(3),
                TimeSpan.FromMinutes(2)));

    static LocationPresenceState ArrivedState(TrackedLocation location)
    {
        var state = LocationPresenceState.CreateAbsent(location.Id);
        state = Apply(location, state, Position(0)).State;
        state = Apply(location, state, Wifi(10)).State;
        state = Apply(location, state, Position(130)).State;
        return Apply(location, state, Wifi(200)).State;
    }

    static PresenceInferenceResult Apply(
        TrackedLocation location,
        LocationPresenceState state,
        Observation observation) =>
        PresenceInference.Apply(location, state, observation);

    static PositionObservation Position(int seconds) =>
        new(
            Morning.AddSeconds(seconds),
            new GeoCoordinate(58.14623, 7.99517),
            5);

    static PositionObservation Outside(int seconds) =>
        new(
            Morning.AddSeconds(seconds),
            new GeoCoordinate(58.2, 8.1),
            5);

    static WifiObservation Wifi(int seconds) =>
        new(Morning.AddSeconds(seconds), "CorpWifi");
}
