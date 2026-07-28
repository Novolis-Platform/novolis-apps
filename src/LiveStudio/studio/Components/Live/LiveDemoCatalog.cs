using Novolis.Audio.Live;

namespace LiveStudio.Components.Live;

/// <summary>Catalog of editable Live demos (source text for the editor).</summary>
public static class LiveDemoCatalog
{
    public static IReadOnlyList<LiveDemoDocument> CreateShowcase() =>
    [
        new(
            Id: "pulse-bloom",
            Title: "Pulse Bloom",
            Description: "Clean triad motif with a steady pulse.",
            SwapPolicy: SwapPolicy.Immediately,
            DelayBeforeCompile: TimeSpan.Zero,
            Source: PulseBloom),
        new(
            Id: "signal-drift",
            Title: "Signal Drift",
            Description: "Brighter transpose with a bass shift.",
            SwapPolicy: SwapPolicy.NextBeat,
            DelayBeforeCompile: TimeSpan.FromSeconds(8),
            Source: SignalDrift),
        new(
            Id: "phrase-lift",
            Title: "Phrase Lift",
            Description: "Motif opens out; accents lift on phrase boundaries.",
            SwapPolicy: SwapPolicy.NextPhrase,
            DelayBeforeCompile: TimeSpan.FromSeconds(8),
            Source: PhraseLift),
    ];

    public const string DefaultBuffer =
        """
        // Live DSL — edit and press F5 / Ctrl+Enter. Completion: Ctrl+Space.

        using static Novolis.Audio.Live.Dsl.LiveDsl;
        using Novolis.Audio.Live.Dsl;
        using Novolis.Audio.MusicTheory;

        return Note.Play(PitchClass.C, 4);
        """;

    const string Usings =
        """
        using static Novolis.Audio.Live.Dsl.LiveDsl;
        using Novolis.Audio.Live.Dsl;
        using Novolis.Audio.MusicTheory;
        using Novolis.Audio.Patterns;
        """;

    const string PulseBloom =
        $$"""
        {{Usings}}

        // Pulse Bloom — lead / bass / kick

        var lead = Sequence(
            Note(PitchClass.C, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Note(PitchClass.D, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Note(PitchClass.E, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Rest(Duration.Quarter));

        var bass = Repeat(
            Sequence(
                Note(PitchClass.C, Octave.MiddleC, Duration.Half, instrument: Instruments.Bass),
                Rest(Duration.Half)),
            2);

        var pulse = Layer(
            Repeat(Note(PitchClass.C, Octave.MiddleC, Duration.Eighth, Velocity.Default, Instruments.Kick), 4),
            Repeat(Rest(Duration.Eighth), 4));

        return Program(
            120m,
            Layer(lead, bass, pulse),
            Track("lead", Instruments.Pluck, lead, 0, Fx.Delay, Fx.Reverb),
            Track("bass", Instruments.Bass, bass, 1, Fx.Filter),
            Track("pulse", Instruments.Kick, pulse, 2, Fx.Compressor));
        """;

    const string SignalDrift =
        $$"""
        {{Usings}}

        // Signal Drift — transpose + bass root move

        var motif = Sequence(
            Note(PitchClass.C, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Note(PitchClass.D, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Note(PitchClass.E, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Rest(Duration.Quarter));

        var lead = Transpose(motif, 2);

        var bass = Repeat(
            Sequence(
                Note(PitchClass.F, Octave.MiddleC, Duration.Half, instrument: Instruments.Bass),
                Rest(Duration.Half)),
            2);

        var pulse = Layer(
            Repeat(Note(PitchClass.C, Octave.MiddleC, Duration.Eighth, new Velocity(112), Instruments.Kick), 4),
            Repeat(Rest(Duration.Eighth), 4));

        return Program(
            120m,
            Layer(lead, bass, pulse),
            Track("lead", Instruments.Pluck, lead, 0, Fx.Delay, Fx.Reverb),
            Track("bass", Instruments.Bass, bass, 1, Fx.Filter),
            Track("pulse", Instruments.Kick, pulse, 2, Fx.Compressor));
        """;

    const string PhraseLift =
        $$"""
        {{Usings}}

        // Phrase Lift — layered fifths + stronger kick

        var motif = Sequence(
            Note(PitchClass.C, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Note(PitchClass.D, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Note(PitchClass.E, Octave.MiddleC, Duration.Quarter, instrument: Instruments.Pluck),
            Rest(Duration.Quarter));

        var lead = Layer(motif, Transpose(motif, 7));

        var bass = Repeat(
            Sequence(
                Note(PitchClass.G, Octave.MiddleC, Duration.Half, instrument: Instruments.Bass),
                Rest(Duration.Half)),
            2);

        var pulse = Layer(
            Repeat(Note(PitchClass.C, Octave.MiddleC, Duration.Eighth, new Velocity(118), Instruments.Kick), 4),
            Repeat(Rest(Duration.Eighth), 4));

        return Program(
            120m,
            Layer(lead, bass, pulse),
            Track("lead", Instruments.Pluck, lead, 0, Fx.Delay, Fx.Reverb),
            Track("bass", Instruments.Bass, bass, 1, Fx.Filter),
            Track("pulse", Instruments.Kick, pulse, 2, Fx.Compressor));
        """;
}
