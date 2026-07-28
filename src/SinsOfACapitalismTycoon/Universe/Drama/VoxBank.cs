namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Speakable flavor for milestones — Calypso / Review voice board.
/// Keeps gameplay readable as radio traffic, not just table rows.
/// </summary>
internal static class VoxBank
{
  public sealed record Line(string Voice, string Text);

  public static Line ForMilestone(string kind, string detail)
  {
    var k = kind.Trim().ToLowerInvariant();
    return k switch
    {
      "fuel-famine" => new("vox.drama",
        $"Fuel window closed — {Truncate(detail)}. Plan fails rising."),
      "shock" => new("vox.drama",
        $"Pattern break — {Truncate(detail)}. Quiet money ends here."),
      "claim" => new("vox.broker",
        $"Claim posted net of deductible. {Truncate(detail)}. Loss quantity remains lost."),
      "fiscal" => new("vox.meridian",
        $"Spend while cheap — {Truncate(detail)}. Finance will complain next quarter."),
      "grounding" => new("vox.james",
        $"Standing closed — {Truncate(detail)}. Uninsured is not brave."),
      "stockout" => new("vox.dock",
        $"Final shelves thin — {Truncate(detail)}. Can she carry it? Does anyone still owe her?"),
      "mega" => new("vox.bulk",
        $"Bulk River Slow — {Truncate(detail)}. We move what we were built to move."),
      "venture" => new("vox.ixa",
        $"New owner-master on the board — {Truncate(detail)}. Every first contract is a test."),
      "burnout" or "overhaul" or "overhaul-forced" or "overhaul-due" => new("vox.james",
        $"Drive life — {Truncate(detail)}. Twelve hurts; bomb-edging is not a plan."),
      "empty-berth" => new("vox.torrik",
        "If the berth is empty, the formal plan already failed. Hallway expected feet; got none."),
      "ugly-standby" => new("vox.ixa",
        $"Ugly money — {Truncate(detail)}. Means the job is ugly or the person is expensive."),
      "known-responsive" => new("vox.meridian",
        $"Listed → known responsive — {Truncate(detail)}. That creates future work."),
      "escrow" => new("vox.cca",
        $"Escrow weather — {Truncate(detail)}. Payment waits for confirmation."),
      "jump-refuse" => new("vox.james",
        $"Jump band refuse — {Truncate(detail)}. Dense sprint is not a lifestyle."),
      "standby-pass" => new("vox.meridian",
        $"Opportunities window closed — {Truncate(detail)}. Refusal is not a premium event."),
      "lien" => new("vox.cca",
        $"Hull lien — {Truncate(detail)}. Debt follows the registry name."),
      "berth-fee" => new("vox.dock",
        $"Port standing — {Truncate(detail)}. Fee-heavy hubs eat small balances."),
      "upgrade" => new("vox.ledger",
        $"Capacity up — {Truncate(detail)}. Ops cash paid; Core still its own story."),
      "default" => new("vox.cca",
        $"Obligation bite — {Truncate(detail)}. Debt can follow the hull."),
      _ => new("vox.ledger", Truncate(detail)),
    };
  }

  public static IEnumerable<Line> SessionOverture(ulong seed, long hours, bool drama)
  {
    var days = hours / 24;
    yield return new("vox.ledger",
      $"Seed {seed}. {days}d. Drama {(drama ? "on" : "off")}. Ops and Core never summed.");
    yield return new("vox.cca",
      "Job boards behind glass. Plenty of opportunity. All of it locked behind registration.");
    yield return new("vox.varr",
      "Watch the Priority column. That is where premiums go to hunt.");
    if (drama)
    {
      yield return new("vox.torrik",
        "Soft pickup ready. If a berth goes empty, that is not elegance — that is failure.");
    }
  }

  public static Line SessionCurtain(int milestoneCount, int lifeMoments)
  {
    if (lifeMoments <= 0)
    {
      return new("vox.varr",
        "Quiet run. Which bill became less dangerous — or did none need to?");
    }

    return new("vox.varr",
      $"We moved. {lifeMoments} life moments in {milestoneCount} beats. Because we moved, others could stay.");
  }

  public static string Format(Line line) => $"[{line.Voice}] {line.Text}";

  private static string Truncate(string s, int max = 72)
  {
    s = s.Trim();
    return s.Length <= max ? s : s[..(max - 1)] + "…";
  }
}
