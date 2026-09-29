using Novolis.Avalonia.Speech;

namespace ReadAloud.Services;

/// <summary>Loads an optional developer-only Azure Speech fallback.</summary>
public interface IAzureSpeechFallbackProvider
{
    /// <summary>Whether a local fallback file is available to load.</summary>
    bool IsAvailable { get; }

    /// <summary>Loads and validates the local fallback without exposing its key.</summary>
    Task<AzureSpeechSetup> LoadAsync(CancellationToken cancellationToken = default);
}
