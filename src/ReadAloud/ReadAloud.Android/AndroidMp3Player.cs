using Android.Media;
using Android.Media.Session;
using Android.OS;
using Java.IO;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Manuscript.Export.Audio;
using ReadAloud.Services;
using File = System.IO.File;
using Path = System.IO.Path;

namespace ReadAloud.Android;

/// <summary>Plays Azure Speech MP3 bytes and publishes the listen as Android media playback.</summary>
public sealed class AndroidMp3Player : IAudioPlayer, IDisposable
{
    readonly object _gate = new();
    readonly Handler _main = new(Looper.MainLooper!);
    readonly ReadAloudPlaybackSession _session = new();
    MediaPlayer? _player;
    string? _tempPath;
    FileInputStream? _stream;
    CancellationTokenRegistration _registration;
    TaskCompletionSource? _tcs;
    Action? _tick;
    bool _userPaused;
    bool _ducked;

    public AndroidMp3Player()
    {
        Active = this;
    }

    public static AndroidMp3Player? Active { get; private set; }

    public static void RequestExternalStop()
    {
        var speech = ReadAloud.App.Services?.GetService<SpeechService>();
        if (speech is not null)
            speech.Stop();
        else
            Active?.Stop();
    }

    public Task PlayAsync(byte[] mp3, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mp3);
        if (mp3.Length == 0)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _main.Post(() =>
        {
            try
            {
                PlayOnMain(mp3, tcs, cancellationToken);
            }
            catch (Exception ex)
            {
                ReleasePlayer();
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public void Pause() => RunOnMain(PauseOnMain);

    public void Resume() => RunOnMain(ResumeOnMain);

    public void Duck() => RunOnMain(() =>
    {
        _ducked = true;
        TrySetVolume(0.2f);
    });

    public void Unduck() => RunOnMain(() =>
    {
        _ducked = false;
        TrySetVolume(1f);
    });

    public void Stop()
    {
        if (Looper.MyLooper() == Looper.MainLooper)
            StopOnMain();
        else
        {
            var done = new ManualResetEventSlim(false);
            _main.Post(() =>
            {
                try
                {
                    StopOnMain();
                }
                finally
                {
                    done.Set();
                }
            });
            done.Wait(TimeSpan.FromSeconds(2));
        }
    }

    public void Dispose() => Stop();

    void PlayOnMain(byte[] mp3, TaskCompletionSource tcs, CancellationToken cancellationToken)
    {
        ReleasePlayer();

        var cacheDir = global::Android.App.Application.Context?.CacheDir?.AbsolutePath
                       ?? Path.GetTempPath();
        Directory.CreateDirectory(cacheDir);
        var path = Path.Combine(cacheDir, $"readaloud-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, mp3);
        var length = new FileInfo(path).Length;
        if (length <= 0)
        {
            TryDelete(path);
            tcs.TrySetException(new InvalidOperationException("Speech audio file was empty."));
            return;
        }

        var player = new MediaPlayer();
        if (_session.Attributes is { } attributes)
            player.SetAudioAttributes(attributes);
        var appContext = global::Android.App.Application.Context;
        if (appContext is not null)
            player.SetWakeMode(appContext, WakeLockFlags.Partial);

        player.Completion += (_, _) => CompleteOnMain(tcs);
        player.Error += (_, args) =>
        {
            FailOnMain(
                tcs,
                new InvalidOperationException(
                    $"Android MediaPlayer failed (what={args?.What}, extra={args?.Extra})."));
        };

        var stream = new FileInputStream(path);
        player.SetDataSource(stream.FD, 0, length);
        player.Prepare();

        var registration = cancellationToken.Register(() => _main.Post(StopOnMain));
        lock (_gate)
        {
            _player = player;
            _tempPath = path;
            _stream = stream;
            _registration.Dispose();
            _registration = registration;
            _tcs = tcs;
        }

        var duration = SafeDuration();
        if (_ducked)
            TrySetVolume(0.2f);

        if (_userPaused)
        {
            _session.Publish(PlaybackStateCode.Paused, 0, duration, 0f);
            return;
        }

        player.Start();
        _session.Publish(PlaybackStateCode.Playing, 0, duration, 1f);
        StartTick();
    }

    void PauseOnMain()
    {
        _userPaused = true;
        try
        {
            if (_player is { IsPlaying: true })
                _player.Pause();
        }
        catch
        {
            // Already idle between segments.
        }

        StopTick();
        _session.Publish(PlaybackStateCode.Paused, SafePosition(), SafeDuration(), 0f);
    }

    void ResumeOnMain()
    {
        _userPaused = false;
        try
        {
            if (_player is not null && !_player.IsPlaying)
                _player.Start();
        }
        catch
        {
            // The next segment starts when synthesis returns.
        }

        if (_player is { IsPlaying: true })
        {
            _session.Publish(PlaybackStateCode.Playing, SafePosition(), SafeDuration(), 1f);
            StartTick();
            return;
        }

        _session.Publish(PlaybackStateCode.Buffering, SafePosition(), SafeDuration(), 0f);
    }

    void CompleteOnMain(TaskCompletionSource tcs)
    {
        var position = SafePosition();
        var duration = SafeDuration();
        lock (_gate)
        {
            if (!ReferenceEquals(_tcs, tcs))
                return;

            _registration.Dispose();
            _registration = default;
            _tcs = null;
            CleanupPlayer();
        }

        StopTick();
        _session.Publish(PlaybackStateCode.Buffering, position, duration, 0f);
        tcs.TrySetResult();
    }

    void FailOnMain(TaskCompletionSource tcs, Exception exception)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_tcs, tcs))
                return;

            _registration.Dispose();
            _registration = default;
            _tcs = null;
            CleanupPlayer();
        }

        StopTick();
        tcs.TrySetException(exception);
    }

