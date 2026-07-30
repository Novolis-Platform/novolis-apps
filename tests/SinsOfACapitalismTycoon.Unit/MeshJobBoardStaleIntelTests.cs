using SinsOfACapitalismTycoon.Universe;
using Novolis.Simulation.Mesh;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Unit;

/// <summary>
/// FTL mesh ahead of tramp travel; job digests retract when taken or repriced;
/// arrival at Wolf can discover the offer is gone / changed.
/// </summary>
public sealed class MeshJobBoardStaleIntelTests
{
  static readonly PacketId DigestId = PacketId.From(Guid.Parse("55555555-5555-4555-8555-555555555501"));
  static readonly PacketId RetractId = PacketId.From(Guid.Parse("55555555-5555-4555-8555-555555555502"));
  static readonly PacketId DigestV2Id = PacketId.From(Guid.Parse("55555555-5555-4555-8555-555555555503"));

  static string JobKeyV1 => SpotJobKeys.ForOffer("sol", "wolf359", "Ore", 10m, 5m, 8m);
  static string JobKeyV2 => SpotJobKeys.ForOffer("sol", "wolf359", "Ore", 10m, 5m, 9m); // destBid changed

  static string DigestBodyV1() =>
    $"{JobKeyV1}|sol|wolf359|Ore|10|5|8|12|StandardCommercial|Sol|Wolf 359";

  static string DigestBodyV2() =>
    $"{JobKeyV2}|sol|wolf359|Ore|10|5|9|20|StandardCommercial|Sol|Wolf 359";

