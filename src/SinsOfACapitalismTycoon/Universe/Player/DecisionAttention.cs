namespace SinsOfACapitalismTycoon.Universe;

/// <summary>How the bridge treats dock decisions relative to the sim clock.</summary>
internal enum DecisionAttention
{
  /// <summary>Never hard-pause for decisions; time flows at <see cref="PlayerControlState.SimSpeedScale"/>.</summary>
  RunAlways = 0,

  /// <summary>While a decision is needed, multiply pace delay (≈0.1× speed) instead of blocking.</summary>
  SoftSlow = 1,

  /// <summary>Hard gate until Continue / Step (legacy UntilDecision behaviour).</summary>
  HardPause = 2,
}
