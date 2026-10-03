namespace Novolis.Hours.Storage;

/// <summary>Checks the selected persistence backend before the host reports readiness.</summary>
public interface IHoursStorageReadiness
{
    /// <summary>Runs a bounded connectivity probe without exposing credentials.</summary>
    ValueTask<HoursStorageProbeResult> CheckAsync(CancellationToken cancellationToken = default);
}
