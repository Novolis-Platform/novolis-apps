using Novolis.Economy.Logistics;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class BerthFantasyTests
{
  [Test]
  public async Task Margin_bands_match_campaign_thresholds()
  {
    await Assert.That(BerthOfferBoard.MarginBand(0m)).IsEqualTo("None");
    await Assert.That(BerthOfferBoard.MarginBand(7.9m)).IsEqualTo("Thin");
    await Assert.That(BerthOfferBoard.MarginBand(CampaignWorld.BerthMarginFair)).IsEqualTo("Fair");
    await Assert.That(BerthOfferBoard.MarginBand(CampaignWorld.BerthMarginFat)).IsEqualTo("Fair");
    await Assert.That(BerthOfferBoard.MarginBand(CampaignWorld.BerthMarginFat + 0.1m)).IsEqualTo("Fat");
  }

  [Test]
  public async Task Dock_board_never_empty_uses_rumors_or_wait()
  {
    var local = FakeSpot("sol", "ez-aquarii", atOrigin: true, margin: 12m);
    var remote = FakeSpot("ez-aquarii", "sol", atOrigin: false, margin: 15m);

    var (withLocal, spots) = BerthOfferBoard.Build(
      [local], [local, remote], dockBoardOnly: true);
    await Assert.That(withLocal.Count).IsGreaterThanOrEqualTo(1);
    await Assert.That(withLocal[0].Kind).IsEqualTo(BerthOfferKind.Local);
    await Assert.That(spots.Count).IsEqualTo(withLocal.Count(o => o.Spot is not null));

    var (barren, barrenSpots) = BerthOfferBoard.Build(
      [], [remote], dockBoardOnly: true);
    await Assert.That(barren.Count).IsEqualTo(1);
    await Assert.That(barren[0].Kind).IsEqualTo(BerthOfferKind.Rumor);
    await Assert.That(barrenSpots.Count).IsEqualTo(1);

    var (empty, emptySpots) = BerthOfferBoard.Build([], [], dockBoardOnly: true);
    await Assert.That(empty.Count).IsEqualTo(1);
    await Assert.That(empty[0].Kind).IsEqualTo(BerthOfferKind.Wait);
    await Assert.That(emptySpots.Count).IsEqualTo(0);
  }

  [Test]
  public async Task Mesh_board_empty_is_wait_not_live_fill()
  {
    var remote = FakeSpot("ez-aquarii", "sol", atOrigin: false, margin: 22m);
    var (offers, spots) = BerthOfferBoard.Build([], [remote], dockBoardOnly: false);
    await Assert.That(offers.Count).IsEqualTo(1);
    await Assert.That(offers[0].Kind).IsEqualTo(BerthOfferKind.Wait);
    await Assert.That(spots.Count).IsEqualTo(0);
  }

  [Test]
  public async Task Mesh_unlock_false_until_calypso_escrow_release()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 42,
      hours: 24,
      drama: false,
      playerControl: true,
      autopilot: false,
      localBoard: true);

    await Assert.That(session.Player.MeshBoardUnlocked).IsFalse();
    MeshBoardUnlock.Sync(session.Player, session.Milestones);
    await Assert.That(session.Player.MeshBoardUnlocked).IsFalse();
    await Assert.That(session.Player.DockBoardOnly).IsTrue();

    session.Milestones.Add(1, "escrow", $"release 100 skim 10 {CampaignWorld.PlayerHullName}");
    MeshBoardUnlock.Sync(session.Player, session.Milestones);
    await Assert.That(session.Player.MeshBoardUnlocked).IsTrue();

    var desk = session.CaptureDesk();
    await Assert.That(desk.MeshBoardUnlocked).IsTrue();
    await Assert.That(desk.BerthOffers.Count).IsGreaterThanOrEqualTo(1);
    await Assert.That(desk.VoyageLine).Contains("Grace");
  }

  [Test]
  public async Task Fresh_desk_projects_berth_offers()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 1001,
      hours: 48,
      drama: false,
      playerControl: true,
      autopilot: false,
      localBoard: true);

    session.Player.DockBoardOnly = true;
    var desk = session.CaptureDesk();
    await Assert.That(desk.MeshBoardUnlocked).IsFalse();
    await Assert.That(desk.BerthOffers.Count).IsGreaterThanOrEqualTo(1);
    await Assert.That(desk.BerthOffers.Count).IsLessThanOrEqualTo(3);
    await Assert.That(desk.CoachLine).StartsWith(CaptainCoach.Prefix);
  }

  static CaptainJobBoard.SpotCandidate FakeSpot(
    string origin,
    string dest,
    bool atOrigin,
    decimal margin) =>
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
      Profile: TransitProfile.StandardCommercial,
      Summary: "test",
      AtOrigin: atOrigin,
      DistanceHint: "2d",
      LogicalKey: $"{origin}|{dest}|Raw");
}
