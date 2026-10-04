using System.Net;
using Microsoft.AspNetCore.Http;

namespace Novolis.Hours.Server;

/// <summary>Small, shared security boundary predicates used by the host and authentication endpoints.</summary>
internal static class HoursServerSecurityBoundary
{
    /// <summary>Returns whether the connection is local or has no address in an in-process test transport.</summary>
    public static bool IsLoopback(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var address = context.Connection.RemoteIpAddress;
        return address is null || IPAddress.IsLoopback(address);
    }

    /// <summary>Returns whether the request is one of the anonymous orchestrator probes.</summary>
    public static bool IsHealthProbe(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
