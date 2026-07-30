namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Wire a dense captain onto a live session (NeuralRacing-style policy).</summary>
internal static class NeuralAutopilotBootstrap
{
  public static void ApplyIfRequested(CampaignRunner.LiveSession session, bool neuralAutopilot)
  {
    if (!neuralAutopilot)
    {
      return;
    }

    session.Player.Autopilot = true;
    session.Player.NeuralBrain ??= NeuralCaptainBrain.CreateRandom(
      $"calypso-neural-{session.Sim.State.Seed}",
      new Random(unchecked((int)session.Sim.State.Seed)));
    // Live horizons use SurvivalCaptain until a trained champion sets AllowNetworkControl.
    session.Player.NeuralBrain.AllowNetworkControl = false;
  }
}
