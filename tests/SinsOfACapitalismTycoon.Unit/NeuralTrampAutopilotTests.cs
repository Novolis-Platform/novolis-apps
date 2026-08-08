using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class NeuralTrampAutopilotTests
{
  [Test]
  public async Task Random_neural_brain_has_expected_shape()
  {
    var brain = NeuralCaptainBrain.CreateRandom("unit", new Random(7));
    await Assert.That(brain.Network.InputSize).IsEqualTo(NeuralCaptainBrain.InputSize);
    await Assert.That(brain.Network.OutputSize).IsEqualTo(NeuralCaptainBrain.OutputSize);

    Span<double> inputs = stackalloc double[NeuralCaptainBrain.InputSize];
    inputs.Fill(0.5);
    var eval = brain.Network.Evaluate(inputs);
    var action = NeuralCaptainCodec.DecodeAction(eval.Output);
    await Assert.That(Enum.IsDefined(action)).IsTrue();
  }

  [Test]
  public async Task Neural_autopilot_advances_without_throwing()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 99,
      hours: 3L * 24,
      drama: false,
      playerControl: true,
      autopilot: true,
      localBoard: true);
    NeuralAutopilotBootstrap.ApplyIfRequested(session, neuralAutopilot: true);
    session.Player.Attention = DecisionAttention.RunAlways;
    session.PauseMode = CaptainPauseMode.Never;

    await session.AdvanceHoursAsync(3L * 24, quiet: true);
    await Assert.That(session.Player.NeuralBrain).IsNotNull();
    var bridge = session.CaptureBridge();
    await Assert.That(bridge.CashLine.Length).IsGreaterThan(0);
  }

  /// <summary>Evolutionary trainer smoke (~8s). Opt-in only so Platform.slnx stays fast.</summary>
  [Test]
  [Explicit]
  public async Task Tiny_evolution_produces_champion_fitness()
  {
    var result = await EvolutionaryNeuralTrampTrainer.TrainAsync(
      populationSize: 4,
      generations: 2,
      episodeHours: 2L * 24,
      baseSeed: 2026);

    await Assert.That(result.Champion).IsNotNull();
    await Assert.That(result.BestPerGeneration.Count).IsEqualTo(2);
    await Assert.That(double.IsFinite(result.BestFitness)).IsTrue();
  }

  [Test]
  public async Task PickLegalAction_masks_empty_depart()
  {
    double[] scores = [0.1, 0.2, 0.3, 9.0, 0.4]; // Depart screams loudest
    var picked = NeuralCaptainCodec.PickLegalAction(
      scores,
      canDepart: false,
      canAcceptLocal: false,
      canSteamRumor: false);
    await Assert.That(picked is NeuralCaptainCodec.ActionKind.Wait
                      or NeuralCaptainCodec.ActionKind.Stabilize).IsTrue();
  }

  [Test]
  public async Task Soft_fail_does_not_gate_neural_autopilot()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 55,
      hours: 2L * 24,
      drama: false,
      playerControl: true,
      autopilot: true,
      localBoard: true);
    NeuralAutopilotBootstrap.ApplyIfRequested(session, neuralAutopilot: true);
    session.PauseMode = CaptainPauseMode.Never;
    session.Player.Attention = DecisionAttention.RunAlways;
    session.Player.SoftFailRaised = true;

    await Assert.That(session.NeedsPlayerDecision()).IsFalse();

    var run = session.RunAsync(quiet: true, story: false);
    var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));
    await Assert.That(ReferenceEquals(finished, run)).IsTrue();
    await run;
    await Assert.That(session.IsComplete).IsTrue();
  }
}
