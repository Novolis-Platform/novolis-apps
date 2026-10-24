using Novolis.Agent.Core;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class CaptainAgentProtocolTests
{
  [Test]
  public async Task Captain_profile_keeps_play_ungated_and_replays_a_revision()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 1001,
      hours: 1,
      drama: false,
      playerControl: true,
      autopilot: false,
      localBoard: false);
    session.Player.Attention = DecisionAttention.RunAlways;
    session.PauseMode = CaptainPauseMode.Never;
    var bridge = new CaptainBridgeService(session);

    var snapshot = bridge.Snapshot();
    await Assert.That(snapshot.StateRevision).IsEqualTo(1);
    await Assert.That(snapshot.MetadataVersion).IsEqualTo(AgentMetadata.Version);
    var travel = bridge.Actions().Actions.Single(action => action.Id == AgentActionIds.Travel);
    await Assert.That(travel.Capability).IsEqualTo("captain.play");
    await Assert.That(travel.ApprovalRequired).IsFalse();
    await Assert.That(travel.EffectPreview.Length).IsGreaterThan(0);

    var missing = bridge.Execute(new AgentCommand { ActionId = AgentActionIds.Travel });
    await Assert.That(missing.ErrorCode).IsEqualTo("invalid-arguments");
    await Assert.That(bridge.Snapshot().StateRevision).IsEqualTo(1);

    var stale = bridge.Execute(new AgentCommand
    {
      ActionId = AgentActionIds.Wait,
      CorrelationId = "stale",
      ExpectedRevision = 99,
    });
    await Assert.That(stale.ErrorCode).IsEqualTo("stale-snapshot");
    await Assert.That(bridge.Snapshot().StateRevision).IsEqualTo(1);

    var applied = bridge.Execute(new AgentCommand
    {
      ActionId = AgentActionIds.Wait,
      CorrelationId = "wait-1",
      ExpectedRevision = 1,
    });
    await Assert.That(applied.Ok).IsTrue();
    await Assert.That(applied.Receipt!.Lifecycle).IsEqualTo(AgentLifecycle.Applied);
    await Assert.That(applied.StateRevision).IsEqualTo(2);

    var replay = bridge.Execute(new AgentCommand
    {
      ActionId = AgentActionIds.Wait,
      CorrelationId = "wait-1",
      ExpectedRevision = 1,
    });
    await Assert.That(replay.StateRevision).IsEqualTo(2);
    await Assert.That(replay.Message).IsEqualTo(applied.Message);
    await Assert.That(bridge.Snapshot().StateRevision).IsEqualTo(2);
  }
}
