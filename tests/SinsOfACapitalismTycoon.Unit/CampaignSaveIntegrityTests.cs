using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Unit;

public sealed class CampaignSaveIntegrityTests
{
  [Test]
  public async Task Replay_Checkpoint_Passes_Hash_Day_Cash_Integrity()
  {
    var root = Path.Combine(Path.GetTempPath(), "sins-save-integrity-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      var store = new CampaignSaveStore(root);
      var session = new CampaignRunner.LiveSession(
        seed: 1001,
        hours: 48,
        drama: true,
        playerControl: false,
        autopilot: false);
      await session.AdvanceHoursAsync(48, quiet: true);

      var save = await store.SaveAsync(session, "integrity-smoke");
      await Assert.That(save.SimHash).IsNotEqualTo(0uL);
      await Assert.That(save.HoursDone).IsEqualTo(48);
      await Assert.That(save.HasIntegrity).IsTrue();

      // FromSaveAsync throws on integrity failure — success is the assert.
      var loaded = await CampaignRunner.LiveSession.FromSaveAsync(save);
      await Assert.That(loaded.HoursDone).IsEqualTo(save.HoursDone);
      await Assert.That(loaded.RequestedHours).IsEqualTo(save.HorizonHours);
      await Assert.That(loaded.LastBridge).IsNotNull();
    }
    finally
    {
      try
      {
        Directory.Delete(root, recursive: true);
      }
      catch
      {
        // best-effort cleanup
      }
    }
  }
}
