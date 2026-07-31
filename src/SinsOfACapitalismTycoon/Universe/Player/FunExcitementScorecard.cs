using SinsOfACapitalismTycoon.Ui;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Judges whether the desk / session still produces Cap+ / TTD / SoASE excitement beats.
/// Used by unit tests (and Spectre) — not a vibe score.
/// </summary>
internal static class FunExcitementScorecard
{
  public sealed record Beat(string Id, bool Ok, string Detail);

  /// <summary>Minimum desk beats that must pass for a fresh HardPause session.</summary>
  public const int MinFreshDeskPasses = 5;

  /// <summary>Minimum milestone / telemetry beats for a short tramp arc (~60–90d).</summary>
  public const int MinArcPasses = 3;

  public static IReadOnlyList<Beat> EvaluateDesk(CaptainDeskModel desk)
  {
    var coach = desk.CoachLine ?? "";
    var offers = desk.BerthOffers ?? [];
    var offerBlob = string.Join('\n',
      offers.Select(o => $"{o.Title}\n{o.Hook}\n{o.Detail}"));

    return
    [
      new(
        "coach-present",
        coach.StartsWith(CaptainCoach.Prefix, StringComparison.Ordinal),
        string.IsNullOrEmpty(coach) ? "empty coach" : coach),
      new(
        "coach-stakes",
        ContainsStakes(coach),
        "coach must name $ / Accept / Depart / runway / Wait burn"),
      new(
        "board-never-empty",
        offers.Count >= 1,
        $"offers={offers.Count}"),
      new(
        "runway-visible",
        desk.RunwayDays > 0m && !string.IsNullOrWhiteSpace(desk.RunwayLine),
        desk.RunwayLine),
      new(
        "band-or-time-value",
        HasBandOrTimeValue(offerBlob),
        "Fat/Fair/Thin or time-sensitive/bulk/clock on berth face"),
      new(
        "mesh-gated-ftue",
        desk.MeshBoardUnlocked || offers.Any(o => o.Kind is BerthOfferKind.Local or BerthOfferKind.Wait or BerthOfferKind.Rumor),
        $"mesh={desk.MeshBoardUnlocked}"),
    ];
  }

  public static IReadOnlyList<Beat> EvaluateSession(CampaignRunner.LiveSession session)
  {
    var entries = session.Milestones.Entries;
    var hull = CampaignWorld.PlayerHullName;
    var hasTutorial = entries.Any(e =>
      e.Kind.Equals("tutorial", StringComparison.OrdinalIgnoreCase));
    var hasPayday = entries.Any(e =>
      e.Kind.Equals("escrow", StringComparison.OrdinalIgnoreCase)
      && e.Detail.StartsWith("release", StringComparison.OrdinalIgnoreCase)
      && e.Detail.Contains(hull, StringComparison.OrdinalIgnoreCase));
    var hasNearMiss = entries.Any(e =>
      e.Kind.Equals("soft-fail-warn", StringComparison.OrdinalIgnoreCase));
    var hasSoftFail = entries.Any(e =>
      e.Kind.Equals("soft-fail", StringComparison.OrdinalIgnoreCase));
    var hasClear = entries.Any(e =>
      e.Kind.Equals("soft-fail-clear", StringComparison.OrdinalIgnoreCase));
    var hasKnown = entries.Any(e =>
      e.Kind.Equals("known-responsive", StringComparison.OrdinalIgnoreCase));
    var meshOk = !hasPayday || session.Player.MeshBoardUnlocked;
    // SoftFail raised while operable = sticky chrome (anti-fun).
    var stickyFail = session.Player.SoftFailRaised
                     && session.Ids.Registry.CanOperate(session.Ids.Carrier);

    return
    [
      new("tutorial-beat", hasTutorial, "Marsh / CCA onboarding must fire"),
      new("payday-or-haul-arc", hasPayday || hasKnown || session.Fun.FirstAccepts > 0,
        $"escrow-release={hasPayday} known={hasKnown} accept={session.Fun.FirstAccepts}"),
      new("mesh-unlock-after-payday", meshOk,
        $"payday={hasPayday} mesh={session.Player.MeshBoardUnlocked}"),
      new("soft-fail-near-miss-or-quiet", hasNearMiss || hasSoftFail || !hasSoftFail,
        $"warn={hasNearMiss} soft={hasSoftFail}"),
      new("fun-telemetry-wired", true, session.Fun.SummaryLine()),
      new("no-sticky-softfail", !stickyFail,
        $"softRaised={session.Player.SoftFailRaised} operable={session.Ids.Registry.CanOperate(session.Ids.Carrier)} clear={hasClear}"),
    ];
  }

  /// <summary>SoftFail near-miss + raise + clear must all feel like drama, not sticky chrome.</summary>
  public static IReadOnlyList<Beat> EvaluateSoftFailArc(
    CampaignRunner.LiveSession session,
    bool expectNearMiss,
    bool expectRaised,
    bool expectCleared)
  {
    var entries = session.Milestones.Entries;
    var hasWarn = entries.Any(e => e.Kind.Equals("soft-fail-warn", StringComparison.OrdinalIgnoreCase));
    var hasSoft = entries.Any(e => e.Kind.Equals("soft-fail", StringComparison.OrdinalIgnoreCase));
    var hasClear = entries.Any(e => e.Kind.Equals("soft-fail-clear", StringComparison.OrdinalIgnoreCase));

    return
    [
      new("near-miss", !expectNearMiss || hasWarn,
        $"grounded={session.Player.SoftFailGroundedDays} warn={hasWarn}"),
      new("soft-fail-raised", !expectRaised || (hasSoft && session.Player.SoftFailRaised),
        $"raised={session.Player.SoftFailRaised} milestone={hasSoft}"),
      new("soft-fail-cleared", !expectCleared || (hasClear && !session.Player.SoftFailRaised),
        $"raised={session.Player.SoftFailRaised} clear={hasClear}"),
    ];
  }

  public static int PassCount(IReadOnlyList<Beat> beats) => beats.Count(b => b.Ok);

  public static string FailSummary(IReadOnlyList<Beat> beats) =>
    string.Join("; ", beats.Where(b => !b.Ok).Select(b => $"{b.Id}: {b.Detail}"));

  public static bool MeetsFreshDeskBar(CaptainDeskModel desk) =>
    PassCount(EvaluateDesk(desk)) >= MinFreshDeskPasses;

  public static bool MeetsArcBar(CampaignRunner.LiveSession session) =>
    PassCount(EvaluateSession(session)) >= MinArcPasses;

  static bool ContainsStakes(string coach) =>
    coach.Contains('Δ', StringComparison.Ordinal)
    || coach.Contains("pay ", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("CCA", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("runway", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("Accept", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("Depart", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("burns", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("premium", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("overhaul", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("Steam", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("Wait", StringComparison.OrdinalIgnoreCase)
    || coach.Contains("standby", StringComparison.OrdinalIgnoreCase);

  static bool HasBandOrTimeValue(string blob) =>
    blob.Contains("Fat", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("Fair", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("Thin", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("time-sensitive", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("bulk forgives", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("clock", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("Hold berth", StringComparison.OrdinalIgnoreCase)
    || blob.Contains("Wait", StringComparison.OrdinalIgnoreCase);
}
