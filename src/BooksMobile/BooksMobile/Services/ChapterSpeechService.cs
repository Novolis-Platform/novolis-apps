using System.Security.Cryptography;
using System.Text;
using Novolis.Audio.Voice.EdgeTts;
using Novolis.Audio.Voice.Manuscript;
using Novolis.Avalonia.Mobile;

namespace BooksMobile.Services;

/// <summary>Speaks one chapter via Edge TTS (Ava), with on-disk cache so unchanged text is not re-synthesized.</summary>
public sealed class ChapterSpeechService : IDisposable
{
    readonly IManuscriptSynthesizer _synthesizer;
    readonly IManuscriptAudioPlayer _player;
    readonly string _cacheDir;
    readonly object _gate = new();
    CancellationTokenSource? _cts;

    public ChapterSpeechService(
        IManuscriptSynthesizer synthesizer,
        IManuscriptAudioPlayer player,
        IAppDataPaths paths)
    {
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        ArgumentNullException.ThrowIfNull(paths);
        _cacheDir = Path.Combine(paths.RootDirectory, "tts-cache");
        Directory.CreateDirectory(_cacheDir);
        Voice = ManuscriptVoiceSettings.FromProfile(EdgeVoiceProfiles.Narrator);
    }

    /// <summary>Fixed narrator profile (Ava −4%).</summary>
    public ManuscriptVoiceSettings Voice { get; }

    public bool IsSpeaking { get; private set; }

    public event EventHandler? Changed;

    public async Task SpeakChapterAsync(string markdown, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (string.IsNullOrWhiteSpace(markdown))
            return;

        CancellationToken linked;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked = _cts.Token;
            _player.Stop();
            IsSpeaking = true;
        }

        Notify();

        try
        {
            var plan = SpeechPlanner.Create(markdown, Voice.ToSpeechOptions(), speakTitle: true);
            var voiceKey = EdgeVoiceCatalog.ToShortName(Voice.Voice);
            foreach (var segment in plan.Segments)
            {
                linked.ThrowIfCancellationRequested();
                if (segment.Kind == SpeechSegmentKind.Pause)
                {
                    if (segment.PauseMs > 0)
                        await Task.Delay(segment.PauseMs, linked).ConfigureAwait(false);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(segment.Text))
                    continue;

                var (mp3, _) = await GetOrSynthesizeAsync(segment.Text, voiceKey, linked)
                    .ConfigureAwait(false);
                linked.ThrowIfCancellationRequested();
                await _player.PlayAsync(mp3, linked).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_gate)
                IsSpeaking = false;
            Notify();
        }
    }

    /// <summary>Returns whether cached audio exists for this exact markdown (Ava narrator).</summary>
    public bool HasCachedAudio(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return false;
        var plan = SpeechPlanner.Create(markdown, Voice.ToSpeechOptions(), speakTitle: true);
        var voiceKey = EdgeVoiceCatalog.ToShortName(Voice.Voice);
        foreach (var segment in plan.Segments)
        {
            if (segment.Kind != SpeechSegmentKind.Text || string.IsNullOrWhiteSpace(segment.Text))
                continue;
            if (!File.Exists(CachePath(segment.Text, voiceKey)))
                return false;
        }

        return plan.Segments.Any(s => s.Kind == SpeechSegmentKind.Text && !string.IsNullOrWhiteSpace(s.Text));
    }

    public void Stop()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _player.Stop();
            IsSpeaking = false;
        }

        Notify();
    }

    public void Dispose() => Stop();

    async Task<(byte[] Mp3, bool FromCache)> GetOrSynthesizeAsync(
        string text,
        string voiceKey,
        CancellationToken cancellationToken)
    {
        var path = CachePath(text, voiceKey);
        if (File.Exists(path))
        {
            var cached = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (cached.Length > 0)
                return (cached, true);
        }

        var mp3 = await _synthesizer.SynthesizeToMp3Async(text, Voice, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await File.WriteAllBytesAsync(path, mp3, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Cache write is best-effort.
        }

        return (mp3, false);
    }

    string CachePath(string text, string voiceKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(voiceKey + "\n" + text)))
            .ToLowerInvariant();
        return Path.Combine(_cacheDir, hash + ".mp3");
    }

    void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
