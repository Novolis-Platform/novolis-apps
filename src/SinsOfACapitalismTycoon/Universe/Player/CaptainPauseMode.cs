using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>When the live session yields to the captain.</summary>
internal enum CaptainPauseMode
{
  /// <summary>Never wait (headless / resume-to-horizon).</summary>
  Never,

  /// <summary>Pause after every day pulse (Step 1d bearings).</summary>
  EveryDay,

  /// <summary>Keep time flowing until Calypso needs a player decision.</summary>
  UntilDecision,
}
