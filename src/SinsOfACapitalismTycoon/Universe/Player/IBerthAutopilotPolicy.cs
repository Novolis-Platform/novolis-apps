using Novolis.Economy.Agents;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Berth / solvency autopilot policy — <see cref="PlayerTrampAgent"/> only executes orders.</summary>
internal interface IBerthAutopilotPolicy
{
  /// <summary>True when an order was queued this tick.</summary>
  bool Tick(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state);
}

/// <summary>Rule SurvivalCaptain wrapped as a berth policy component.</summary>
internal sealed class SurvivalBerthPolicy : IBerthAutopilotPolicy
{
  public static SurvivalBerthPolicy Instance { get; } = new();

  public bool Tick(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state) =>
    SurvivalCaptain.Tick(agent, context, ids, state);
}

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
