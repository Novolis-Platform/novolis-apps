using Novolis.Simulation.Mesh;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

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

    state = DefaultMeshPipeline.Advance(state);
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsFalse();
    state = DefaultMeshPipeline.Advance(state);
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsTrue();
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Other)).IsFalse();
    InvariantChecker.AssertAll(state);
  }

  [Test]
  public async Task Identity_Push_Only_When_Mailbox_CoLocated()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var identity = MeshIdentityIds.Ship("calypso");
    // Mailbox parked at Wolf — publish at Sol should not push until packet reaches Wolf.
    state = MailboxEngine.Register(state, identity, MeshTestGraph.Wolf, MeshIdentityKind.Ship);
    var id = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111102"));
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToIdentity(identity), priority: 1, id: id);

    await Assert.That(state.IsInMailbox(id, identity)).IsFalse();
    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Sol)).IsTrue();

    for (var i = 0; i < 40; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
      if (state.IsInMailbox(id, identity))
      {
        break;
      }
    }

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsTrue();
    await Assert.That(state.IsInMailbox(id, identity)).IsTrue();
    InvariantChecker.AssertAll(state);
  }

  [Test]
  public async Task Feed_Pull_Only_Subscribed_Channels()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var identity = MeshIdentityIds.Person("reader");
    state = MailboxEngine.Register(state, identity, MeshTestGraph.Sol, MeshIdentityKind.Person);
    state = FeedEngine.Subscribe(state, identity, MeshFeedId.NewsGeneral);

    var general = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111103"));
    var prices = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111104"));
    var whales = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111105"));
    var emergency = PacketId.From(Guid.Parse("11111111-1111-4111-8111-11111111110a"));

    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToFeed(MeshFeedId.NewsGeneral), id: general);
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToFeed(MeshFeedId.NewsPrices), id: prices);
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToFeed(MeshFeedId.NewsSpaceWhales), id: whales);
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToFeed(MeshFeedId.Emergency), priority: 10, id: emergency);

    // Voluntary feeds do not push into personal mailbox; Emergency force-fills feed inbox.
    await Assert.That(state.IsInMailbox(general, identity)).IsFalse();
    await Assert.That(state.IsInFeedInbox(emergency, identity)).IsTrue();
    await Assert.That(state.Stats.EmergencyForced).IsGreaterThan(0);

    // Cannot drop Emergency
    state = FeedEngine.Unsubscribe(state, identity, MeshFeedId.Emergency);
    await Assert.That(FeedEngine.EffectiveFeedIds(state, identity).Contains(MeshFeedId.Emergency.Value))
      .IsTrue();

    state = FeedEngine.Pull(state, identity);

    await Assert.That(state.IsInFeedInbox(general, identity)).IsTrue();
    await Assert.That(state.IsInFeedInbox(prices, identity)).IsFalse();
    await Assert.That(state.IsInFeedInbox(whales, identity)).IsFalse();

    state = FeedEngine.Subscribe(state, identity, MeshFeedId.NewsSpaceWhales);
    state = FeedEngine.Pull(state, identity);
    await Assert.That(state.IsInFeedInbox(whales, identity)).IsTrue();
    await Assert.That(state.IsInFeedInbox(prices, identity)).IsFalse();
  }

  [Test]
  public async Task Household_And_Thing_Mailboxes_Receive_Emergency()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 8);
    var hh = MeshIdentityIds.Household("cohort-sol");
    var thing = MeshIdentityIds.Thing("facility:kiosk");
    state = MailboxEngine.Register(state, hh, MeshTestGraph.Sol, MeshIdentityKind.Household);
    state = MailboxEngine.Register(state, thing, MeshTestGraph.Sol, MeshIdentityKind.Thing);

    var emergency = PacketId.From(Guid.Parse("11111111-1111-4111-8111-11111111110b"));
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToFeed(MeshFeedId.Emergency), priority: 10, id: emergency);

    await Assert.That(state.IsInFeedInbox(emergency, hh)).IsTrue();
    await Assert.That(state.IsInFeedInbox(emergency, thing)).IsTrue();
    await Assert.That(state.Mailboxes[hh.Value].Kind).IsEqualTo(MeshIdentityKind.Household);
    await Assert.That(state.Mailboxes[thing.Value].Kind).IsEqualTo(MeshIdentityKind.Thing);
  }

  [Test]
  public async Task Bandwidth_Feed_Defers_Lower_Priority()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 1);
    var hi = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111106"));
    var lo = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111107"));

    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToFeed(MeshFeedId.NewsGeneral), priority: 10, id: hi);
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToPlace(MeshTestGraph.Wolf), priority: 1, id: lo);

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

  [Test]
  public async Task Moving_Mailbox_Gets_CatchUp_Push()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var identity = MeshIdentityIds.Ship("tramp");
    state = MailboxEngine.Register(state, identity, MeshTestGraph.Other, MeshIdentityKind.Ship);
    var id = PacketId.From(Guid.Parse("11111111-1111-4111-8111-111111111108"));
    (state, _) = PublishEngine.PublishPulse(
      state, MeshTestGraph.Sol, MeshAddress.ToIdentity(identity), id: id);

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Sol)).IsTrue();
    await Assert.That(state.IsInMailbox(id, identity)).IsFalse();

    state = MailboxEngine.Move(state, identity, MeshTestGraph.Sol);
    await Assert.That(state.IsInMailbox(id, identity)).IsTrue();
  }
}
