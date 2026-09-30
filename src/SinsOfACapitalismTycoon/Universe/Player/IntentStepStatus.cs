namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Lifecycle of one captain intent / compound step.</summary>
internal enum IntentStepStatus
{
  Pending,
  Active,
  WaitingFuel,
  WaitingCargo,
  Blocked,
  Failed,
  Done,
}
