using Novolis.Audio.Voice;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;
using ReadAloud.Services;

namespace ReadAloud.Unit;

public sealed class SpeechServiceTests
{
    [Test]
    public async Task Empty_text_does_not_speak_or_cache()
    {
        using var harness = Harness.Create();
        await harness.Speech.SpeakAsync("   ");
        await Assert.That(harness.Voice.Spoken).IsNull();
        await Assert.That(harness.Speech.HasCachedAudio("   ")).IsFalse();
    }

    [Test]
    public async Task Device_provider_reads_without_mp3_playback()
    {
        using var harness = Harness.Create();
        await harness.Speech.SpeakAsync("Hello from Read Aloud.");

        await Assert.That(harness.Voice.Spoken).IsEqualTo("Hello from Read Aloud.");
        await Assert.That(harness.Player.PlayCount).IsEqualTo(0);
        await Assert.That(harness.Speech.Provider).IsEqualTo(SpeechProvider.DeviceVoice);
    }

    [Test]
    public async Task Device_provider_reports_mp3_capability_as_unavailable()
    {
        using var harness = Harness.Create();

        await Assert.That(async () =>
                await harness.Speech.SynthesizeDocumentMp3Async("text"))
            .ThrowsExactly<SpeechCapabilityException>();
    }

    sealed class Harness : IDisposable
    {
        public required SpeechService Speech { get; init; }
        public required CapturingVoice Voice { get; init; }
        public required FakePlayer Player { get; init; }
        public required string Root { get; init; }

        public static Harness Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "readaloud-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var voice = new CapturingVoice();
            var player = new FakePlayer();
            var front = new SpeechFront(voice, new MemoryTokenStore());
            var speech = new SpeechService(front, player, new TempPaths(root));
            return new Harness
            {
                Speech = speech,
                Voice = voice,
                Player = player,
                Root = root,
            };
        }

        public void Dispose()
        {
            Speech.Dispose();
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }

    sealed class TempPaths(string root) : IAppDataPaths
    {
        public string ProductName => "ReadAloud";
        public string RootDirectory => root;
        public string WorkspaceDirectory => Path.Combine(root, "workspace");
    }

    sealed class CapturingVoice : IVoiceService
    {
        public string? Spoken { get; private set; }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            Spoken = text;
            return Task.CompletedTask;
        }

        public Task WriteToFileAsync(
            string text,
            FileInfo destination,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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

    sealed class FakePlayer : IAudioPlayer
    {
        public int PlayCount { get; private set; }

        public Task PlayAsync(byte[] mp3, CancellationToken cancellationToken = default)
        {
            PlayCount++;
            return Task.CompletedTask;
        }

        public void Stop()
        {
        }
    }
}
