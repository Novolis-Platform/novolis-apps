using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace FrankMoat.Agent;

[AgentSurface(
    "frank-moat",
    Description = "Local gameplay surface for observing and controlling the Frank Moat alpha.",
    EnableEnv = "NOVOLIS_FRANK_MOAT_AGENT",
    HttpPort = 18885,
    TcpPort = 18886,
    MarkerPrefix = "novolis-frank-moat")]
[AgentAction(
    FrankMoatAgentActions.Control,
    Summary = "Apply movement, aim, and held controls for a short duration.",
    Params = "moveX?; moveZ?; aimX?; aimZ?; duration?; run?; fire?; reload?; secondary?")]
[AgentAction(
    FrankMoatAgentActions.Release,
    Summary = "Release agent-owned held controls.")]
[AgentAction(
    FrankMoatAgentActions.Interact,
    Summary = "Press the interaction control at the current position.")]
[AgentAction(
    FrankMoatAgentActions.Fire,
    Summary = "Press the fire control once.",
    Params = "aimX?; aimZ?; moveX?; moveZ?; duration?; run?")]
[AgentAction(
    FrankMoatAgentActions.Reload,
    Summary = "Press reload.")]
[AgentAction(
    FrankMoatAgentActions.Melee,
    Summary = "Press quick melee.")]
[AgentAction(
    FrankMoatAgentActions.Weapon,
    Summary = "Select a weapon slot.",
    Params = "slot")]
[AgentAction(
    FrankMoatAgentActions.Map,
    Summary = "Toggle the floor map.")]
[AgentAction(
    FrankMoatAgentActions.Inspect,
    Summary = "Toggle inspection mode.")]
[AgentAction(
    FrankMoatAgentActions.Pause,
    Summary = "Toggle pause.")]
[AgentAction(
    FrankMoatAgentActions.Save,
    Summary = "Save a checkpoint.",
    Params = "name?")]
[AgentAction(
    FrankMoatAgentActions.Load,
    Summary = "Restore a checkpoint.",
    Params = "name?")]
[AgentAction(
    FrankMoatAgentActions.ControlMode,
    Summary = "Toggle world-relative and character-oriented movement.")]
[AgentAction(
    FrankMoatAgentActions.Exit,
    Summary = "Request that the host close the game.")]
internal interface IFrankMoatAgentSurface : IAgentHost;
