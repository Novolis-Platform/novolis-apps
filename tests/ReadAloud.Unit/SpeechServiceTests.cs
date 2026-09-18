using Novolis.Avalonia.Mobile;
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
        await Assert.That(harness.Player.PlayCount).IsEqualTo(0);
        await Assert.That(harness.Speech.HasCachedAudio("   ")).IsFalse();
    }

    [Test]
    public async Task Speak_synthesizes_once_then_replays_from_cache()
    {
        using var harness = Harness.Create();
        const string text = "Hello from Read Aloud.";
        await harness.Speech.SpeakAsync(text);
        await Assert.That(harness.Synth.Calls).IsEqualTo(1);
        await Assert.That(harness.Player.PlayCount).IsEqualTo(1);
        await Assert.That(harness.Speech.HasCachedAudio(text)).IsTrue();

        harness.Speech.Stop();
        await harness.Speech.SpeakAsync(text);
        await Assert.That(harness.Synth.Calls).IsEqualTo(1);
        await Assert.That(harness.Player.PlayCount).IsEqualTo(2);
    }

    [Test]
    public async Task Synthesize_document_concatenates_cached_segments()
    {
        using var harness = Harness.Create();
        const string text = "First paragraph.\n\nSecond paragraph.";
        var mp3 = await harness.Speech.SynthesizeDocumentMp3Async(text);
        await Assert.That(mp3.Length).IsGreaterThan(0);
        await Assert.That(harness.Synth.Calls).IsGreaterThanOrEqualTo(2);
        await Assert.That(harness.Speech.HasCachedAudio(text)).IsTrue();
        var firstCalls = harness.Synth.Calls;

        var again = await harness.Speech.SynthesizeDocumentMp3Async(text);
        await Assert.That(Convert.ToHexString(again)).IsEqualTo(Convert.ToHexString(mp3));
        await Assert.That(harness.Synth.Calls).IsEqualTo(firstCalls);
    }

    sealed class Harness : IDisposable
    {
        public required SpeechService Speech { get; init; }
        public required FakeSynthesizer Synth { get; init; }
        public required FakePlayer Player { get; init; }
        public required string Root { get; init; }

        public static Harness Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "readaloud-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var synth = new FakeSynthesizer();
            var player = new FakePlayer();
            var speech = new SpeechService(synth, player, new TempPaths(root));
            return new Harness
            {
                Speech = speech,
                Synth = synth,
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

    sealed class FakeSynthesizer : ISynthesizer
    {
        public int Calls { get; private set; }

        public Task<byte[]> SynthesizeToMp3Async(
            string text,
            VoiceSettings settings,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var payload = System.Text.Encoding.UTF8.GetBytes("mp3:" + text);
            return Task.FromResult(payload);
        }

        public async Task SaveMp3Async(
            string text,
            string path,
            VoiceSettings settings,
            CancellationToken cancellationToken = default)
        {
            var bytes = await SynthesizeToMp3Async(text, settings, cancellationToken);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
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
