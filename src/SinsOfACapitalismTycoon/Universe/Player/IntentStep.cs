namespace SinsOfACapitalismTycoon.Universe;

/// <summary>One visible entry on the captain action stack.</summary>
internal sealed class IntentStep
{
  public required Guid Id { get; init; }
  public required string Label { get; init; }
  public IntentStepStatus Status { get; set; } = IntentStepStatus.Pending;
  public string? Detail { get; set; }
  public PlayerOrder? Order { get; init; }
  public bool IsCompound { get; init; }
}
