using Novolis.Audio.Live;
using Novolis.Audio.Live.Dsl;
using Novolis.Audio.MusicTheory;
using Novolis.Audio.Patterns;

namespace LiveStudio;

internal static class LiveSamplePrograms
{
    public static IReadOnlyList<LiveProgramPreset> CreateShowcasePresets() =>
    [
        new LiveProgramPreset(
            Name: "Pulse Bloom",
            Description: "A clean triad motif with a steady pulse.",
            Version: 1,
            SwapPolicy: SwapPolicy.Immediately,
            DelayBeforeCompile: TimeSpan.Zero,
            Definition: CreateProgram(1)),
        new LiveProgramPreset(
            Name: "Signal Drift",
            Description: "A brighter transpose with a bass shift.",
            Version: 2,
            SwapPolicy: SwapPolicy.NextBeat,
            DelayBeforeCompile: TimeSpan.FromSeconds(2),
            Definition: CreateProgram(2)),
        new LiveProgramPreset(
            Name: "Phrase Lift",
            Description: "The motif opens out and the accents lift on phrase boundaries.",
            Version: 3,
            SwapPolicy: SwapPolicy.NextPhrase,
            DelayBeforeCompile: TimeSpan.FromSeconds(2),
            Definition: CreateProgram(3)),
    ];

    public static LiveProgramDefinition CreateProgram(int version)
    {
        var lead = CreateLeadPattern(version);
        var bass = CreateBassPattern(version);
        var rhythm = CreateRhythmPattern(version);

        return LiveDsl.Program(
            120m,
            LiveDsl.Layer(lead, bass, rhythm),
            LiveDsl.Track("lead", Instruments.Pluck, lead, effects: [Fx.Delay, Fx.Reverb]),
            LiveDsl.Track("bass", Instruments.Bass, bass, channel: 1, effects: [Fx.Filter]),
            LiveDsl.Track("pulse", Instruments.Kick, rhythm, channel: 2, effects: [Fx.Compressor]));
    }

    private static PatternNode CreateLeadPattern(int version)
    {
        PatternNode motif = LiveDsl.Sequence(
            LiveDsl.Note(PitchClass.C, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            LiveDsl.Note(PitchClass.D, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            LiveDsl.Note(PitchClass.E, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            LiveDsl.Rest(Duration.Quarter));

        return version switch
        {
            1 => motif,
            2 => LiveDsl.Transpose(motif, 2),
            3 => LiveDsl.Layer(
                motif,
                LiveDsl.Transpose(motif, 7)),
            _ => LiveDsl.Transpose(motif, version),
        };
    }

    private static PatternNode CreateBassPattern(int version)
    {
        var root = version switch
        {
            1 => PitchClass.C,
            2 => PitchClass.F,
            3 => PitchClass.G,
            _ => PitchClass.C,
        };

        return LiveDsl.Repeat(
            LiveDsl.Sequence(
                LiveDsl.Note(root, Octave.MiddleC, Duration.Half, instrument: Instruments.Bass),
                LiveDsl.Rest(Duration.Half)),
            2);
    }

    private static PatternNode CreateRhythmPattern(int version)
    {
        var accent = version switch
        {
            1 => Velocity.Default,
            2 => new Velocity(112),
            3 => new Velocity(118),
            _ => Velocity.Default,
        };

        return LiveDsl.Layer(
            LiveDsl.Repeat(LiveDsl.Note(PitchClass.C, Octave.MiddleC, Duration.Eighth, accent, Instruments.Kick), 4),
            LiveDsl.Repeat(LiveDsl.Rest(Duration.Eighth), 4));
    }
}
