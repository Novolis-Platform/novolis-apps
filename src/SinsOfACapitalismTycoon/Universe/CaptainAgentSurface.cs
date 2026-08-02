using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Attributed contract for the Captain's Bridge agent surface (HTTP + LocalIpc + TCP).</summary>
[AgentSurface("sins", HttpPort = 18765, TcpPort = 18766, EnableEnv = "NOVOLIS_GAME_SESSION", MarkerPrefix = "novolis-game-session")]
public interface ICaptainAgentSurface : IAgentHost;

public static class CaptainAgentSurfaceContract
{
    public static AgentSurfaceDefinition Definition { get; } = AgentSurfaceDefinition.From<ICaptainAgentSurface>();
}
