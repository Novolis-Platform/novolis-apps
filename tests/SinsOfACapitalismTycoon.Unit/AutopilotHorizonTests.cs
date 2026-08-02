using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class AutopilotHorizonTests
{
  const decimal MinSolventCash = 100m;

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

    var bridge = session.CaptureBridge();
    var cash = ParseCash(bridge.CashLine);
    var uninsured = bridge.HullLine.Contains("uninsured", StringComparison.OrdinalIgnoreCase);
    var burnedOut = bridge.HullLine.Contains("burned-out", StringComparison.OrdinalIgnoreCase);

    await Assert.That(session.IsComplete).IsTrue();
    await Assert.That(bridge.Day).IsGreaterThanOrEqualTo(998);

    if (cash < MinSolventCash || session.Player.SoftFailRaised || uninsured || burnedOut)
    {
      throw new Exception(
        $"horizon broke: d{bridge.Day} cash={cash} hull={bridge.HullLine} decision={bridge.DecisionLine} soft={session.Player.SoftFailRaised} coach={bridge.CoachLine}");
    }
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
