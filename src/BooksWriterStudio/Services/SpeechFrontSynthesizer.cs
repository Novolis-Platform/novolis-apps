using Novolis.Audio.Voice.AzureSpeech;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;

namespace BooksWriterStudio.Services;

/// <summary>
/// Manuscript <see cref="ISynthesizer"/> over the same <see cref="SpeechFront"/> Read Aloud uses.
/// </summary>
sealed class SpeechFrontSynthesizer : ISynthesizer
{
    readonly SpeechFront _front;

    public SpeechFrontSynthesizer(SpeechFront front) =>
        _front = front ?? throw new ArgumentNullException(nameof(front));

    public bool CanCreateMp3 => _front.Capabilities.CanCreateMp3;

    public Task<byte[]> SynthesizeToMp3Async(
        string text,
        VoiceSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _front.CreateMp3Async(
            text,
            new AzureSpeechSynthesisOptions
            {
                VoiceName = settings.Voice,
                Locale = _front.AzureConfiguration?.Locale ?? "en-US",
                RatePercent = settings.RatePercent,
                PitchHertz = settings.PitchHertz,
                VolumePercent = settings.VolumePercent,
            },
            cancellationToken);
    }

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
