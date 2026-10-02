namespace Novolis.Ndjson.App.Viewer;

public static class ViewerNavigation
{
    public static ViewerState Previous(ViewerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var skip = Math.Max(0, state.Skip - state.Take);
        return state with { Skip = skip, Error = null };
    }

    public static ViewerState Next(ViewerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Slice is not { HasMore: true })
            return state;

        return state with
        {
            Skip = checked(state.Skip + state.Take),
            Error = null,
        };
    }

    public static ViewerState Jump(ViewerState state, long skip)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip));

        return state with { Skip = skip, Error = null };
    }

    public static ViewerState ChangeTake(ViewerState state, int take)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        return state with { Take = take, Error = null };
    }

    public static ViewerState BeginRefresh(ViewerState state) =>
        state with { IsRefreshing = true, Error = null };

    public static ViewerState CompleteRefresh(ViewerState state) =>
        state with { IsRefreshing = false };

    public static ViewerState Fail(ViewerState state, Exception error) =>
        state with { IsRefreshing = false, Error = error };
}
