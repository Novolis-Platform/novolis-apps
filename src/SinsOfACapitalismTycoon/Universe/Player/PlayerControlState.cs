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

/// <summary>Shared captain desk state for Calypso when <see cref="Enabled"/>.</summary>
internal sealed class PlayerControlState
{
  public bool Enabled { get; init; }

  public bool Autopilot { get; set; }

  public TransitProfile DefaultProfile { get; set; } = TransitProfile.StandardCommercial;

  /// <summary>
  /// When true, Spot intel filters to current berth only.
  /// Default false — see network postings; accept still requires dock at origin.
  /// </summary>
  public bool LocalBoardOnly { get; set; }

  public PlayerOrderQueue Orders { get; } = new();

  public BerthManifest Manifest { get; } = new();

  public OpportunitiesPool? Opportunities { get; set; }

  /// <summary>Map / CLI travel target system id.</summary>
  public string? TravelTargetSystemId { get; set; }

  /// <summary>Consecutive days Calypso cannot operate (soft-fail tracker).</summary>
  public int SoftFailGroundedDays { get; set; }

  public bool SoftFailRaised { get; set; }

  /// <summary>Memoir mode: thin rival tramps; win when Calypso is sole operable light-commercial.</summary>
  public bool LastTrampMode { get; set; }

  public bool LastTrampWon { get; set; }

  public bool LastTrampLost { get; set; }

  public TrampSurvival.Snapshot? LastSurvival { get; set; }
}
