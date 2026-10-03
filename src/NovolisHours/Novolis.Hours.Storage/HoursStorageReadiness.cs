namespace Novolis.Hours.Storage;

/// <summary>Provider-neutral result from a storage connectivity probe.</summary>
public sealed record HoursStorageProbeResult(bool IsHealthy, string Description);
