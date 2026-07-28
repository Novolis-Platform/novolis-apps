namespace Novolis.Audio.Live.Host.Render;

internal readonly record struct ScheduledLiveNote(
    decimal StartBeat,
    decimal DurationBeats,
    float FrequencyHz,
    float Amplitude,
    LiveWaveform Waveform);
