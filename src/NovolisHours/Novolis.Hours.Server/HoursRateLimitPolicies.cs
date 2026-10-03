using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Novolis.Hours.Server;

/// <summary>Named request-rate policies for security-sensitive Hours endpoints.</summary>
public static class HoursRateLimitPolicies
{
    /// <summary>Partitioned fixed-window policy for credential sign-in.</summary>
    public const string Login = "hours-login";

    /// <summary>Registers the product's rate-limiting policies.</summary>
    public static void AddTo(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = static (context, _) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            return ValueTask.CompletedTask;
        };
        options.AddPolicy(Login, httpContext =>
        {
            var address = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var login = httpContext.Request.Query["login"].ToString();
            return RateLimitPartition.GetFixedWindowLimiter(
                $"{address}:{login}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
        });
    }
}
