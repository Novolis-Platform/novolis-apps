using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Novolis.Audio.Voice.AzureSpeech;
using Novolis.Avalonia.Speech;
using Novolis.Manuscript.Export.Audio;
using Novolis.Logging.Diagnostics;
using ReadAloud.Reading;

namespace ReadAloud.Services;

/// <summary>Reads text with the configured user-owned Azure Speech resource.</summary>
public sealed class SpeechService : IDisposable
{
    public const int MobileMaxChunkChars = 700;
    public const string DefaultVoiceName = "en-US-AvaMultilingualNeural";
    public const string DefaultLocale = "en-US";
    public const string DefaultAzureClientId = "c8b938aa-2e5d-48b4-89c6-fc139733c44d";
    public const string DefaultAzureTenantId = "25427e56-8e11-4e5a-b8e6-d7645bdc27b1";
    public const string DefaultAzureLoginHint = "frank.haugen@gmail.com";

    readonly SpeechFront _front;
    readonly IAudioPlayer _player;
    readonly ILogger<SpeechService>? _logger;
    readonly SpeechUsageLedger _usage;
    readonly string _cacheDir;
    readonly object _gate = new();
    CancellationTokenSource? _cts;
    int _listen;

    public SpeechService(
        SpeechFront front,
        IAudioPlayer player,
        Novolis.Avalonia.Mobile.IAppDataPaths paths,
        ILogger<SpeechService>? logger = null)
    {
        _front = front ?? throw new ArgumentNullException(nameof(front));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        ArgumentNullException.ThrowIfNull(paths);
        _logger = logger;
        _usage = new SpeechUsageLedger(Path.Combine(paths.RootDirectory, "speech-usage.json"));
        _cacheDir = Path.Combine(paths.RootDirectory, "tts-cache");
        Directory.CreateDirectory(_cacheDir);

        _front.Changed += OnFrontChanged;
        Voice = CreateVoice(DefaultVoiceName);
    }

    /// <summary>Current speech settings used for Azure synthesis and planning.</summary>
    public VoiceSettings Voice { get; private set; }

    /// <summary>Whether Azure MP3 output is currently available.</summary>
    public bool CanCreateMp3 => _front.Capabilities.CanCreateMp3;

    /// <summary>Characters, calls, cache replays, and failures recorded on this device.</summary>
    public string DeviceUsageSummary => _usage.Format();

    /// <summary>Redacted Azure setup for the provider settings UI.</summary>
    public AzureSpeechSetup? AzureConfiguration => _front.AzureConfiguration;

    public bool IsSpeaking { get; private set; }

    /// <summary>Segment position for the current listen. Stop returns this to the start.</summary>
    public SpeechProgress Progress { get; private set; } = SpeechProgress.Ready;

    /// <summary>The latest listen, without the spoken text.</summary>
    public SpeechOperationReport? LastOperation { get; private set; }

    /// <summary>Test seam. Production listens leave this unset and call Azure Speech.</summary>
    internal Func<string, CancellationToken, Task<byte[]>>? SynthesisOverride { get; set; }

    int _azureCalls;
    int _cacheReplays;

    /// <summary>Raised when speaking or provider state changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the first segment begins playback.</summary>
    public event EventHandler? PlaybackStarted;

    /// <summary>Loads secure provider configuration before the first UI refresh.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _front.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (_front.IsAzureConfigured)
        {
            await _front.UseAzureSpeechAsync(cancellationToken).ConfigureAwait(false);
            var setup = _front.AzureConfiguration;
            if (setup is not null && !string.IsNullOrWhiteSpace(setup.VoiceName))
                SetVoice(setup.VoiceName, setup.Locale);
        }