  static MeshState SeedCaptainAt(MeshState state, MeshNodeId node)
  {
    var person = MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId);
    var ship = MeshIdentityIds.Ship(CampaignWorld.PlayerHullName);
    state = MailboxEngine.Register(state, person, node, MeshIdentityKind.Person);
    state = MailboxEngine.Register(state, ship, node, MeshIdentityKind.Ship);
    state = FeedEngine.Subscribe(state, person, MeshFeedId.CommerceSpot);
    state = FeedEngine.Subscribe(state, ship, MeshFeedId.CommerceSpot);
    return state;
  }

  static MeshState AdvanceUntil(MeshState state, Func<MeshState, bool> done, int maxHours = 80)
  {
    for (var i = 0; i < maxHours && !done(state); i++)
    {
      state = DefaultMeshPipeline.Advance(state);
    }

    return state;
  }

  [Test]
  public async Task Ftl_Pulse_Is_Much_Faster_Than_Tramp_Sol_To_Wolf()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var pulse = MeshTravelCompare.PathPulseHours(state, MeshTestGraph.Sol, MeshTestGraph.Wolf);
    var tramp = MeshTravelCompare.PathTrampHours(state, MeshTestGraph.Sol, MeshTestGraph.Wolf);

    await Assert.That(pulse).IsLessThan(tramp / 10);
    await Assert.That(tramp).IsGreaterThanOrEqualTo(pulse * 15);
    await Assert.That(pulse).IsEqualTo(2);
  }

  [Test]
  public async Task Spot_Digest_Reaches_Wolf_Long_Before_Tramp_Could()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    state = SeedCaptainAt(state, MeshTestGraph.Wolf);

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      priority: 2,
      id: DigestId,
      subject: "Spot · Sol",
      body: DigestBodyV1(),
      topic: MeshTopics.SpotDigest,
      logicalKey: JobKeyV1);

    var pulseEta = MeshTravelCompare.PathPulseHours(state, MeshTestGraph.Sol, MeshTestGraph.Wolf);
    var trampEta = MeshTravelCompare.PathTrampHours(state, MeshTestGraph.Sol, MeshTestGraph.Wolf);

    state = AdvanceUntil(state, s => s.IsVisibleAt(DigestId, MeshTestGraph.Wolf), maxHours: pulseEta + 5);
    await Assert.That(state.IsVisibleAt(DigestId, MeshTestGraph.Wolf)).IsTrue();
    await Assert.That(state.HourIndex).IsLessThan(trampEta / 2);

    state = FeedEngine.Pull(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId));
    var board = CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359", evaluationNode: MeshTestGraph.Wolf);
    await Assert.That(board.Count).IsEqualTo(1);
    await Assert.That(board[0].LogicalKey).IsEqualTo(JobKeyV1);
  }

  [Test]
  public async Task Job_Taken_Retraction_Beats_Tramp_Network_Board_Empty_On_Arrival()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    // Captain starts at Sol, reads offer, then steams — mailbox moves with hull.
    state = SeedCaptainAt(state, MeshTestGraph.Sol);

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      priority: 2,
      id: DigestId,
      subject: "Spot · Sol",
      body: DigestBodyV1(),
      topic: MeshTopics.SpotDigest,
      logicalKey: JobKeyV1);
    state = FeedEngine.Pull(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId));
    await Assert.That(CaptainJobBoard.ListSpotFromMeshDigests(state, "sol").Count).IsEqualTo(1);

    // Job taken at Sol — retract floods while captain is still en route.
    (state, _) = PublishEngine.PublishRetraction(
      state,
      MeshTestGraph.Sol,
      JobKeyV1,
      id: RetractId,
      subject: "Job taken");

    // Simulate travel: move mailbox to Wolf only after pulse+retract have had time to flood.
    var pulseEta = MeshTravelCompare.PathPulseHours(state, MeshTestGraph.Sol, MeshTestGraph.Wolf);
    state = AdvanceUntil(
      state,
      s => s.IsVisibleAt(DigestId, MeshTestGraph.Wolf) && s.IsRetractedAt(JobKeyV1, MeshTestGraph.Wolf),
      maxHours: pulseEta + 8);

    await Assert.That(state.IsRetractedAt(JobKeyV1, MeshTestGraph.Wolf)).IsTrue();
    await Assert.That(state.HourIndex)
      .IsLessThan(MeshTravelCompare.PathTrampHours(state, MeshTestGraph.Sol, MeshTestGraph.Wolf));

    // Arrival: hull co-locates; stale digest may still be in inbox from Sol pull,
    // but Wolf retraction filters the network board.
    state = MailboxEngine.Move(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId), MeshTestGraph.Wolf);
    state = MailboxEngine.Move(state, MeshIdentityIds.Ship(CampaignWorld.PlayerHullName), MeshTestGraph.Wolf);
    state = FeedEngine.Pull(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId));

    var network = CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359", evaluationNode: MeshTestGraph.Wolf);
    await Assert.That(network).IsEmpty();
    await Assert.That(state.Stats.RetractionsApplied).IsGreaterThan(0);
  }

  [Test]
  public async Task Price_Change_Retracts_Old_Key_And_Publishes_New_Offer()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    state = SeedCaptainAt(state, MeshTestGraph.Wolf);

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      priority: 2,
      id: DigestId,
      body: DigestBodyV1(),
      topic: MeshTopics.SpotDigest,
      logicalKey: JobKeyV1);

    (state, _) = PublishEngine.PublishRetraction(
      state, MeshTestGraph.Sol, JobKeyV1, id: RetractId);

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      priority: 2,
      id: DigestV2Id,
      body: DigestBodyV2(),
      topic: MeshTopics.SpotDigest,
      logicalKey: JobKeyV2);

    state = AdvanceUntil(
      state,
      s => s.IsRetractedAt(JobKeyV1, MeshTestGraph.Wolf)
           && s.IsVisibleAt(DigestV2Id, MeshTestGraph.Wolf),
      maxHours: 40);

    state = FeedEngine.Pull(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId));
    var board = CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359", evaluationNode: MeshTestGraph.Wolf);

    await Assert.That(board.Count).IsEqualTo(1);
    await Assert.That(board[0].LogicalKey).IsEqualTo(JobKeyV2);
    await Assert.That(board[0].DestBid).IsEqualTo(9m);
    await Assert.That(board[0].Margin).IsEqualTo(20m);
    await Assert.That(board.Any(b => b.LogicalKey == JobKeyV1)).IsFalse();
  }

  [Test]
  public async Task Stale_Inbox_Digest_Survives_But_Retracted_Lines_Are_Filtered()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    state = SeedCaptainAt(state, MeshTestGraph.Wolf);

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      id: DigestId,
      body: DigestBodyV1(),
      topic: MeshTopics.SpotDigest,
      logicalKey: JobKeyV1);

    state = AdvanceUntil(state, s => s.IsVisibleAt(DigestId, MeshTestGraph.Wolf));
    state = FeedEngine.Pull(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId));
    await Assert.That(state.IsInFeedInbox(DigestId, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId))).IsTrue();
    await Assert.That(CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359").Count).IsEqualTo(1);

    (state, _) = PublishEngine.PublishRetraction(state, MeshTestGraph.Sol, JobKeyV1, id: RetractId);
    state = AdvanceUntil(state, s => s.IsRetractedAt(JobKeyV1, MeshTestGraph.Wolf));

    // Digest packet remains in inbox (no postal delete) — board still filters.
    await Assert.That(state.IsInFeedInbox(DigestId, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId))).IsTrue();
    await Assert.That(CaptainJobBoard.ListSpotFromMeshDigests(state, "wolf359", evaluationNode: MeshTestGraph.Wolf))
      .IsEmpty();
  }

  [Test]
  public async Task SyncSpotBoard_Retracts_Keys_Missing_From_Live_Set()
  {
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    var ledger = new SpotMeshLedger();
    ledger.Remember(JobKeyV1, "sol");

    // Live set empty → SyncSpotBoard path via GoneRelativeTo
    var gone = ledger.GoneRelativeTo(new HashSet<string>(StringComparer.Ordinal));
    await Assert.That(gone.Count).IsEqualTo(1);
    await Assert.That(gone[0].LogicalKey).IsEqualTo(JobKeyV1);

    state = MeshGameplayPulse.RetractOffer(state, "sol", JobKeyV1, ledger);
    await Assert.That(state.IsRetractedAt(JobKeyV1, MeshTestGraph.Sol)).IsTrue();
    await Assert.That(ledger.AdvertisedKeys.Contains(JobKeyV1)).IsFalse();
  }

  [Test]
  public async Task Dock_Live_Offer_Distinct_From_Retracted_Mesh_View()
  {
    // Mesh filtered empty; a live candidate with same route still has a LogicalKey
    // (berth board uses BuildSpot — here we assert the dual view contract on keys).
    var state = MeshTestGraph.Triangle(bandwidth: 32);
    state = SeedCaptainAt(state, MeshTestGraph.Sol);

    (state, _) = PublishEngine.PublishPulse(
      state,
      MeshTestGraph.Sol,
      MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
      id: DigestId,
      body: DigestBodyV1(),
      topic: MeshTopics.SpotDigest,
      logicalKey: JobKeyV1);
    state = FeedEngine.Pull(state, MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId));
    (state, _) = PublishEngine.PublishRetraction(state, MeshTestGraph.Sol, JobKeyV1, id: RetractId);

    var network = CaptainJobBoard.ListSpotFromMeshDigests(state, "sol", evaluationNode: MeshTestGraph.Sol);
    await Assert.That(network).IsEmpty();

    // Live berth "truth": same route still constructible as a candidate key that is NOT retracted
    // when prices differ — models price change at dock.
    var liveKey = SpotJobKeys.ForOffer("sol", "wolf359", "Ore", 10m, 5m, 7m);
    await Assert.That(liveKey).IsNotEqualTo(JobKeyV1);
    await Assert.That(state.IsRetractedAt(liveKey, MeshTestGraph.Sol)).IsFalse();
    await Assert.That(state.IsRetractedAt(JobKeyV1, MeshTestGraph.Sol)).IsTrue();
  }
}
