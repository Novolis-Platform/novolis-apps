using Novolis.Economy.Agents;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Neural-flagged policy (Survival until a champion enables network control).</summary>
internal sealed class NeuralBerthPolicy : IBerthAutopilotPolicy
{
  public static NeuralBerthPolicy Instance { get; } = new();

  public bool Tick(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state) =>
    NeuralSurvivalCaptain.Tick(agent, context, ids, state);
}
