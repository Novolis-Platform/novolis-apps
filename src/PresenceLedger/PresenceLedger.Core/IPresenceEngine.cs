namespace PresenceLedger.Core;

/// <summary>Feeds platform observations into the inference engine.</summary>
public interface IPresenceEngine
{
    /// <summary>Processes one observation against every configured location.</summary>
    ValueTask ProcessAsync(
        Observation observation,
        CancellationToken cancellationToken = default);
}
