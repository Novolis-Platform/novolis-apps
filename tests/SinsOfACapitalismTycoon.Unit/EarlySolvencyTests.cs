using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class EarlySolvencyTests
{
  [Test]
  public async Task Idle_dock_through_grace_preserves_lien_and_opening_cash_band()
  {
    var cashFactor = CampaignWorld.PlayerOpeningCashFactor;
    var grace = CampaignWorld.LienServiceGraceDays;
    var idleFactor = CampaignWorld.IdleStandingPremiumFactor;
    await Assert.That(cashFactor).IsEqualTo(0.50m);
    await Assert.That(grace).IsEqualTo(21);
    await Assert.That(idleFactor).IsEqualTo(0.15m);

    var session = new CampaignRunner.LiveSession(
      seed: 1001,
      hours: 40L * 24,
      drama: false,
      playerControl: true,
      autopilot: false,
      localBoard: false);

    session.Player.Attention = DecisionAttention.RunAlways;
    session.PauseMode = CaptainPauseMode.Never;

    var start = session.CaptureDesk();
    var startCash = ParseCash(start.CashLine);
    await Assert.That(startCash).IsEqualTo(9_000m);
    await Assert.That(start.HullLine).Contains("lien 4500");

    await session.AdvanceHoursAsync(21L * 24, quiet: true);
    var mid = session.CaptureDesk();
    await Assert.That(mid.HullLine).Contains("lien 4500");
    await Assert.That(ParseCash(mid.CashLine)).IsGreaterThan(8_000m);

    await session.AdvanceHoursAsync(24, quiet: true);
    var after = session.CaptureDesk();
    await Assert.That(after.HullLine).DoesNotContain("lien 4500");
  }

  static decimal ParseCash(string cashLine)
  {
    var normalized = cashLine.Replace(',', '.');
    return decimal.TryParse(
      normalized,
      System.Globalization.NumberStyles.Number,
      System.Globalization.CultureInfo.InvariantCulture,
      out var v)
      ? v
      : 0m;
  }
}
