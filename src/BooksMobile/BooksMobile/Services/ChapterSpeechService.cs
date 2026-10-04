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
    readonly SpeechSegmentPlayback _playback;
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
        _playback = new SpeechSegmentPlayback(front, Path.Combine(paths.RootDirectory, "tts-cache"));

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

        return await _playback.ConcatenateMp3Async(
            markdown,
            Voice,
            BuildAzureOptions(),
            speakTitle: true,
            cancellationToken).ConfigureAwait(false);
    }

    public bool HasCachedAudio(string markdown)
    {
        return _playback.HasCachedAudio(markdown, Voice, speakTitle: true);
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

    Task<byte[]> GetOrSynthesizeAsync(string text, CancellationToken cancellationToken) =>
        _playback.GetOrSynthesizeAsync(text, Voice, BuildAzureOptions(), cancellationToken);

    AzureSpeechSynthesisOptions BuildAzureOptions() => new()
    {
        VoiceName = Voice.Voice,
        Locale = _front.AzureConfiguration?.Locale ?? "en-US",
        RatePercent = Voice.RatePercent,
        PitchHertz = Voice.PitchHertz,
        VolumePercent = Voice.VolumePercent,
    };

    void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
