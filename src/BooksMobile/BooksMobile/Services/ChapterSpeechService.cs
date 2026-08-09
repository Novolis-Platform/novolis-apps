using System.Security.Cryptography;
using System.Text;
using Novolis.Audio.Voice.EdgeTts;
using Novolis.Avalonia.Mobile;
using Novolis.Manuscript.Export.Audio;

namespace BooksMobile.Services;

/// <summary>Speaks one document via Edge TTS (Ava), with on-disk cache, paragraph chunks, and prefetch.</summary>
public sealed class ChapterSpeechService : IDisposable
{
    public const int MobileMaxChunkChars = 700;

    readonly ISynthesizer _synthesizer;
    readonly IAudioPlayer _player;
    readonly string _cacheDir;
    readonly object _gate = new();
    CancellationTokenSource? _cts;

    public ChapterSpeechService(
        ISynthesizer synthesizer,
        IAudioPlayer player,
        IAppDataPaths paths)
    {
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        ArgumentNullException.ThrowIfNull(paths);
        _cacheDir = Path.Combine(paths.RootDirectory, "tts-cache");
        Directory.CreateDirectory(_cacheDir);

        var profile = EdgeVoiceProfiles.Narrator;
        Voice = new VoiceSettings
        {
            Voice = profile.Voice,
            Rate = profile.Rate,
            Pitch = profile.Pitch,
            Volume = profile.Volume,
            SceneBreakMs = profile.SceneBreakMs,
            PauseMs = profile.PauseMs,
            MaxChunkChars = MobileMaxChunkChars,
            Pronunciation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Fixed narrator profile (Ava −4%) with mobile paragraph-sized chunks.</summary>
    public VoiceSettings Voice { get; }

    public bool IsSpeaking { get; private set; }

    /// <summary>Raised when speaking state changes (may be off the UI thread).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the first audio segment begins playing.</summary>
    public event EventHandler? PlaybackStarted;

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
            var textSegments = plan.Segments
                .Where(s => s.Kind == SpeechSegmentKind.Text && !string.IsNullOrWhiteSpace(s.Text))
                .Select(s => s.Text!)
                .ToList();

            Task<(byte[] Mp3, bool FromCache)>? prefetch = null;
            var textIndex = 0;
            var startedPlayback = false;

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

                var current = prefetch ?? GetOrSynthesizeAsync(segment.Text, voiceKey, linked);
                prefetch = null;
                textIndex++;
                if (textIndex < textSegments.Count)
                {
                    var nextText = textSegments[textIndex];
                    prefetch = GetOrSynthesizeAsync(nextText, voiceKey, linked);
                }

                var (mp3, _) = await current.ConfigureAwait(false);
                linked.ThrowIfCancellationRequested();
                if (mp3.Length == 0)
                    continue;
                if (!startedPlayback)
                {
                    startedPlayback = true;
                    PlaybackStarted?.Invoke(this, EventArgs.Empty);
                }

                // Play on whatever context the platform player requires; do not
                // cancel the whole chapter if one segment fails to decode.
                try
                {
                    await _player.PlayAsync(mp3, linked).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Skip bad segment; continue with the rest of the document.
                }
            }
        }
        finally
        {
            lock (_gate)
                IsSpeaking = false;
            Notify();
        }
    }

    /// <summary>Synthesizes the full document to a single MP3 (concatenated Edge segments).</summary>
    public async Task<byte[]> SynthesizeDocumentMp3Async(
        string markdown,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var plan = SpeechPlanner.Create(markdown, Voice.ToSpeechOptions(), speakTitle: true);
        var voiceKey = EdgeVoiceCatalog.ToShortName(Voice.Voice);
        using var ms = new MemoryStream();
        foreach (var segment in plan.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (segment.Kind != SpeechSegmentKind.Text || string.IsNullOrWhiteSpace(segment.Text))
                continue;
            var (mp3, _) = await GetOrSynthesizeAsync(segment.Text, voiceKey, cancellationToken)
                .ConfigureAwait(false);
            await ms.WriteAsync(mp3, cancellationToken).ConfigureAwait(false);
        }

        return ms.ToArray();
    }

    /// <summary>Returns whether cached audio exists for this exact markdown (Ava narrator).</summary>
    public bool HasCachedAudio(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return false;
        var plan = SpeechPlanner.Create(markdown, Voice.ToSpeechOptions(), speakTitle: true);
        var voiceKey = EdgeVoiceCatalog.ToShortName(Voice.Voice);
        var any = false;
        foreach (var segment in plan.Segments)
        {
            if (segment.Kind != SpeechSegmentKind.Text || string.IsNullOrWhiteSpace(segment.Text))
                continue;
            any = true;
            if (!File.Exists(CachePath(segment.Text, voiceKey)))
                return false;
        }

        return any;
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
