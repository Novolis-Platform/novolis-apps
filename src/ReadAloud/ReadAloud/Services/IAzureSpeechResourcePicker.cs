using Novolis.Avalonia.Speech;

namespace ReadAloud.Services;

/// <summary>
/// Host Azure Speech discovery for Read Aloud: Entra resource cascade plus Monitor usage.
/// </summary>
public interface IAzureSpeechResourcePicker : Novolis.Avalonia.Speech.IAzureSpeechResourcePicker
{
    /// <summary>
    /// Reads Azure Monitor usage for the selected resource. The endpoint is used
    /// to recover the resource identity after an app restart.
    /// </summary>
    Task<AzureSpeechUsageSnapshot> GetUsageAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default);
}
