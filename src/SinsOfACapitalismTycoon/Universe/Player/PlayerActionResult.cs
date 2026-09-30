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
