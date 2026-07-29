using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class MeshDualTtlTests
{
  [Test]
  public async Task Local_Ttl_Drops_Node_Cache_Before_Global()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var id = PacketId.From(Guid.Parse("44444444-4444-4444-8444-444444444401"));
    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToPlace(MeshTestGraph.Wolf),
      priority: 2,
      globalTtlHours: 100,
      localTtlHours: 3,
      id: id);

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Sol)).IsTrue();
    await Assert.That(state.TryGetCacheEntry(id, MeshTestGraph.Sol, out var entry)).IsTrue();
    await Assert.That(entry.ReceivedHour).IsEqualTo(0);

    // Advance past local TTL from receive; packet object must remain (global not reached).
    for (var i = 0; i < 3; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
    }

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Sol)).IsFalse();
    await Assert.That(state.Packets.ContainsKey(MeshState.PacketKey(id))).IsTrue();
    await Assert.That(state.Stats.LocalCacheDrops).IsGreaterThan(0);
  }

  [Test]
  public async Task Global_Ttl_Is_Earliest_Universal_Removal()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var id = PacketId.From(Guid.Parse("44444444-4444-4444-8444-444444444402"));
    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.NewsGeneral),
      priority: 1,
      globalTtlHours: 5,
      localTtlHours: 50,
      id: id);

    for (var i = 0; i < 5; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
    }

    await Assert.That(state.Packets.ContainsKey(MeshState.PacketKey(id))).IsFalse();
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Sol)).IsFalse();
    await Assert.That(state.Stats.GlobalPacketDrops).IsGreaterThan(0);
  }

  [Test]
  public async Task Local_Priority_Drops_Lowest_Under_Cache_Cap()
  {
    var state = MeshTestGraph.Triangle(
      new MeshPolicy(LossEveryNth: 0, MaxPacketsPerNodeCache: 1),
      bandwidth: 32);

    var low = PacketId.From(Guid.Parse("44444444-4444-4444-8444-444444444403"));
    var high = PacketId.From(Guid.Parse("44444444-4444-4444-8444-444444444404"));

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.NewsPrices),
      priority: 1,
      localRetentionPriority: 1,
      id: low);
    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.Emergency),
      priority: 10,
      localRetentionPriority: 10,
      id: high);

    await Assert.That(state.IsVisibleAt(low, MeshTestGraph.Sol)).IsFalse();
    await Assert.That(state.IsVisibleAt(high, MeshTestGraph.Sol)).IsTrue();
    await Assert.That(state.TryGetCacheEntry(high, MeshTestGraph.Sol, out var kept)).IsTrue();
    await Assert.That(kept.LocalPriority).IsEqualTo(10);
  }
}
