namespace LiveStudio;

internal static class LiveCodeTemplates
{
    public const string DefaultSource =
        """
        // Live code compiles on the host and plays through the audio engine.
        // Supported today: Note.Play(), Note.Play(4), Note.Play(C4)

        Note.Play(C4);
        """;

    public const string TriadSource =
        """
        Note.Play(C4);
        """;

    public const string OctaveSource =
        """
        Note.Play(4);
        """;
}
