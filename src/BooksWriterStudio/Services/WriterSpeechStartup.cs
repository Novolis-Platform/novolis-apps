using System.Diagnostics.CodeAnalysis;
using Novolis.Avalonia.Speech;

namespace BooksWriterStudio.Services;

/// <summary>
/// Loads Read Aloud's <see cref="SpeechFront"/> store, then optionally imports
/// <c>NOVOLIS_AZURE_SPEECH_*</c> when no Azure connection is saved yet.
/// </summary>
static class WriterSpeechStartup
{
    public static async Task InitializeAsync(
        SpeechFront front,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(front);
        await front.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (front.IsAzureConfigured || !TryReadEnvironment(out var setup))
            return;

        await front.ConfigureAzureAsync(setup, cancellationToken).ConfigureAwait(false);
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
