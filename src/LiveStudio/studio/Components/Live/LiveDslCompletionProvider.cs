using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace LiveStudio.Components.Live;

/// <summary>Live DSL completion entries for AvaloniaEdit (extractable to Novolis.Avalonia.Live).</summary>
public sealed class LiveDslCompletionData : ICompletionData
{
    public LiveDslCompletionData(string text, string description, string? insertText = null)
    {
        Text = text;
        Description = description;
        Content = text;
        InsertionText = insertText ?? text;
    }

    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public double Priority => 0;
    public string InsertionText { get; }

    public Avalonia.Media.IImage? Image => null;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, InsertionText);
    }
}

/// <summary>Static Live DSL / MusicTheory completion catalog (Ctrl+Space).</summary>
public static class LiveDslCompletionProvider
{
    static readonly IReadOnlyList<LiveDslCompletionData> All = Build();

    public static IEnumerable<ICompletionData> GetCompletions(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return All;

        return All.Where(c =>
            c.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (c.Description as string)?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
    }

    static IReadOnlyList<LiveDslCompletionData> Build() =>
    [
        new("Program", "LiveDsl.Program(bpm, root, tracks…)", "Program(120m, "),
        new("Track", "LiveDsl.Track(name, instrument, pattern, channel, effects…)", "Track(\""),
        new("Note", "LiveDsl.Note(pitchClass, octave, duration, …)", "Note(PitchClass."),
        new("Rest", "LiveDsl.Rest(duration)", "Rest(Duration."),
        new("Sequence", "LiveDsl.Sequence(steps…)", "Sequence("),
        new("Layer", "LiveDsl.Layer(layers…)", "Layer("),
        new("Repeat", "LiveDsl.Repeat(inner, count)", "Repeat("),
        new("Transpose", "LiveDsl.Transpose(inner, semitones)", "Transpose("),
        new("Note.Play", "Quick REPL: Note.Play(C4) / Note.Play(4)", "Note.Play("),
        new("Instruments.Pluck", "Pluck instrument", "Instruments.Pluck"),
        new("Instruments.Bass", "Bass instrument", "Instruments.Bass"),
        new("Instruments.Kick", "Kick instrument", "Instruments.Kick"),
        new("Instruments.Lead", "Lead instrument", "Instruments.Lead"),
        new("Fx.Delay", "Delay effect", "Fx.Delay"),
        new("Fx.Reverb", "Reverb effect", "Fx.Reverb"),
        new("Fx.Filter", "Filter effect", "Fx.Filter"),
        new("Fx.Compressor", "Compressor effect", "Fx.Compressor"),
        new("PitchClass.C", "Pitch class C", "PitchClass.C"),
        new("PitchClass.D", "Pitch class D", "PitchClass.D"),
        new("PitchClass.E", "Pitch class E", "PitchClass.E"),
        new("PitchClass.F", "Pitch class F", "PitchClass.F"),
        new("PitchClass.G", "Pitch class G", "PitchClass.G"),
        new("PitchClass.A", "Pitch class A", "PitchClass.A"),
        new("Octave.MiddleC", "Middle C octave (4)", "Octave.MiddleC"),
        new("Duration.Quarter", "Quarter note", "Duration.Quarter"),
        new("Duration.Eighth", "Eighth note", "Duration.Eighth"),
        new("Duration.Half", "Half note", "Duration.Half"),
        new("Velocity.Default", "Default velocity (96)", "Velocity.Default"),
        new("SwapPolicy.Immediately", "Swap now", "SwapPolicy.Immediately"),
        new("SwapPolicy.NextBeat", "Swap on next beat", "SwapPolicy.NextBeat"),
        new("SwapPolicy.NextPhrase", "Swap on next phrase", "SwapPolicy.NextPhrase"),
    ];
}
