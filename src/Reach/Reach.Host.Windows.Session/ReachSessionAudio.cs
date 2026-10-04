using Microsoft.Extensions.Logging;
using Novolis.Transports.LocalIpc;
using Novolis.Windows.Audio;

namespace Novolis.Reach.Host.Windows.Session;

internal sealed class ReachSessionAudio(ReachSessionHost host)
{
    internal void Start()
    {
        if (!host._audioEnabled || host._audioStarted)
            return;

        try
        {
            host._audioCapture.DataAvailable += OnDataAvailable;
            host._audioCapture.Start();
            host._audioStarted = true;
        }
        catch (Exception exception)
        {
            host._log.LogWarning(
                exception,
                "Loopback audio is unavailable; continuing without audio.");
            host._audioCapture.DataAvailable -= OnDataAvailable;
            host._audioStarted = false;
        }
    }

    internal async Task StopAsync()
    {
        if (!host._audioStarted)
            return;

        host._audioCapture.DataAvailable -= OnDataAvailable;
        host._audioStarted = false;
        await host._audioCapture.DisposeAsync().ConfigureAwait(false);
    }

    private void OnDataAvailable(object? sender, NAudio.Wave.WaveInEventArgs args)
    {
        var connection = host._connection;
        var format = host._audioCapture.Format;
        if (connection is null || format is null || args.BytesRecorded == 0)
            return;

        var data = args.Buffer.AsSpan(0, args.BytesRecorded).ToArray();
        _ = host.SendAsync(
                connection,
                ReachMessageType.AudioFrame,
                new ReachAudioFrame(
                    Interlocked.Increment(ref host._sequence),
                    DateTime.UtcNow.Ticks,
                    format.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat
                        ? "PCM_FLOAT"
                        : "PCM",
                    format.SampleRate,
                    format.Channels,
                    data,
                    format.BitsPerSample,
                    format.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat),
                "media",
                CancellationToken.None)
            .ContinueWith(
                task =>
                {
                    if (task.IsFaulted)
                        host._log.LogDebug(task.Exception, "Audio frame send failed.");
                },
                TaskScheduler.Default);
    }
}
