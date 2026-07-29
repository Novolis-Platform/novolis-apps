using SinsOfACapitalismTycoon.Universe;
using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Unit;

/// <summary>Gameplay delay: digests, berth vs network board, mailbox sync, escrow push.</summary>
public sealed class MeshGameplayDelayTests
{
  [Test]
  public async Task Spot_Digest_Not_In_Wolf_Inbox_Until_Hops_Complete()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var reader = MeshIdentityIds.Person("ST-7749-63325116");
    state = MailboxEngine.Register(state, reader, MeshTestGraph.Wolf, MeshIdentityKind.Person);
    state = FeedEngine.Subscribe(state, reader, MeshFeedId.CommerceSpot);

    var body =
      "spot:sol:wolf359:Ore:10:5:8|sol|wolf359|Ore|10|5|8|12|StandardCommercial|Sol|Wolf 359";

    var id = PacketId.From(Guid.Parse("33333333-3333-4333-8333-333333333301"));
    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      priority: 2,
      id: id,
      subject: "Spot · Sol",
      body: body,
      topic: MeshTopics.SpotDigest);

    state = FeedEngine.Pull(state, reader);
    await Assert.That(state.IsInFeedInbox(id, reader)).IsFalse();
    await Assert.That(CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359")).IsEmpty();

    for (var i = 0; i < 40; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
      if (state.IsVisibleAt(id, MeshTestGraph.Wolf))
      {
        break;
      }
    }

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsTrue();
    state = FeedEngine.Pull(state, reader);
    await Assert.That(state.IsInFeedInbox(id, reader)).IsTrue();

    var spots = CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359");
    await Assert.That(spots.Count).IsEqualTo(1);
    await Assert.That(spots[0].SkuLabel).IsEqualTo("Ore");
    await Assert.That(spots[0].OriginSystemId).IsEqualTo("sol");
  }

  [Test]
  public async Task Dock_ListSpot_Live_Without_Digest()
  {
    var line = "spot:sol:wolf359:Parts:4:2:3|sol|wolf359|Parts|4|2|3|1|SlowEconomic|Sol|Wolf";
    await Assert.That(SpotDigestCodec.TryParseLine(line, "sol", out var parsed)).IsTrue();
    await Assert.That(parsed.AtOrigin).IsTrue();
    await Assert.That(parsed.SkuLabel).IsEqualTo("Parts");
    await Assert.That(parsed.LogicalKey).IsEqualTo(SpotJobKeys.ForOffer("sol", "wolf359", "Parts", 4m, 2m, 3m));

    // Empty mesh → network board empty (berth board stays live BuildSpot separately).
    var mesh = MeshTestGraph.Triangle();
    await Assert.That(CaptainJobBoard.ListSpotFromMeshDigests(mesh, "sol")).IsEmpty();
  }

  [Test]
  public async Task Mailbox_Move_With_System_Change_CatchUp_Push()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var ship = MeshIdentityIds.Ship("ST Calypso");
    var person = MeshIdentityIds.Person("ST-7749-63325116");
    state = MailboxEngine.Register(state, ship, MeshTestGraph.Sol, MeshIdentityKind.Ship);
    state = MailboxEngine.Register(state, person, MeshTestGraph.Sol, MeshIdentityKind.Person);

    var id = PacketId.From(Guid.Parse("33333333-3333-4333-8333-333333333302"));
    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Wolf,
      MeshAddress.ToIdentity(ship),
      priority: 1,
      id: id,
      subject: "Escrow open",
      body: "open test",
      topic: MeshTopics.Escrow);

    await Assert.That(state.IsInMailbox(id, ship)).IsFalse();

    for (var i = 0; i < 40; i++)
    {
      state = DefaultMeshPipeline.Advance(state);
      if (state.IsVisibleAt(id, MeshTestGraph.Wolf))
      {
        break;
      }
    }

    await Assert.That(state.IsVisibleAt(id, MeshTestGraph.Wolf)).IsTrue();
    await Assert.That(state.IsInMailbox(id, ship)).IsFalse();

    state = MailboxEngine.Move(state, ship, MeshTestGraph.Wolf);
    state = MailboxEngine.Move(state, person, MeshTestGraph.Wolf);
    await Assert.That(state.IsInMailbox(id, ship)).IsTrue();
  }

  [Test]
  public async Task Escrow_Notice_Identity_Eventually_Mailboxed_When_CoLocated()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var ship = MeshIdentityIds.Ship("ST Calypso");
    state = MailboxEngine.Register(state, ship, MeshTestGraph.Sol, MeshIdentityKind.Ship);

    var id = PacketId.From(Guid.Parse("33333333-3333-4333-8333-333333333303"));
    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToIdentity(ship),
      priority: 3,
      id: id,
      subject: "Escrow release · ST Calypso",
      body: "release 100",
      topic: MeshTopics.Escrow);

    await Assert.That(state.IsInMailbox(id, ship)).IsTrue();
    await Assert.That(state.TryGetPacket(id, out var packet)).IsTrue();
    await Assert.That(packet.Topic).IsEqualTo(MeshTopics.Escrow);
    await Assert.That(packet.Subject).Contains("Escrow");
  }
}
