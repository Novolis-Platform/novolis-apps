using BooksWriterStudio.Services;
using Novolis.Audio.Voice;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Speech;

namespace BooksWriterStudio.Unit;

[NotInParallel]
public sealed class WriterSpeechStartupTests
{
    [Test]
    public async Task Missing_environment_leaves_speech_front_unconfigured()
    {
        using var env = AzureSpeechEnvironment.Clear();
        var front = new SpeechFront(new SilentVoice(), new MemoryTokenStore());

        await WriterSpeechStartup.InitializeAsync(front);

        await Assert.That(front.IsAzureConfigured).IsFalse();
        await Assert.That(front.Capabilities.CanCreateMp3).IsFalse();
    }

    [Test]
    public async Task Environment_credentials_import_into_the_shared_speech_front()
    {
        using var env = AzureSpeechEnvironment.Set(
            "https://speech.example.test/",
            "secret");
        var store = new MemoryTokenStore();
        var front = new SpeechFront(new SilentVoice(), store);

        await WriterSpeechStartup.InitializeAsync(front);

        await Assert.That(front.IsAzureConfigured).IsTrue();
        await Assert.That(front.Capabilities.CanCreateMp3).IsTrue();
        await Assert.That(front.AzureConfiguration!.ApiKey).IsNull();
        await Assert.That(store.Values.Single()).Contains("secret");
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

        public IReadOnlyCollection<string> Values => _values.Values;

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