    void StopOnMain()
    {
        _userPaused = false;
        TaskCompletionSource? tcs;
        lock (_gate)
        {
            _registration.Dispose();
            _registration = default;
            tcs = _tcs;
            _tcs = null;
            CleanupPlayer();
        }

        StopTick();
        _session.End();
        tcs?.TrySetCanceled();
    }

    void ReleasePlayer()
    {
        lock (_gate)
        {
            _registration.Dispose();
            _registration = default;
            CleanupPlayer();
        }
    }

    void StartTick()
    {
        _tick ??= () =>
        {
            if (_player is not { IsPlaying: true })
                return;

            try
            {
                _session.UpdatePosition(_player.CurrentPosition, Math.Max(_player.Duration, 0));
            }
            catch
            {
                return;
            }

            _main.PostDelayed(_tick!, 1000);
        };
        _main.RemoveCallbacks(_tick);
        _main.PostDelayed(_tick, 1000);
    }

    void StopTick()
    {
        if (_tick is not null)
            _main.RemoveCallbacks(_tick);
    }

    void RunOnMain(Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
            action();
        else
            _main.Post(action);
    }

    void CleanupPlayer()
    {
        try
        {
            _player?.Stop();
        }
        catch
        {
            // Already stopped or paused before start.
        }

        try
        {
            _player?.Reset();
        }
        catch
        {
            // Ignore.
        }

        _player?.Release();
        _player = null;

        try
        {
            _stream?.Close();
        }
        catch
        {
            // Ignore.
        }

        _stream = null;

        if (_tempPath is not null)
        {
            TryDelete(_tempPath);
            _tempPath = null;
        }
    }

    long SafePosition()
    {
        try
        {
            return _player?.CurrentPosition ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    long SafeDuration()
    {
        try
        {
            var duration = _player?.Duration ?? 0;
            return duration > 0 ? duration : 0;
        }
        catch
        {
            return 0;
        }
    }

    void TrySetVolume(float volume)
    {
        try
        {
            _player?.SetVolume(volume, volume);
        }
        catch
        {
            // Player is between segments.
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
