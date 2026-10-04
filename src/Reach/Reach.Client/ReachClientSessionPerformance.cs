namespace Novolis.Reach.Client;

internal static class ReachClientSessionPerformance
{
    internal static DateTimeOffset? LastVideoFrameAt(ReachClientSession session)
    {
        var ticks = Interlocked.Read(ref session._lastVideoFrameTicks);
        return ticks == 0
            ? null
            : new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    internal static void RecordPresented(
        ReachClientSession session,
        long sourceUtcTicks,
        double durationMilliseconds)
    {
        session._performance.RecordPresented(durationMilliseconds, sourceUtcTicks);
        session.RaisePerformanceChanged();
    }

    internal static void RecordDecoded(
        ReachClientSession session,
        double durationMilliseconds)
    {
        session._performance.RecordDecoded(durationMilliseconds);
        session.RaisePerformanceChanged();
    }

    internal static void RecordDropped(ReachClientSession session)
    {
        session._performance.RecordDropped();
        session.RaisePerformanceChanged();
    }
}
