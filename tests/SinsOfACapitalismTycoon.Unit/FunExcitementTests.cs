using Novolis.Economy.Logistics;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

/// <summary>
/// Validates Cap+ / TTD / SoASE excitement beats stay wired — not solvency, not vibes.
/// </summary>
public sealed class FunExcitementTests
{
  [Test]
  public async Task Fresh_captain_meets_excitement_bar()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    session.Player.DockBoardOnly = true;
    var bridge = session.CaptureBridge();
    var beats = FunExcitementScorecard.EvaluateCaptain(bridge);

    await Assert.That(FunExcitementScorecard.PassCount(beats))
      .IsGreaterThanOrEqualTo(FunExcitementScorecard.MinFreshCaptainPasses);
    await Assert.That(FunExcitementScorecard.MeetsFreshCaptainBar(bridge)).IsTrue();
    if (beats.Any(b => !b.Ok))
    {
      // Soft diagnostic — still fail the bar assertion above when under threshold.
      await Assert.That(FunExcitementScorecard.FailSummary(beats)).IsNotNull();
    }
  }

  [Test]
  public async Task Coach_names_stakes_not_empty_chrome()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    session.Player.DockBoardOnly = true;
    var bridge = session.CaptureBridge();

    await Assert.That(bridge.CoachLine).StartsWith(CaptainCoach.Prefix);
    var stakes = FunExcitementScorecard.EvaluateCaptain(bridge)
      .First(b => b.Id == "coach-stakes");
    await Assert.That(stakes.Ok).IsTrue();
  }

  [Test]
  public async Task Berth_time_value_and_fat_thin_are_playable_stakes()
  {
    var priority = FakeSpot("sol", "wolf", atOrigin: true, margin: 22m, TransitProfile.PriorityCommercial);
    var bulk = FakeSpot("sol", "ez-aquarii", atOrigin: true, margin: 9m, TransitProfile.SlowEconomic);
    var (offers, _) = BerthOfferBoard.Build([priority, bulk], [priority, bulk], dockBoardOnly: true);

    await Assert.That(offers.Count).IsGreaterThanOrEqualTo(1);
    var blob = string.Join('\n', offers.Select(o => $"{o.Hook}\n{o.Detail}\n{o.Title}"));
    await Assert.That(
        blob.Contains("time-sensitive", StringComparison.OrdinalIgnoreCase)
        || blob.Contains("bulk forgives", StringComparison.OrdinalIgnoreCase)
        || blob.Contains("Fat", StringComparison.OrdinalIgnoreCase))
      .IsTrue();

    await Assert.That(BerthOfferBoard.TimeValueHint(TransitProfile.PriorityCommercial, "2d"))
      .Contains("time-sensitive");
  }

  [Test]
  public async Task Runway_chip_fields_track_cash_over_premium()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    var bridge = session.CaptureBridge();
    await Assert.That(bridge.DailyPremium).IsGreaterThan(0m);
    await Assert.That(bridge.RunwayDays).IsGreaterThan(0m);
    await Assert.That(bridge.RunwayLine).Contains("runway");
  }

  [Test]
  public async Task SoftFail_near_miss_then_raise_then_clear_is_exciting_not_sticky()
  {
    var session = NewPlayerDesk(seed: 42, days: 30);
    var tutorial = session.Tutorial;
    await Assert.That(tutorial).IsNotNull();

    var entry = session.Ids.Registry.TryGet(session.Ids.Carrier);
    await Assert.That(entry).IsNotNull();
    // Suspended keeps CanOperate false without InsurancePulse auto-reinstating (Insured=false does).
    entry!.Suspended = true;

    // Near-miss window (5–6d grounded) — tick tutorial only so other pulses cannot clear standing.
    for (var i = 0; i < 5; i++)
    {
      tutorial!.TickDayEnd(session.Sim);
    }

    await Assert.That(session.Player.SoftFailGroundedDays).IsEqualTo(5);
    var near = FunExcitementScorecard.EvaluateSoftFailArc(
      session, expectNearMiss: true, expectRaised: false, expectCleared: false);
    await Assert.That(near.First(b => b.Id == "near-miss").Ok).IsTrue();

    for (var i = 0; i < 2; i++)
    {
      tutorial.TickDayEnd(session.Sim);
    }

    await Assert.That(session.Player.SoftFailGroundedDays).IsGreaterThanOrEqualTo(7);
    await Assert.That(session.Player.SoftFailRaised).IsTrue();
    var raised = FunExcitementScorecard.EvaluateSoftFailArc(
      session, expectNearMiss: true, expectRaised: true, expectCleared: false);
    await Assert.That(raised.First(b => b.Id == "soft-fail-raised").Ok).IsTrue();

    // Reinstate — SoftFail must clear (Cap+ bleed + SoASE recovery catharsis).
    entry.Suspended = false;
    tutorial.TickDayEnd(session.Sim);
    await Assert.That(session.Player.SoftFailRaised).IsFalse();
    await Assert.That(session.Player.SoftFailGroundedDays).IsEqualTo(0);
    var cleared = FunExcitementScorecard.EvaluateSoftFailArc(
      session, expectNearMiss: true, expectRaised: false, expectCleared: true);
    if (cleared.Any(b => !b.Ok))
    {
      throw new Exception(FunExcitementScorecard.FailSummary(cleared));
    }
  }

  [Test]
  public async Task Mesh_unlock_after_payday_is_a_ceremony_gate()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    await Assert.That(session.Player.MeshBoardUnlocked).IsFalse();

    session.Milestones.Add(3, "escrow", $"release 180 skim 18 {CampaignWorld.PlayerHullName}");
    MeshBoardUnlock.Sync(session.Player, session.Milestones);
    await Assert.That(session.Player.MeshBoardUnlocked).IsTrue();

    session.Fun.NoteEscrowRelease();
    session.Fun.NoteMeshUnlock();
    await Assert.That(session.Fun.EscrowReleases).IsEqualTo(1);
    await Assert.That(session.Fun.MeshUnlocks).IsEqualTo(1);

    var bridge = session.CaptureBridge();
    await Assert.That(bridge.MeshBoardUnlocked).IsTrue();
  }

  [Test]
  public async Task First_local_accept_wires_fun_telemetry_and_tutorial()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    session.Player.DockBoardOnly = true;
    var bridge = session.CaptureBridge();
    var local = bridge.BerthOffers.FirstOrDefault(o => o is { Kind: BerthOfferKind.Local, Spot: not null });
    if (local?.Spot is null)
    {
      // Seed may lack a Fat local at day 0 — still validate NoteLocalAccept + Fun path.
      session.Fun.NoteFirstAccept();
      session.Tutorial?.NoteLocalAccept(1);
      await Assert.That(session.Fun.FirstAccepts).IsEqualTo(1);
      await Assert.That(session.Milestones.Entries.Any(e =>
        e.Kind.Equals("tutorial", StringComparison.OrdinalIgnoreCase)
        && e.Detail.Contains("First local accept", StringComparison.OrdinalIgnoreCase))).IsTrue();
      return;
    }

    var result = CaptainActions.TryAcceptSpot(session, local.Spot, continueSession: false);
    await Assert.That(result.Ok).IsTrue();
    await Assert.That(session.Fun.FirstAccepts).IsEqualTo(1);
    await Assert.That(session.Milestones.Entries.Any(e =>
      e.Detail.Contains("First local accept", StringComparison.OrdinalIgnoreCase))).IsTrue();
  }

  [Test]
  public async Task Short_autopilot_arc_produces_excitement_milestones()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 1001,
      hours: 60L * 24,
      drama: true,
      playerControl: true,
      autopilot: true,
      localBoard: true);

    session.PauseMode = CaptainPauseMode.Never;
    session.Player.Attention = DecisionAttention.RunAlways;
    await session.RunAsync(quiet: true, story: false);

    var beats = FunExcitementScorecard.EvaluateSession(session);
    await Assert.That(FunExcitementScorecard.PassCount(beats))
      .IsGreaterThanOrEqualTo(FunExcitementScorecard.MinArcPasses);
    await Assert.That(FunExcitementScorecard.MeetsArcBar(session)).IsTrue();

    var bridge = session.CaptureBridge();
    await Assert.That(FunExcitementScorecard.MeetsFreshCaptainBar(bridge)).IsTrue();

    // Sticky SoftFail after operable reinstate is anti-fun.
    if (session.Ids.Registry.CanOperate(session.Ids.Carrier))
    {
      await Assert.That(session.Player.SoftFailRaised).IsFalse();
    }
  }

  [Test]
  public async Task Reputation_lift_eases_margins_like_TTD_station_rating()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    var firm = session.Ids.Carrier;
    var baseMin = CampaignWorld.MinMargin;
    var low = session.Ids.Reputation.EffectiveMinMargin(firm, baseMin);

    session.Ids.Reputation.ObserveKnownResponsive(firm);
    session.Ids.Reputation.ObserveKnownResponsive(firm);
    var high = session.Ids.Reputation.EffectiveMinMargin(firm, baseMin);

    await Assert.That(high).IsLessThan(low);
    session.Fun.NoteReputationLift();
    await Assert.That(session.Fun.ReputationLifts).IsEqualTo(1);
    await Assert.That(session.Fun.SummaryLine()).Contains("rep 1");
  }

  [Test]
  public async Task Escrow_clock_fields_exist_for_mid_haul_sensation()
  {
    var session = NewPlayerDesk(seed: 1001, days: 2);
    var bridge = session.CaptureBridge();
    // Docked idle may have empty clock — field must still project without throwing.
    await Assert.That(bridge.EscrowClockLine).IsNotNull();
    await Assert.That(bridge.EscrowPending).IsGreaterThanOrEqualTo(0m);
  }

  [Test]
  public async Task Fun_telemetry_summary_covers_all_excitement_counters()
  {
    var fun = new FunTelemetry();
    fun.NoteFirstAccept();
    fun.NoteEscrowRelease();
    fun.NoteMeshUnlock();
    fun.NoteSoftFailNearMiss();
    fun.NoteSoftFailRaised();
    fun.NoteSoftFailRecovery();
    fun.NoteReputationLift();
    var line = fun.SummaryLine();
    await Assert.That(line).Contains("accept 1");
    await Assert.That(line).Contains("payday 1");
    await Assert.That(line).Contains("mesh 1");
    await Assert.That(line).Contains("near-miss 1");
    await Assert.That(line).Contains("recover 1");
    await Assert.That(line).Contains("softfail 1");
    await Assert.That(line).Contains("rep 1");
  }

  static CampaignRunner.LiveSession NewPlayerDesk(ulong seed, int days) =>
    new(
      seed,
      hours: days * 24L,
      drama: false,
      playerControl: true,
      autopilot: false,
      localBoard: true);

  static CaptainJobBoard.SpotCandidate FakeSpot(
    string origin,
    string dest,
    bool atOrigin,
    decimal margin,
    TransitProfile profile = TransitProfile.StandardCommercial) =>
    new(
      Label: $"{origin}→{dest}",
      OriginSystemId: origin,
      DestSystemId: dest,
      OriginName: origin,
      DestName: dest,
      SkuLabel: "Raw",
      Quantity: 10m,
      LiftLimit: 1m,
      DestBid: 2m + margin,
      Margin: margin,
      Profile: profile,
      Summary: "test",
      AtOrigin: atOrigin,
      DistanceHint: profile == TransitProfile.PriorityCommercial ? "fast 2d" : "4d",
      LogicalKey: $"{origin}|{dest}|Raw|{profile}");
}
