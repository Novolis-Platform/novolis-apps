using System.Runtime.CompilerServices;
using Novolis.Math.Geometry;
using PresenceLedger.Core;

namespace PresenceLedger.Core.Tests;

public sealed class PresenceEngineTests
{
    static readonly Guid WorkId = Guid.Parse("0c2cdf46-fac0-4107-b3bb-f8c21c3fac46");
    static readonly Guid BarrikadenId = Guid.Parse("d7cb3f5c-95e3-4df6-9b5b-2b7bca3f0d87");

    [Test]
    public async Task Live_engine_closes_work_when_Barrikaden_is_confirmed()
    {
        var start = new DateTimeOffset(2026, 10, 8, 16, 0, 0, TimeSpan.Zero);
        var workCenter = new GeoCoordinate(58.143788, 7.992383);
        var barrikadenCenter = new GeoCoordinate(58.147712, 8.001682);
        var locations = new InMemoryLocationStore(
        [
            new TrackedLocation(
                WorkId,
                "Work",
                new GeoCircle(workCenter, 80),
                null,
                new PresencePolicy(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(20),
                    TimeSpan.FromMinutes(10))),
            new TrackedLocation(
                BarrikadenId,
                "Barrikaden",
                new GeoCircle(barrikadenCenter, 80),
                new WifiEvidence("Barrikaden"),
                new PresencePolicy(
                    TimeSpan.FromMinutes(3),
                    TimeSpan.FromMinutes(3),
                    TimeSpan.FromMinutes(5))),
        ]);
        var states = new InMemoryStateStore();
        var events = new InMemoryEventStore();
        var engine = new PresenceEngine(
            TimeProvider.System,
            locations,
            states,
            events);

        await engine.ProcessAsync(new PositionObservation(start, workCenter, 5));
        await engine.ProcessAsync(new PositionObservation(
            start.AddMinutes(5),
            workCenter,
            5));
        await engine.ProcessAsync(new WifiObservation(
            start.AddMinutes(10),
            "Barrikaden"));
        await engine.ProcessAsync(new WifiObservation(
            start.AddMinutes(13),
            "Barrikaden"));

        var workEvents = events.Items.Where(item => item.LocationId == WorkId).ToArray();
        var barrikadenEvents = events.Items
            .Where(item => item.LocationId == BarrikadenId)
            .ToArray();
        var workLeft = workEvents.Single(
            item => item.Transition == PresenceTransition.Left);
        var barrikadenArrival = barrikadenEvents.Single(
            item => item.Transition == PresenceTransition.Arrived);

        await Assert.That(workEvents.Count(
                item => item.Transition == PresenceTransition.Arrived))
            .IsEqualTo(1);
        await Assert.That(workLeft.At).IsEqualTo(barrikadenArrival.At);
        await Assert.That((await states.GetAsync(WorkId))!.State)
            .IsEqualTo(PresenceState.Absent);
        await Assert.That((await states.GetAsync(BarrikadenId))!.State)
            .IsEqualTo(PresenceState.Present);
    }

    sealed class InMemoryLocationStore : ITrackedLocationStore
    {
        readonly List<TrackedLocation> _locations;

        public InMemoryLocationStore(IEnumerable<TrackedLocation> locations) =>
            _locations = locations.ToList();

        public async IAsyncEnumerable<TrackedLocation> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var location in _locations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return location;
                await Task.CompletedTask;
            }
        }

        public IAsyncEnumerable<TrackedLocation> ReadHistoryAsync(
            CancellationToken cancellationToken = default) =>
            ReadAsync(cancellationToken);

        public ValueTask SaveAsync(
            TrackedLocation location,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = _locations.FindIndex(item => item.Id == location.Id);
            if (index >= 0)
                _locations[index] = location;
            else
                _locations.Add(location);
            return ValueTask.CompletedTask;
        }
    }

    sealed class InMemoryStateStore : IPresenceStateStore
    {
        readonly Dictionary<Guid, LocationPresenceState> _states = [];

        public ValueTask<LocationPresenceState?> GetAsync(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                _states.TryGetValue(locationId, out var state) ? state : null);
        }

        public async IAsyncEnumerable<LocationPresenceState> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var state in _states.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return state;
                await Task.CompletedTask;
            }
        }

        public ValueTask SaveAsync(
            LocationPresenceState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _states[state.LocationId] = state;
            return ValueTask.CompletedTask;
        }

        public ValueTask ReplaceAsync(
            IReadOnlyList<LocationPresenceState> states,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _states.Clear();
            foreach (var state in states)
                _states[state.LocationId] = state;
            return ValueTask.CompletedTask;
        }
    }

    sealed class InMemoryEventStore : IPresenceEventStore
    {
        public List<PresenceEvent> Items { get; } = [];

        public async IAsyncEnumerable<PresenceEvent> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var presenceEvent in Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return presenceEvent;
                await Task.CompletedTask;
            }
        }

        public ValueTask AppendAsync(
            PresenceEvent presenceEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items.Add(presenceEvent);
            return ValueTask.CompletedTask;
        }

        public ValueTask ReplaceAsync(
            IReadOnlyList<PresenceEvent> events,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items.Clear();
            Items.AddRange(events);
            return ValueTask.CompletedTask;
        }
    }
}
