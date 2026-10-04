using Microsoft.Extensions.Diagnostics.HealthChecks;
using Novolis.Hours.Storage;

namespace Novolis.Hours.Server;

/// <summary>Reports the selected Hours storage backend as part of readiness.</summary>
public sealed class HoursServerStorageHealthCheck(IHoursStorageReadiness readiness) : IHealthCheck
{
    private readonly IHoursStorageReadiness readiness =
        readiness ?? throw new ArgumentNullException(nameof(readiness));

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await readiness.CheckAsync(cancellationToken).ConfigureAwait(false);
        return result.IsHealthy
            ? HealthCheckResult.Healthy(result.Description)
            : HealthCheckResult.Unhealthy(result.Description);
    }
}
