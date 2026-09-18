using System.Security.Cryptography;
using System.Text;
using Novolis.Audio.Voice.AzureSpeech;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;

namespace BooksMobile.Services;

/// <summary>Reads chapters with local device speech or configured Azure Speech.</summary>
public sealed class ChapterSpeechService : IDisposable
{
    public const int MobileMaxChunkChars = 700;

    readonly SpeechFront _front;
    readonly IAudioPlayer _player;
    readonly string _cacheDir;
    readonly object _gate = new();
    CancellationTokenSource? _cts;

    public ChapterSpeechService(
        SpeechFront front,
        IAudioPlayer player,
        IAppDataPaths paths)
    {
        _front = front ?? throw new ArgumentNullException(nameof(front));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        ArgumentNullException.ThrowIfNull(paths);
        _cacheDir = Path.Combine(paths.RootDirectory, "tts-cache");
        Directory.CreateDirectory(_cacheDir);

        Voice = new VoiceSettings
        {
            Voice = "en-US-AvaMultilingualNeural",
            RatePercent = -4,
            PitchHertz = 0,
            VolumePercent = 0,
            SceneBreakMs = 1200,
            PauseMs = 500,
            MaxChunkChars = MobileMaxChunkChars,
            Pronunciation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Default Azure voice settings used when Azure is configured.</summary>
    public VoiceSettings Voice { get; }

    public bool IsSpeaking { get; private set; }

    public event EventHandler? Changed;
    public event EventHandler? PlaybackStarted;

    public async Task SpeakChapterAsync(
        string markdown,
        CancellationToken cancellationToken = default)
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
            var provider = _front.Provider;
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

                if (provider == SpeechProvider.DeviceVoice)
                {
                    PlaybackStarted?.Invoke(this, EventArgs.Empty);
                    await _front.ReadAsync(
                        segment.Text,
                        static (_, _) => Task.CompletedTask,
                        BuildAzureOptions(),
                        linked).ConfigureAwait(false);
                    continue;
                }

                var mp3 = await GetOrSynthesizeAsync(segment.Text, linked).ConfigureAwait(false);
                if (mp3.Length == 0)
                    continue;
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
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
                    // Skip a bad segment while continuing the chapter.
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

    /// <summary>Synthesizes one chapter to MP3 through the configured Azure resource.</summary>
    public async Task<byte[]> SynthesizeDocumentMp3Async(
        string markdown,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (!_front.IsAzureConfigured)
            throw new SpeechCapabilityException(
                "MP3 export requires Azure Speech setup. Device voice does not create files.");

        var plan = SpeechPlanner.Create(markdown, Voice.ToSpeechOptions(), speakTitle: true);
        using var output = new MemoryStream();
        foreach (var segment in plan.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (segment.Kind != SpeechSegmentKind.Text || string.IsNullOrWhiteSpace(segment.Text))
                continue;
            var mp3 = await GetOrSynthesizeAsync(segment.Text, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(mp3, cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    public bool HasCachedAudio(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown) || !_front.IsAzureConfigured)
            return false;

        var plan = SpeechPlanner.Create(markdown, Voice.ToSpeechOptions(), speakTitle: true);
        var any = false;
        foreach (var segment in plan.Segments)
        {
            if (segment.Kind != SpeechSegmentKind.Text || string.IsNullOrWhiteSpace(segment.Text))
                continue;
            any = true;
            if (!File.Exists(CachePath(segment.Text)))
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

    async Task<byte[]> GetOrSynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        var path = CachePath(text);
        if (File.Exists(path))
        {
            var cached = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (cached.Length > 0)
                return cached;
        }

        var mp3 = await _front.CreateMp3Async(
                text,
                BuildAzureOptions(),
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await File.WriteAllBytesAsync(path, mp3, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Cache write is best-effort.
        }

        return mp3;
    }

    AzureSpeechSynthesisOptions BuildAzureOptions() => new()
    {
        VoiceName = Voice.Voice,
        Locale = _front.AzureConfiguration?.Locale ?? "en-US",
        RatePercent = Voice.RatePercent,
        PitchHertz = Voice.PitchHertz,
        VolumePercent = Voice.VolumePercent,
    };

    string CachePath(string text)
    {
        var key = string.Join(
            "\n",
            Voice.Voice,
            Voice.RatePercent,
            Voice.PitchHertz,
            Voice.VolumePercent,
            text);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        return Path.Combine(_cacheDir, hash + ".mp3");
    }

    void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
