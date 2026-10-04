using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Novolis.Hours.Server;

/// <summary>Product telemetry names for worktime commands, realtime projection, and audit exports.</summary>
public static class HoursServerTelemetry
{
    /// <summary>Activity source for request-independent Hours operations.</summary>
    public static ActivitySource ActivitySource { get; } = new("Novolis.Hours");

    /// <summary>Meter for durable worktime product measurements.</summary>
    public static Meter Meter { get; } = new("Novolis.Hours");

    /// <summary>Counts successful journal append operations.</summary>
    public static Counter<long> JournalAppends { get; } = Meter.CreateCounter<long>("hours.journal.appended");

    /// <summary>Counts realtime projections emitted to clients.</summary>
    public static Counter<long> RealtimeMessages { get; } = Meter.CreateCounter<long>("hours.realtime.messages");
}
