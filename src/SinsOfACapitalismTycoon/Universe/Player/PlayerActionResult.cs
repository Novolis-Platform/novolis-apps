namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Structured outcome for the last captain bridge action (travel, market, etc.).</summary>
internal sealed record PlayerActionResult(
  string ActionId,
  bool Ok,
  string Message,
  string? ErrorCode = null)
{
  public static PlayerActionResult Success(string actionId, string message) =>
    new(actionId, true, message);

  public static PlayerActionResult Fail(string actionId, string errorCode, string message) =>
    new(actionId, false, message, errorCode);
}

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
