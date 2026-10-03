namespace Novolis.Hours.Storage;

/// <summary>Readiness probe for local JSON and in-memory providers.</summary>
public sealed class LocalHoursStorageReadiness : IHoursStorageReadiness
{
    /// <inheritdoc />
    public ValueTask<HoursStorageProbeResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            new HoursStorageProbeResult(true, "The local Hours storage provider is available."));
    }
}
