using Android.Media;
using Android.OS;
using Java.IO;
using Novolis.Manuscript.Export.Audio;
using File = System.IO.File;
using Path = System.IO.Path;

namespace BooksMobile.Android;

/// <summary>Plays Edge TTS MP3 bytes via Android <see cref="MediaPlayer"/> on the main looper.</summary>
public sealed class AndroidMp3Player : IAudioPlayer, IDisposable
{
    readonly object _gate = new();
    readonly Handler _main = new(Looper.MainLooper!);
    MediaPlayer? _player;
    string? _tempPath;
    FileInputStream? _stream;
    CancellationTokenRegistration _registration;

    public Task PlayAsync(byte[] mp3, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mp3);
        if (mp3.Length == 0)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // MediaPlayer must be created/prepared/started on a looper thread (main).
        _main.Post(() =>
        {
            try
            {
                PlayOnMain(mp3, tcs, cancellationToken);
            }
            catch (Exception ex)
            {
                CleanupPlayer();
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    void PlayOnMain(byte[] mp3, TaskCompletionSource tcs, CancellationToken cancellationToken)
    {
        StopOnMain();

        var cacheDir = global::Android.App.Application.Context?.CacheDir?.AbsolutePath
                       ?? Path.GetTempPath();
        Directory.CreateDirectory(cacheDir);
        var path = Path.Combine(cacheDir, $"booksmobile-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, mp3);
        var length = new FileInfo(path).Length;
        if (length <= 0)
        {
            TryDelete(path);
            tcs.TrySetException(new InvalidOperationException("Speech audio file was empty."));
            return;
        }

        var player = new MediaPlayer();
        player.Completion += (_, _) =>
        {
            CleanupPlayer();
            tcs.TrySetResult();
        };
        player.Error += (_, args) =>
        {
            CleanupPlayer();
            tcs.TrySetException(new InvalidOperationException(
                $"Android MediaPlayer failed (what={args?.What}, extra={args?.Extra})."));
        };

        // Explicit length avoids NuPlayer reading 0x7FFFFFFFFFFFFFFF on some OEM builds.
        var stream = new FileInputStream(path);
        player.SetDataSource(stream.FD, 0, length);
        player.Prepare();

        lock (_gate)
        {
            _player = player;
            _tempPath = path;
            _stream = stream;
            _registration = cancellationToken.Register(() => _main.Post(StopOnMain));
        }

        player.Start();
    }

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

    void StopOnMain()
    {
        lock (_gate)
        {
            _registration.Dispose();
            _registration = default;
            CleanupPlayer();
        }
    }

    public void Dispose() => Stop();

    void CleanupPlayer()
    {
        try
        {
            _player?.Stop();
        }
        catch
        {
            // already stopped
        }

        try
        {
            _player?.Reset();
        }
        catch
        {
            // ignore
        }

        _player?.Release();
        _player = null;

        try
        {
            _stream?.Close();
        }
        catch
        {
            // ignore
        }

        _stream = null;

        if (_tempPath is not null)
        {
            TryDelete(_tempPath);
            _tempPath = null;
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
            // best-effort temp cleanup
        }
    }
}
