using Android.Media;
using Novolis.Manuscript.Export.Audio;

namespace BooksMobile.Android;

/// <summary>Plays Edge TTS MP3 bytes via Android <see cref="MediaPlayer"/>.</summary>
public sealed class AndroidMp3Player : IAudioPlayer, IDisposable
{
    readonly object _gate = new();
    MediaPlayer? _player;
    string? _tempPath;
    CancellationTokenRegistration _registration;

    public Task PlayAsync(byte[] mp3, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mp3);
        if (mp3.Length == 0)
            return Task.CompletedTask;

        Stop();

        var path = Path.Combine(Path.GetTempPath(), $"booksmobile-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, mp3);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer();
        player.Completion += (_, _) =>
        {
            CleanupPlayer();
            tcs.TrySetResult();
        };
        player.Error += (_, _) =>
        {
            CleanupPlayer();
            tcs.TrySetException(new InvalidOperationException("Android MediaPlayer failed to play speech audio."));
        };

        try
        {
            player.SetDataSource(path);
            player.Prepare();
        }
        catch
        {
            player.Release();
            TryDelete(path);
            throw;
        }

        lock (_gate)
        {
            _player = player;
            _tempPath = path;
            _registration = cancellationToken.Register(Stop);
        }

        player.Start();
        return tcs.Task;
    }

    public void Stop()
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

        _player?.Release();
        _player = null;
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
