namespace FrankMoat.Narrative;

internal sealed record FaithLine(
    string Speaker,
    string Text,
    float RemainingSeconds,
    bool IsInstruction);