        Notify();
    }

    /// <summary>Updates the in-memory voice used for synthesis. Does not persist.</summary>
    public void SetVoice(string voiceName, string? locale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceName);
        _ = locale;
        Voice = CreateVoice(voiceName.Trim(), Voice);
    }

    /// <summary>Updates the in-memory voice and persists it on the Azure setup when configured.</summary>
    public async Task UpdateVoiceAsync(
        string voiceName,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        SetVoice(voiceName, locale);
        if (_front.IsAzureConfigured)
        {
            await _front
                .UpdateAzureVoiceAsync(Voice.Voice, locale, cancellationToken)
                .ConfigureAwait(false);
        }

        Notify();
    }

    /// <summary>Lists every voice available at the configured Speech resource.</summary>
    public Task<IReadOnlyList<AzureSpeechVoice>> ListVoicesAsync(
        CancellationToken cancellationToken = default) =>
        _front.ListVoicesAsync(cancellationToken);

    /// <summary>Selects configured Azure Speech.</summary>
    public Task UseAzureSpeechAsync(CancellationToken cancellationToken = default) =>
        _front.UseAzureSpeechAsync(cancellationToken);

    /// <summary>Stores and tests are deliberately separate so setup UI can report both steps.</summary>
    public async Task ConfigureAzureAsync(
        AzureSpeechSetup setup,
        CancellationToken cancellationToken = default)
    {
        await _front.ConfigureAzureAsync(setup, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(setup.VoiceName))
            SetVoice(setup.VoiceName, setup.Locale);
    }

    public Task<IReadOnlyList<AzureSpeechVoice>> TestAzureAsync(
        CancellationToken cancellationToken = default) =>
        _front.TestAzureAsync(cancellationToken);

    public Task RemoveAzureAsync(CancellationToken cancellationToken = default) =>
        _front.RemoveAzureAsync(cancellationToken);

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var speechText = MarkdownSpeechPreParser.Normalize(text);
        if (string.IsNullOrWhiteSpace(speechText))
            return;
        if (!_front.IsAzureConfigured)
        {
            throw new SpeechCapabilityException(
                "Azure Speech is required for Read Aloud playback.");
        }

        CancellationToken linked;
        var listen = 0;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked = _cts.Token;
            listen = ++_listen;
            _player.Stop();
            IsSpeaking = true;
        }

        Notify();
        var started = Stopwatch.GetTimestamp();
        _azureCalls = 0;
        _cacheReplays = 0;
        var completed = 0;
        var segmentCount = 0;
        using var operation = DiagnosticLog.BeginOperation(_logger);

        try
        {
            var plan = SpeechPlanner.Create(speechText, Voice.ToSpeechOptions(), speakTitle: false);
            segmentCount = CountSpokenSegments(plan);
            SetProgress(0, segmentCount, reading: true);
            DiagnosticLog.Information(
                _logger,
                "Speech synthesis started.",
                new DiagnosticProperties()
                    .Set("characters", speechText.Length)
                    .Set("voice", Voice.Voice)
                    .Set("segments", segmentCount));
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

                completed++;
                SetProgress(completed, segmentCount, reading: true);
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
                var mp3 = await GetOrSynthesizeAsync(segment.Text, linked).ConfigureAwait(false);
                if (mp3.Length > 0)
                {
                    DiagnosticLog.Information(
                        _logger,
                        "Audio playback started.",
                        new DiagnosticProperties()
                            .Set("segment", completed)
                            .Set("segments", segmentCount));
                    try
                    {
                        await _player.PlayAsync(mp3, linked).ConfigureAwait(false);
                        DiagnosticLog.Information(
                            _logger,
                            "Audio playback completed.",
                            new DiagnosticProperties()
                                .Set("segment", completed)
                                .Set("segments", segmentCount));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        DiagnosticLog.Error(
                            _logger,
                            ex,
                            "Audio playback failed.",
                            new DiagnosticProperties()
                                .Set("segment", completed)
                                .Set("segments", segmentCount)
                                .Set("failure", ex.GetType().Name));
                        throw;
                    }
                }
            }

            PublishReport("completed", speechText.Length, completed, segmentCount, started, failure: null);
            DiagnosticLog.Information(
                _logger,
                "Speech synthesis completed.",
                ReportProperties(speechText.Length, completed, segmentCount, started, failure: null));
            SetProgress(0, 0, reading: false);
        }
        catch (OperationCanceledException)
        {
            PublishReport("stopped", speechText.Length, completed, segmentCount, started, failure: null);
            DiagnosticLog.Information(
                _logger,
                "Speech synthesis stopped.",
                ReportProperties(speechText.Length, completed, segmentCount, started, failure: null));
            SetProgress(0, 0, reading: false);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PublishReport(
                "failed",
                speechText.Length,
                completed,
                segmentCount,
                started,
                ex.GetType().Name);
            DiagnosticLog.Error(
                _logger,
                ex,
                "Speech synthesis failed.",
                ReportProperties(speechText.Length, completed, segmentCount, started, ex.GetType().Name));
            SetProgress(0, 0, reading: false);
            throw;
        }
        finally
        {
            var current = false;
            lock (_gate)
            {
                current = listen == _listen;
                if (current)
                    IsSpeaking = false;
            }

            if (current)
                _player.Stop();

            Notify();
        }
    }

    /// <summary>Synthesizes the document to one MP3 through Azure Speech.</summary>
    public async Task<byte[]> SynthesizeDocumentMp3Async(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var speechText = MarkdownSpeechPreParser.Normalize(text);
        if (string.IsNullOrWhiteSpace(speechText))
            return [];
        if (!_front.IsAzureConfigured)
            throw new SpeechCapabilityException(
                "MP3 export requires Azure Speech setup.");

        var plan = SpeechPlanner.Create(speechText, Voice.ToSpeechOptions(), speakTitle: false);
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
        var speechText = MarkdownSpeechPreParser.Normalize(text);
        if (string.IsNullOrWhiteSpace(speechText) || !_front.IsAzureConfigured)
            return false;

        var plan = SpeechPlanner.Create(speechText, Voice.ToSpeechOptions(), speakTitle: false);
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

    /// <summary>Stops playback, cancels synthesis, and returns the listen to the first chunk.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _player.Stop();
            IsSpeaking = false;
            Progress = SpeechProgress.Ready;
        }

        Notify();
    }

    public void Dispose()
    {
        _front.Changed -= OnFrontChanged;
        Stop();
    }

    static int CountSpokenSegments(SpeechPlan plan)
    {
        var count = 0;
        foreach (var segment in plan.Segments)
        {
            if (segment.Kind == SpeechSegmentKind.Text &&
                !string.IsNullOrWhiteSpace(segment.Text))
            {
                count++;
            }
        }

        return count;
    }

    void SetProgress(int index, int count, bool reading)
    {
        Progress = new SpeechProgress(index, count, reading);
        Notify();
    }

    void PublishReport(
        string phase,
        int characters,
        int completed,
        int segmentCount,
        long startedTimestamp,
        string? failure)
    {
        LastOperation = new SpeechOperationReport(
            phase,
            Voice.Voice,
            characters,
            completed,
            segmentCount,
            (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            _azureCalls,
            _cacheReplays,
            failure);
    }

    DiagnosticProperties ReportProperties(
        int characters,
        int completed,
        int segmentCount,
        long startedTimestamp,
        string? failure)
    {
        var properties = new DiagnosticProperties()
            .Set("voice", Voice.Voice)
            .Set("characters", characters)
            .Set("segmentsCompleted", completed)
            .Set("segments", segmentCount)
            .Set("elapsedMs", (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds)
            .Set("azureCalls", _azureCalls)
            .Set("cacheReplays", _cacheReplays);
        if (!string.IsNullOrWhiteSpace(failure))
            properties.Set("failure", failure);
        return properties;
    }

    async Task<byte[]> GetOrSynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        if (SynthesisOverride is not null)
        {
            _azureCalls++;
            return await SynthesisOverride(text, cancellationToken).ConfigureAwait(false);
        }

        var path = CachePath(text);
        if (File.Exists(path))
        {
            var cached = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (cached.Length > 0)
            {
                _usage.RecordCacheReplay();
                _cacheReplays++;
                DiagnosticLog.Information(
                    _logger,
                    "Replayed cached audio.",
                    new DiagnosticProperties()
                        .Set("characters", text.Length)
                        .Set("voice", Voice.Voice)
                        .Set("bytes", cached.Length));
                return cached;
            }
        }

        var started = Stopwatch.GetTimestamp();
        byte[] mp3;
        try
        {
            mp3 = await _front.CreateMp3Async(
                    text,
                    BuildAzureOptions(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _usage.RecordFailure(ex.GetType().Name);
            DiagnosticLog.Error(
                _logger,
                ex,
                "Azure synthesis failed.",
                new DiagnosticProperties()
                    .Set("characters", text.Length)
                    .Set("voice", Voice.Voice)
                    .Set("failure", ex.GetType().Name));
            throw;
        }

        _usage.RecordAzureCall(text.Length);
        _azureCalls++;
        DiagnosticLog.Information(
            _logger,
            "Synthesized audio.",
            new DiagnosticProperties()
                .Set("characters", text.Length)
                .Set("voice", Voice.Voice)
                .Set("elapsedMs", (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds)
                .Set("bytes", mp3.Length));
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

    static VoiceSettings CreateVoice(string voiceName, VoiceSettings? current = null) => new()
    {
        Voice = voiceName,
        RatePercent = current?.RatePercent ?? -4,
        PitchHertz = current?.PitchHertz ?? 0,
        VolumePercent = current?.VolumePercent ?? 0,
        SceneBreakMs = current?.SceneBreakMs ?? 1200,
        PauseMs = current?.PauseMs ?? 500,
        MaxChunkChars = current?.MaxChunkChars ?? MobileMaxChunkChars,
        Pronunciation = current?.Pronunciation
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    void OnFrontChanged(object? sender, EventArgs e) => Notify();

    void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
