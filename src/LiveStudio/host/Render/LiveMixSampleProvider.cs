using NAudio.Wave;
using Novolis.Audio.Live;

namespace Novolis.Audio.Live.Host.Render;

/// <summary>
/// Sample-accurate mixer. Musical time is driven here (not the coarse host timer)
/// so notes are not skipped between 125ms clock ticks.
/// </summary>
sealed class LiveMixSampleProvider : ISampleProvider
{
    readonly object _gate = new();
    readonly Random _rng = new(1);

    LiveSession? _session;
    Guid? _cachedProgramId;
    IReadOnlyList<ScheduledLiveNote> _schedule = [];
    decimal _loopBeats = 4m;
    double _beat;
    long _sampleIndex;

    public LiveMixSampleProvider()
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(LiveNoteScheduler.SampleRateHz, 1);
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Peak absolute sample in the last <see cref="Read"/> (for headless smoke).</summary>
    public float LastPeak { get; private set; }

    public void Bind(LiveSession session)
    {
        lock (_gate)
            _session = session;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        LiveSession? session;
        lock (_gate)
            session = _session;

        if (session is null)
        {
            Array.Clear(buffer, offset, count);
            LastPeak = 0;
            return count;
        }

        var program = session.ActiveProgram;
        EnsureSchedule(program);

        var bpm = program?.Bpm > 0 ? (double)program.Bpm : 120.0;
        var beatsPerSample = bpm / (60.0 * LiveNoteScheduler.SampleRateHz);
        var loop = _loopBeats > 0 ? (double)_loopBeats : 4.0;

        float peak = 0;
        for (var i = 0; i < count; i++)
        {
            _beat += beatsPerSample;
            var loopBeat = _beat % loop;
            if (loopBeat < 0)
                loopBeat += loop;

            var sample = MixAtLoopBeat(loopBeat, bpm);
            buffer[offset + i] = sample;
            var abs = MathF.Abs(sample);
            if (abs > peak)
                peak = abs;

            _sampleIndex++;
        }

        LastPeak = peak;

        // Publish musical time for snapshots / swap policies (bar = 4 beats, phrase = 16).
        var beat = (decimal)_beat;
        session.AdvanceTo(new LiveClockState(
            beat,
            1 + (int)Math.Floor(_beat / 4.0),
            1 + (int)Math.Floor(_beat / 16.0)));

        return count;
    }

    void EnsureSchedule(LiveProgram? program)
    {
        if (program is null)
        {
            _schedule = [];
            _cachedProgramId = null;
            _loopBeats = 4m;
            return;
        }

        if (_cachedProgramId == program.Id)
            return;

        _schedule = LiveNoteScheduler.Flatten(program);
        _loopBeats = Math.Max(LiveNoteScheduler.LengthBeats(program.Root), 1m);
        foreach (var track in program.Tracks)
            _loopBeats = Math.Max(_loopBeats, LiveNoteScheduler.LengthBeats(track.Pattern));

        _cachedProgramId = program.Id;
        // Keep musical time continuous across live swaps so the groove doesn't reset.
    }

    float MixAtLoopBeat(double loopBeat, double bpm)
    {
        float mix = 0;
        foreach (var note in _schedule)
        {
            var start = (double)note.StartBeat;
            var dur = (double)note.DurationBeats;
            if (dur <= 0)
                continue;

            var ageBeats = loopBeat - start;
            if (ageBeats < 0 || ageBeats >= dur)
                continue;

            var ageSeconds = ageBeats * 60.0 / bpm;
            var phase = (float)((ageSeconds * note.FrequencyHz) % 1.0);
            var env = 1f - (float)(ageBeats / dur);
            env = MathF.Max(0.08f, env);
            mix += Oscillator.Sample(note.Waveform, phase, _rng) * note.Amplitude * env * 0.22f;
        }

        return Math.Clamp(mix, -1f, 1f);
    }
}

static class Oscillator
{
    public static float Sample(LiveWaveform waveform, float phase, Random rng) => waveform switch
    {
        LiveWaveform.Square => phase < 0.5f ? 1f : -1f,
        LiveWaveform.Saw => 2f * phase - 1f,
        LiveWaveform.Triangle => 1f - 4f * MathF.Abs(phase - 0.5f),
        LiveWaveform.Noise => (float)(rng.NextDouble() * 2.0 - 1.0),
        _ => MathF.Sin(phase * MathF.Tau),
    };
}
