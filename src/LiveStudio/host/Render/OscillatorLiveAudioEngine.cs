using NAudio.Wave;
using Novolis.Audio.Live;

namespace Novolis.Audio.Live.Host.Render;

/// <summary>
/// v0 realtime oscillator engine for the LiveStudio host demo.
/// EffectKind chains are ignored; instruments map to basic waveforms.
/// </summary>
internal sealed class OscillatorLiveAudioEngine : ILiveAudioEngine
{
    readonly object _gate = new();
    LiveSession? _session;
    WaveOutEvent? _waveOut;
    LiveMixSampleProvider? _provider;
    bool _started;

    public float LastPeak
    {
        get
        {
            lock (_gate)
                return _provider?.LastPeak ?? 0f;
        }
    }

    public void Bind(LiveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
            _session = session;
        _provider?.Bind(session);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_started)
                return Task.CompletedTask;

            _provider = new LiveMixSampleProvider();
            if (_session is not null)
                _provider.Bind(_session);

            _waveOut = new WaveOutEvent { DesiredLatency = 80 };
            _waveOut.Init(_provider);
            _waveOut.Play();
            _started = true;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_started)
                return Task.CompletedTask;

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _provider = null;
            _started = false;
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
