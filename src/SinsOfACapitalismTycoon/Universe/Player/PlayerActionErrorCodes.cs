namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Stable error codes for session.command / LastAction (travel-focused v1).</summary>
internal static class PlayerActionErrorCodes
{
  public const string AlreadyHere = "already-here";
  public const string UnknownDest = "unknown-dest";
  public const string NoRoute = "no-route";
  public const string Registry = "registry";
  public const string Busy = "busy";
  public const string Bunkering = "bunkering";
  public const string PlanFailed = "plan-failed";
  public const string OriginUnknown = "origin-unknown";
  public const string Incomplete = "incomplete";
  public const string NotAtDock = "not-at-dock";
  public const string HoldFull = "hold-full";
  public const string CashShort = "cash-short";
  public const string UnknownSku = "unknown-sku";
  public const string Rejected = "rejected";
}
