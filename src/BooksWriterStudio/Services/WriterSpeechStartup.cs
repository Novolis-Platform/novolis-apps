using System.Diagnostics.CodeAnalysis;
using Novolis.Avalonia.Speech;

namespace BooksWriterStudio.Services;

/// <summary>
/// Loads the <see cref="SpeechFront"/> store, optionally imports
/// <c>NOVOLIS_AZURE_SPEECH_*</c>, otherwise selects Edge Read Aloud for MP3.
/// </summary>
static class WriterSpeechStartup
{
    public static async Task InitializeAsync(
        SpeechFront front,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(front);
        await front.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (front.IsAzureConfigured)
        {
            if (front.Provider != SpeechProvider.AzureSpeech)
                await front.UseAzureSpeechAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (TryReadEnvironment(out var setup))
        {
            await front.ConfigureAzureAsync(setup, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (front.Provider != SpeechProvider.EdgeTts)
            await front.UseEdgeTtsAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static bool TryReadEnvironment([NotNullWhen(true)] out AzureSpeechSetup? setup)
    {
        setup = null;
        var endpointText = Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY");
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
            || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        setup = new AzureSpeechSetup
        {
            Endpoint = endpoint,
            ApiKey = key,
            CredentialSource = AzureSpeechCredentialSource.Manual,
            AuthenticationMode = AzureSpeechAuthenticationMode.ApiKey,
        };
        return true;
    }
}
