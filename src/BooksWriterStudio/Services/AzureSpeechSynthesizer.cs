using Azure;
using Novolis.Audio.Voice.AzureSpeech;
using Novolis.Manuscript.Export.Audio;

namespace BooksWriterStudio.Services;

/// <summary>Books Writer Studio composition adapter for user-owned Azure Speech.</summary>
sealed class AzureSpeechSynthesizer : ISynthesizer
{
    readonly AzureSpeechClient _client;

    public AzureSpeechSynthesizer()
    {
        var endpointText = Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY");
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Audiobook generation requires NOVOLIS_AZURE_SPEECH_ENDPOINT and NOVOLIS_AZURE_SPEECH_KEY.");
        }

        _client = new AzureSpeechClient(endpoint, new AzureKeyCredential(key));
    }

    public Task<byte[]> SynthesizeToMp3Async(
        string text,
        VoiceSettings settings,
        CancellationToken cancellationToken = default) =>
        _client.SynthesizeToMp3Async(
            text,
            new AzureSpeechSynthesisOptions
            {
                VoiceName = settings.Voice,
                RatePercent = settings.RatePercent,
                PitchHertz = settings.PitchHertz,
                VolumePercent = settings.VolumePercent,
            },
            cancellationToken);

    public async Task SaveMp3Async(
        string text,
        string path,
        VoiceSettings settings,
        CancellationToken cancellationToken = default)
    {
        var mp3 = await SynthesizeToMp3Async(text, settings, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, mp3, cancellationToken).ConfigureAwait(false);
    }
}
