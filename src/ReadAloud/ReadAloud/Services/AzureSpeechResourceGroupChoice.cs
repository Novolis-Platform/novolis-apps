namespace ReadAloud.Services;

/// <summary>A resource group containing at least one compatible Speech resource.</summary>
public sealed record AzureSpeechResourceGroupChoice(
    string Name,
    int SpeechResourceCount);
