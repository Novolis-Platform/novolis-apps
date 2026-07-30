using Novolis.MachineLearning.Neural;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Tiny evolutionary harness (NeuralRacing clone) scoring Calypso cash after a short horizon.
/// Not a full opponent trainer — proves DenseNetwork can drive berth autopilot.
/// </summary>
internal static class EvolutionaryNeuralTrampTrainer
{
  public sealed record Result(
    NeuralCaptainBrain Champion,
    double BestFitness,
    IReadOnlyList<double> BestPerGeneration);

  public static async Task<Result> TrainAsync(
    int populationSize = 8,
    int generations = 3,
    long episodeHours = 5L * 24,
    ulong baseSeed = 4242,
    CancellationToken ct = default)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(populationSize, 2);
    ArgumentOutOfRangeException.ThrowIfLessThan(generations, 1);

    var random = new Random(unchecked((int)baseSeed));
    var mutation = new MutationSettings(0.2, 0.15, 0.2, 0.15);
    var population = new NeuralCaptainBrain[populationSize];
    for (var i = 0; i < populationSize; i++)
    {
      population[i] = NeuralCaptainBrain.CreateRandom($"tramp-gen0-{i}", random);
    }

    var bestPerGen = new List<double>(generations);
    NeuralCaptainBrain? bestEver = null;
    var bestFitnessEver = double.NegativeInfinity;

    for (var gen = 0; gen < generations; gen++)
    {
      ct.ThrowIfCancellationRequested();
      var fitness = new double[populationSize];
      for (var i = 0; i < populationSize; i++)
      {
        fitness[i] = await EvaluateAsync(
          population[i],
          seed: baseSeed + (ulong)(gen * 1000 + i),
          episodeHours,
          ct).ConfigureAwait(false);
      }

      var ranked = fitness
        .Select((f, i) => (Index: i, Fitness: f))
        .OrderByDescending(x => x.Fitness)
        .ToArray();

      bestPerGen.Add(ranked[0].Fitness);
      if (ranked[0].Fitness > bestFitnessEver)
      {
        bestFitnessEver = ranked[0].Fitness;
        bestEver = population[ranked[0].Index].Clone($"tramp-champion-gen{gen}");
      }

      var next = new NeuralCaptainBrain[populationSize];
      next[0] = population[ranked[0].Index].Clone(population[ranked[0].Index].Network.Name + "-elite");
      for (var i = 1; i < populationSize; i++)
      {
        var parent = population[ranked[random.Next(Math.Min(3, ranked.Length))].Index];
        var child = parent.Clone($"tramp-gen{gen + 1}-{i}");
        child.Network.Mutate(random, mutation);
        next[i] = child;
      }

      population = next;
    }

    return new Result(
      bestEver ?? population[0],
      bestFitnessEver,
      bestPerGen);
  }

  public static async Task<double> EvaluateAsync(
    NeuralCaptainBrain brain,
    ulong seed,
    long episodeHours,
    CancellationToken ct = default)
  {
    var session = new CampaignRunner.LiveSession(
      seed,
      episodeHours,
      drama: false,
      playerControl: true,
      autopilot: true,
      localBoard: true);

    session.Player.NeuralBrain = brain;
    brain.AllowNetworkControl = true;
    session.Player.Attention = DecisionAttention.RunAlways;
    session.PauseMode = CaptainPauseMode.Never;

    await session.AdvanceHoursAsync(episodeHours, quiet: true, ct).ConfigureAwait(false);

    var cash = session.Sim.State.World.Ledgers.TryGetValue(session.Ids.Carrier, out var led)
      ? (double)led.Cash.Amount
      : 0.0;
    var operable = session.Ids.Registry.CanOperate(session.Ids.Carrier) ? 500.0 : -2_000.0;
    var softFail = session.Player.SoftFailRaised ? -3_000.0 : 0.0;
    return cash + operable + softFail;
  }
}
