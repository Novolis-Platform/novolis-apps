using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class AutopilotHorizonTests
{
  [Test]
  public async Task Neural_autopilot_survives_1000_days()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 1001,
      hours: 1000L * 24,
      drama: false,
      playerControl: true,
      autopilot: true,
      localBoard: true);

    NeuralAutopilotBootstrap.ApplyIfRequested(session, neuralAutopilot: true);
    // RunAsync (not warm) so PreferMaxSpeedThroughput can skip NPC/mesh flood work.
    session.PauseMode = CaptainPauseMode.Never;
    session.Player.Attention = DecisionAttention.RunAlways;
    session.Player.SimSpeedScale = 1.0;

    await session.RunAsync(quiet: true, story: false);

    var desk = session.CaptureDesk();
    var cash = ParseCash(desk.CashLine);

    await Assert.That(session.IsComplete).IsTrue();
    await Assert.That(desk.Day).IsGreaterThanOrEqualTo(998);
    if (cash < 500m)
    {
      throw new Exception(
        $"horizon broke: d{desk.Day} cash={cash} hull={desk.HullLine} decision={desk.DecisionLine} soft={session.Player.SoftFailRaised} coach={desk.CoachLine}");
    }

    await Assert.That(cash).IsGreaterThanOrEqualTo(500m);
    await Assert.That(desk.HullLine.Contains("uninsured", StringComparison.OrdinalIgnoreCase)).IsFalse();
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
