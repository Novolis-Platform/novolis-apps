using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace FrankMoat.Agent;

internal sealed class FrankMoatAgentHost(FrankMoatSession session) : IFrankMoatAgentSurface
{
    private readonly FrankMoatSession _session =
        session ?? throw new ArgumentNullException(nameof(session));

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;
#pragma warning restore CS0067

    public AgentHello Hello() =>
        new()
        {
            ProtocolVersion = FrankMoatAgentContract.Definition.ProtocolVersion,
            AppId = FrankMoatAgentContract.Definition.SurfaceId,
            AppTitle = "Frank Moat vs. the Evil Undead",
            ProcessId = Environment.ProcessId,
            SurfaceId = FrankMoatAgentContract.Definition.SurfaceId,
            Description = FrankMoatAgentContract.Definition.Description,
            HttpPort = FrankMoatAgentContract.Definition.DefaultHttpPort,
            TcpPort = FrankMoatAgentContract.Definition.DefaultTcpPort,
            Capabilities = FrankMoatAgentContract.Definition.Methods.ToArray(),
        };

    public AgentSnapshot Snapshot() => _session.CreateAgentSnapshot();

    public AgentActionsResponse Actions() =>
        FrankMoatAgentContract.Definition.BuildActions();

    public AgentCommandResult Execute(AgentCommand command) =>
        _session.ExecuteAgentAction(command);

    public AgentCommandResult Continue() =>
        new()
        {
            Ok = true,
            ActionId = AgentMethodNames.Continue,
            Message = "Frank Moat has no decision gate.",
            Snapshot = Snapshot(),
        };

    public void Subscribe()
    {
    }
}
