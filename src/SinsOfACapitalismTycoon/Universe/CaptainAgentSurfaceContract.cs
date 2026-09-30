using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace SinsOfACapitalismTycoon.Universe;

public static class CaptainAgentSurfaceContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<ICaptainAgentSurface>();
}
