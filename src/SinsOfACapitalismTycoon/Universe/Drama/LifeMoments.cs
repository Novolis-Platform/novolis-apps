namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Player-facing scorecard: which memoir scenes actually fired.</summary>
internal static class LifeMoments
{
  public static readonly string[] Kinds =
  [
    "fuel-famine",
    "claim",
    "grounding",
    "shock",
    "fiscal",
    "mega",
    "venture",
    "stockout",
    "empty-berth",
    "ugly-standby",
    "known-responsive",
    "escrow",
    "jump-refuse",
    "standby-pass",
    "lien",
    "burnout",
    "overhaul",
    "upgrade",
    "default",
    "tutorial",
    "soft-fail",
    "soft-fail-warn",
    "soft-fail-clear",
    "last-tramp-squeeze",
    "last-tramp",
    "last-tramp-lose",
  ];

  public static int Count(MilestoneLog log) =>
    Kinds.Sum(k => log.CountKind(k) > 0 ? 1 : 0);

  public static IEnumerable<(string Kind, int Hits, string Hook)> Score(MilestoneLog log)
  {
    foreach (var k in Kinds)
    {
      var n = log.CountKind(k);
      if (n <= 0)
      {
        continue;
      }

      yield return (k, n, Hook(k));
    }
  }

  public static string Hook(string kind) =>
    kind.ToLowerInvariant() switch
    {
      "fuel-famine" => "Geography bites — bunkers dry",
      "claim" => "Insurance pays; loss still gone",
      "grounding" => "Registry door closed",
      "shock" => "Production weather",
      "fiscal" => "Station bled for households",
      "mega" => "Bulk River biography wrote a page",
      "venture" => "New owner-master entered",
      "stockout" => "Final shelves thin",
      "empty-berth" => "Formal plan failed at the dock",
      "ugly-standby" => "Completion money, not hero money",
      "known-responsive" => "Reputation became currency",
      "escrow" => "CCA hold / release / clawback",
      "jump-refuse" => "Twelve hurts — dense Priority refused",
      "standby-pass" => "Refusal ≠ premium hit",
      "lien" => "Debt followed the hull",
      "burnout" or "overhaul" => "Drive life demanded yard time",
      "upgrade" => "Capacity chased scarcity",
      "default" => "Due-now arrived",
      "tutorial" => "Marsh / CCA onboarding beat",
      "soft-fail" => "Grounded long enough to fail soft",
      "soft-fail-warn" => "Near SoftFail — runway drama",
      "soft-fail-clear" => "Standing open again after SoftFail",
      "last-tramp-squeeze" => "Rival tramp squeezed off the board",
      "last-tramp" => "Sole operable tramp — last tramp win",
      "last-tramp-lose" => "Calypso down while rivals still sail",
      _ => kind,
    };
}
