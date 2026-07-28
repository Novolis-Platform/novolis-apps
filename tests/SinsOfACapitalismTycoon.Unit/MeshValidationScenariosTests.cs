using SinsOfACapitalismTycoon.Universe.Mesh;

namespace SinsOfACapitalismTycoon.Unit;

/// <summary>BM validation scenarios for the Confederation mesh kernel.</summary>
public sealed class MeshValidationScenariosTests
{
  [Test]
  public async Task Directed_Sol_To_Wolf_Visible_At_Dest_Not_Unrelated_Early()
  {
    var state = MeshTestGraph.Triangle();
    var id = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111101"));
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToPlace(MeshTestGraph.Wolf), priority: 2, id: id);
    state = LaunchEngine.LaunchPending(state);

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Sol)).IsTrue();
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsFalse();
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Other)).IsFalse();

    // Pulse hop Sol→Wolf is 2 hours
    state = DefaultMeshPipeline.Advance(state);
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsFalse();
    state = DefaultMeshPipeline.Advance(state);
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsTrue();
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Other)).IsFalse();
    InvariantChecker.AssertAll(state);
  }

  [Test]
  public async Task Identity_Flood_Eventually_Visible_Everywhere()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var identity = MeshIdentityId.From("ship:calypso");
    state = MeshBridge.RegisterIdentity(state, identity, MeshTestGraph.Sol);
    var id = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111102"));
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToIdentity(identity), priority: 1, id: id);

    for (var i = 0; i < 40; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
    }

    foreach (var hub in state.Hubs.Values)
    {
      await Assert.That(state.IsVisibleAt(id, hub.Id)).IsTrue();
    }

    await Assert.That(state.IsInMailbox(id, identity)).IsTrue();
    InvariantChecker.AssertAll(state);
  }

  [Test]
  public async Task Offline_Identity_Mailbox_Holds_Without_Delivered_Api()
  {
    var state = MeshTestGraph.Triangle();
    var identity = MeshIdentityId.From("offline-captain");
    // No LastKnownHub — still mailboxed on publish
    state = MeshBridge.RegisterIdentity(state, identity);
    var id = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111103"));
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToIdentity(identity), id: id);

    await Assert.That(state.IsInMailbox(id, identity)).IsTrue();
    // Reflect: MeshState has no IsDelivered — visibility only
    await Assert.That(typeof(MeshState).GetMethod("IsDelivered")).IsNull();
  }

  [Test]
  public async Task Bandwidth_Flood_Defers_Lower_Priority()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 1);
    var hi = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111104"));
    var lo = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111105"));

    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToPublic(), priority: 10, id: hi);
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToPlace(MeshTestGraph.Wolf), priority: 1, id: lo);

    // FloodDispatch + LaunchPending once without hour advance
    state = FloodEngine.Dispatch(state);
    state = LaunchEngine.LaunchPending(state);

    await Assert.That(state.Drones.Length).IsEqualTo(1);
    await Assert.That(state.Drones[0].PacketId).IsEqualTo(hi);
    await Assert.That(state.Pending.Length).IsGreaterThan(0);
    await Assert.That(state.Stats.BandwidthDeferred).IsGreaterThan(0);
  }

  [Test]
  public async Task Loss_And_Retry_Still_Converges_To_Visibility()
  {
    PacketId? cursed = null;
    for (var i = 0; i < 10_000; i++)
    {
      var candidate = PacketId.From(Guid.Parse($"22222222-2222-4222-8222-{i:D12}"));
      if ((HashCode.Combine(candidate.Value) & int.MaxValue) % 2 == 0)
      {
        cursed = candidate;
        break;
      }
    }

    await Assert.That(cursed).IsNotNull();

    var state = MeshTestGraph.Triangle(
      new MeshPolicy(LossEveryNth: 2, MaxLossesPerPacket: 1),
      bandwidth: 16);
    var id = cursed!.Value;
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToPlace(MeshTestGraph.Wolf), priority: 3, id: id);

    for (var i = 0; i < 40; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
      if (state.IsVisibleAt(id, MeshTestGraph.Wolf))
      {
        break;
      }
    }

    await Assert.That(state.Stats.DronesLost).IsEqualTo(1);
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsTrue();
    InvariantChecker.AssertAll(state);
  }
}
