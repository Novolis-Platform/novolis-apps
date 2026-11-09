using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.Storage.Tests;

public sealed class ObservationSettingsStoreTests
{
    [Test]
    public async Task Save_round_trips_settings_and_raises_changed()
    {
        var root = Path.Combine(Path.GetTempPath(), "presence-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ObservationSettingsStore(root);
            var changed = 0;
            store.Changed += (_, _) => changed++;

            var settings = new ObservationSettings(
                pollIntervalSeconds: 45,
                freshFixAgeMinutes: 5,
                freshNetworkAgeMinutes: 4,
                autoPublishDownloads: false,
                autoPublishMinimumIntervalMinutes: 30,
                clockifyProjectName: " Client work ",
                clockifyBillable: true);
            await store.SaveAsync(settings);

            await Assert.That(changed).IsEqualTo(1);
            await Assert.That(store.Current.PollIntervalSeconds).IsEqualTo(45);
            await Assert.That(store.Current.AutoPublishDownloads).IsFalse();
            await Assert.That(store.Current.ClockifyProjectName).IsEqualTo("Client work");

            var reloaded = new ObservationSettingsStore(root);
            await Assert.That(reloaded.Current.PollIntervalSeconds).IsEqualTo(45);
            await Assert.That(reloaded.Current.FreshFixAgeMinutes).IsEqualTo(5);
            await Assert.That(reloaded.Current.ClockifyBillable).IsTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
