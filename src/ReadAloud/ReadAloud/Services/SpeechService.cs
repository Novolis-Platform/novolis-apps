using System.Security.Cryptography;
using System.Text;
using Novolis.Audio.Voice.AzureSpeech;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;

namespace ReadAloud.Services;

/// <summary>Reads text with the selected device voice or user-owned Azure Speech.</summary>
public sealed class SpeechService : IDisposable
{
    public const int MobileMaxChunkChars = 700;

    readonly SpeechFront _front;
    readonly IAudioPlayer _player;
    readonly string _cacheDir;
    readonly object _gate = new();
    CancellationTokenSource? _cts;

    public SpeechService(
        SpeechFront front,
        IAudioPlayer player,
        Novolis.Avalonia.Mobile.IAppDataPaths paths)
    {
        _front = front ?? throw new ArgumentNullException(nameof(front));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        ArgumentNullException.ThrowIfNull(paths);
        _cacheDir = Path.Combine(paths.RootDirectory, "tts-cache");
        Directory.CreateDirectory(_cacheDir);

        _front.Changed += OnFrontChanged;
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

    /// <summary>Current speech settings used for Azure synthesis and planning.</summary>
    public VoiceSettings Voice { get; }

    /// <summary>Current provider selected in the application front.</summary>
    public SpeechProvider Provider => _front.Provider;

    /// <summary>Whether Azure MP3 output is currently available.</summary>
    public bool CanCreateMp3 => _front.Capabilities.CanCreateMp3;

    /// <summary>Redacted Azure setup for the provider settings UI.</summary>
    public AzureSpeechSetup? AzureConfiguration => _front.AzureConfiguration;

    public bool IsSpeaking { get; private set; }

    /// <summary>Raised when speaking or provider state changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the first segment begins playback.</summary>
    public event EventHandler? PlaybackStarted;

    /// <summary>Loads secure provider configuration before the first UI refresh.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _front.InitializeAsync(cancellationToken).ConfigureAwait(false);
        Notify();
    }

    /// <summary>Selects local device voice.</summary>
    public Task UseDeviceVoiceAsync(CancellationToken cancellationToken = default) =>
        _front.UseDeviceVoiceAsync(cancellationToken);

    /// <summary>Selects configured Azure Speech.</summary>
    public Task UseAzureSpeechAsync(CancellationToken cancellationToken = default) =>
        _front.UseAzureSpeechAsync(cancellationToken);

    /// <summary>Stores and tests are deliberately separate so setup UI can report both steps.</summary>
    public Task ConfigureAzureAsync(
        AzureSpeechSetup setup,
        CancellationToken cancellationToken = default) =>
        _front.ConfigureAzureAsync(setup, cancellationToken);

    public Task<IReadOnlyList<AzureSpeechVoice>> TestAzureAsync(
        CancellationToken cancellationToken = default) =>
        _front.TestAzureAsync(cancellationToken);

    public Task RemoveAzureAsync(CancellationToken cancellationToken = default) =>
        _front.RemoveAzureAsync(cancellationToken);

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
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
            var plan = SpeechPlanner.Create(text, Voice.ToSpeechOptions(), speakTitle: false);
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

                PlaybackStarted?.Invoke(this, EventArgs.Empty);
                if (provider == SpeechProvider.DeviceVoice)
                {
                    await _front.ReadAsync(
                        segment.Text,
                        static (_, _) => Task.CompletedTask,
                        BuildAzureOptions(),
                        linked).ConfigureAwait(false);
                    continue;
                }

                var mp3 = await GetOrSynthesizeAsync(segment.Text, linked).ConfigureAwait(false);
                if (mp3.Length > 0)
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

    /// <summary>Synthesizes the document to one MP3 through Azure Speech.</summary>
    public async Task<byte[]> SynthesizeDocumentMp3Async(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];
        if (!_front.IsAzureConfigured)
            throw new SpeechCapabilityException(
                "MP3 export requires Azure Speech setup. Device voice does not create files.");

        var plan = SpeechPlanner.Create(text, Voice.ToSpeechOptions(), speakTitle: false);
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

    /// <summary>Returns whether all Azure segments for this text are cached.</summary>
    public bool HasCachedAudio(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !_front.IsAzureConfigured)
            return false;

        var plan = SpeechPlanner.Create(text, Voice.ToSpeechOptions(), speakTitle: false);
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

    /// <summary>Stops playback and cancels the current synthesis operation.</summary>
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

    public void Dispose()
    {
        _front.Changed -= OnFrontChanged;
        Stop();
    }

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
            // Cache writes are best-effort; playback/export still succeeds.
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
        var cacheKey = string.Join(
            "\n",
            Voice.Voice,
            Voice.RatePercent,
            Voice.PitchHertz,
            Voice.VolumePercent,
            text);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))
            .ToLowerInvariant();
        return Path.Combine(_cacheDir, hash + ".mp3");
    }

    void OnFrontChanged(object? sender, EventArgs e) => Notify();

    void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
