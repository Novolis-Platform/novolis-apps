using Novolis.Agent.Surface;

namespace FrankMoat.Agent;

internal static class FrankMoatAgentContract
{
    public static AgentSurfaceDefinition Definition { get; } =
        AgentSurfaceDefinition.From<IFrankMoatAgentSurface>();
}
