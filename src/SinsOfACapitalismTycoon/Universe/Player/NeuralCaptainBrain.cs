using Novolis.MachineLearning.Neural;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Dense-network captain brain (NeuralRacing-style policy for berth bets).</summary>
internal sealed class NeuralCaptainBrain
{
  public const int InputSize = 16;
  public const int OutputSize = 5;
  public static readonly int[] DefaultHidden = [12, 8];

  public NeuralCaptainBrain(IMutableNeuralNetwork network) =>
    Network = network ?? throw new ArgumentNullException(nameof(network));

  public IMutableNeuralNetwork Network { get; }

  public string LastDecision { get; set; } = "neural idle";

  /// <summary>
  /// When false (default), <see cref="NeuralSurvivalCaptain"/> uses SurvivalCaptain —
  /// random nets are for evolution benches, not live horizons.
  /// </summary>
  public bool AllowNetworkControl { get; set; }

  public static NeuralCaptainBrain CreateRandom(string name, Random? random = null) =>
    new(DenseNetwork.Create(name, InputSize, DefaultHidden, OutputSize, random: random));

  public NeuralCaptainBrain Clone(string? name = null) =>
    new(Network.Clone(name)) { AllowNetworkControl = AllowNetworkControl };
}
