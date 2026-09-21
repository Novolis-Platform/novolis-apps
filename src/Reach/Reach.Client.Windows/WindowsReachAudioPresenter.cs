using NAudio.Wave;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;

namespace Novolis.Reach.Client.Windows;

/// <summary>Buffers PCM blocks and plays remote audio through the default endpoint.</summary>
public sealed class WindowsReachAudioPresenter : IReachAudioPresenter
{
    private readonly object _gate = new();
    private BufferedWaveProvider? _buffer;
    private WasapiOut? _output;
    private bool _disposed;

    /// <inheritdoc />
    public void Present(ReachAudioFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!string.Equals(frame.Codec, "PCM", StringComparison.OrdinalIgnoreCase))
            return;

        lock (_gate)
        {
            if (_buffer is null)
            {
                var format = frame.IsFloat
                    ? WaveFormat.CreateIeeeFloatWaveFormat(frame.SampleRate, frame.Channels)
                    : new WaveFormat(
                        frame.SampleRate,
                        frame.BitsPerSample,
                        frame.Channels);
                _buffer = new BufferedWaveProvider(format)
                {
                    DiscardOnBufferOverflow = true,
                };
                _output = new WasapiOut();
                _output.Init(_buffer);
                _output.Play();
            }

            _buffer.AddSamples(frame.Data, 0, frame.Data.Length);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            _disposed = true;
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _buffer = null;
        }
    }
}
