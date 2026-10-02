using Novolis.Ndjson;
using Novolis.Ndjson.App.Viewer;

namespace Novolis.Ndjson.App.Tests;

public sealed class ViewerNavigationTests
{
    [Test]
    public async Task Previous_ClampsAtTheBeginning()
    {
        var state = new ViewerState(
            Skip: 25,
            Take: 100,
            Slice: new NdjsonSlice(25, 100, [], HasPrevious: true, HasMore: true),
            IsRefreshing: false,
            Error: null);

        var previous = ViewerNavigation.Previous(state);

        await Assert.That(previous.Skip).IsEqualTo(0);
    }

    [Test]
    public async Task Next_UsesTheCurrentSliceSize()
    {
        var state = new ViewerState(
            Skip: 10_000,
            Take: 250,
            Slice: new NdjsonSlice(10_000, 250, [], HasPrevious: true, HasMore: true),
            IsRefreshing: false,
            Error: null);

        var next = ViewerNavigation.Next(state);

        await Assert.That(next.Skip).IsEqualTo(10_250);
    }

    [Test]
    public async Task Next_DoesNotMovePastTheEnd()
    {
        var state = new ViewerState(
            Skip: 10,
            Take: 100,
            Slice: new NdjsonSlice(10, 100, [], HasPrevious: true, HasMore: false),
            IsRefreshing: false,
            Error: null);

        var next = ViewerNavigation.Next(state);

        await Assert.That(next).IsEqualTo(state);
    }

    [Test]
    public async Task ChangingTake_PreservesLogicalLocation()
    {
        var state = new ViewerState(
            Skip: 10_000,
            Take: 100,
            Slice: null,
            IsRefreshing: false,
            Error: null);

        var changed = ViewerNavigation.ChangeTake(state, 500);

        await Assert.That(changed.Skip).IsEqualTo(10_000);
        await Assert.That(changed.Take).IsEqualTo(500);
    }
}
