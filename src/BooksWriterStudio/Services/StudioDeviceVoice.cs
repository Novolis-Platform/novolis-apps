using Novolis.Audio.Voice;

namespace BooksWriterStudio.Services;

/// <summary>
/// Device-voice placeholder so <see cref="Novolis.Avalonia.Speech.SpeechFront"/> can resolve.
/// Writer Studio playback and audiobooks go through Azure MP3, same as Read Aloud.
/// </summary>
sealed class StudioDeviceVoice : IVoiceService
{
    public Task SpeakAsync(string text, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task WriteToFileAsync(
        string text,
        FileInfo destination,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
