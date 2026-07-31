using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class CaptainJobBoardRaceTests
{
  [Test]
  public async Task SnapshotHubOrders_skips_null_slots_from_concurrent_list_corruption()
  {
    var world = new EconomyWorld();
    // Simulate a raced ToArray that can include nulls after Clear/RemoveAll.
    world.HubOrders.Add(null!);
    var snap = CaptainJobBoard.SnapshotHubOrders(world);
    await Assert.That(snap.Length).IsEqualTo(0);
  }

  [Test]
  public async Task CaptureDesk_survives_null_hub_order_slots()
  {
    var session = new CampaignRunner.LiveSession(
      seed: 1001,
      hours: 48,
      drama: false,
      playerControl: true,
      autopilot: false,
      localBoard: true);

    session.Sim.State.World.HubOrders.Insert(0, null!);
    var desk = session.CaptureDesk();
    await Assert.That(desk.BerthOffers.Count).IsGreaterThanOrEqualTo(1);
    await Assert.That(desk.Charters).IsNotNull();
  }
}
