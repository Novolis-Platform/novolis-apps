namespace ReadAloud.Reading;

/// <summary>Where the current listen sits. A stop returns the index to the start.</summary>
public readonly record struct SpeechProgress(int Index, int Count, bool Reading)
{
    public static SpeechProgress Ready { get; } = new(0, 0, false);

    public string StatusText => Reading && Count > 0
        ? $"Reading · part {Math.Max(Index, 1)} of {Count}"
        : "Ready. Listen starts at the beginning.";
}
