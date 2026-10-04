using BooksWriterStudio.Services;
using Novolis.Audio.Voice;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;

namespace BooksWriterStudio.Unit;

public sealed class SpeechFrontSynthesizerTests
{
    [Test]
    public async Task Constructor_does_not_require_azure_configuration()
    {
        var front = new SpeechFront(new SilentVoice(), new MemoryTokenStore());
        var synthesizer = new SpeechFrontSynthesizer(front);

        await Assert.That(synthesizer.CanCreateMp3).IsFalse();
    }

    [Test]
    public async Task Synthesize_without_azure_uses_the_shared_capability_error()
    {
        var synthesizer = new SpeechFrontSynthesizer(
            new SpeechFront(new SilentVoice(), new MemoryTokenStore()));

        await Assert.That(async () =>
                await synthesizer.SynthesizeToMp3Async("hello", new VoiceSettings()))
            .ThrowsExactly<SpeechCapabilityException>();
    }

    [Test]
    public async Task SaveMp3_without_azure_does_not_write_a_file()
    {
        var synthesizer = new SpeechFrontSynthesizer(
            new SpeechFront(new SilentVoice(), new MemoryTokenStore()));
        var path = Path.Combine(Path.GetTempPath(), $"bws-speech-{Guid.NewGuid():N}.mp3");

        try
        {
            await Assert.That(async () =>
                    await synthesizer.SaveMp3Async("hello", path, new VoiceSettings()))
                .ThrowsExactly<SpeechCapabilityException>();
            await Assert.That(File.Exists(path)).IsFalse();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public async Task Configure_azure_enables_mp3_capability_without_calling_azure()
    {
        var front = new SpeechFront(new SilentVoice(), new MemoryTokenStore());
        await front.ConfigureAzureAsync(new AzureSpeechSetup
        {
            Endpoint = new Uri("https://speech.example.test/"),
            ApiKey = "secret",
        });

        var synthesizer = new SpeechFrontSynthesizer(front);

        await Assert.That(synthesizer.CanCreateMp3).IsTrue();
        await Assert.That(front.AzureConfiguration!.ApiKey).IsNull();
    }

    sealed class SilentVoice : IVoiceService
    {
        public Task SpeakAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteToFileAsync(
            string text,
            FileInfo destination,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    sealed class MemoryTokenStore : ISecureTokenStore
    {
        readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
