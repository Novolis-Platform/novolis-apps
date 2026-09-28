using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using PresenceLedger.App;
using PresenceLedgerApp = PresenceLedger.App.App;
using System.Runtime.Versioning;

namespace PresenceLedger.Android;

/// <summary>
/// Foreground service that keeps sparse evidence observation alive while the
/// Avalonia activity is not visible.
/// </summary>
[Service(
    Name = "com.novolis.presenceledger.PresenceObservationService",
    Exported = false)]
public sealed class PresenceObservationService : Service
{
    const string ChannelId = "presence-observation";
    const int NotificationId = 20260928;

    PresenceObservationCoordinator? _coordinator;

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            CreateNotificationChannel();
        var notification = BuildNotification();
        StartForeground(NotificationId, notification);
        _coordinator = PresenceLedgerApp.Services.GetService<PresenceObservationCoordinator>();
        if (_coordinator is not null)
            _ = _coordinator.StartAsync();
    }

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId) =>
        StartCommandResult.Sticky;

    /// <inheritdoc />
    public override void OnDestroy()
    {
        try
        {
            _coordinator?.StopAsync().GetAwaiter().GetResult();
        }
        catch (global::System.OperationCanceledException)
        {
            // Service shutdown is already in progress.
        }

        base.OnDestroy();
    }

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    [SupportedOSPlatform("android26.0")]
    void CreateNotificationChannel()
    {
        var manager = GetSystemService(NotificationService) as NotificationManager;
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Presence observation",
            NotificationImportance.Low)
        {
            Description = "Sparse local evidence used to infer configured presence.",
        });
    }

    Notification BuildNotification()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return BuildModernNotification();

        return BuildLegacyNotification();
    }

    [SupportedOSPlatform("android26.0")]
    Notification BuildModernNotification() =>
        new Notification.Builder(this, ChannelId)
            .SetContentTitle("Presence Ledger")
            .SetContentText("Monitoring configured locations locally")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .Build();

    [UnsupportedOSPlatform("android26.0")]
    Notification BuildLegacyNotification() =>
        new Notification.Builder(this)
            .SetContentTitle("Presence Ledger")
            .SetContentText("Monitoring configured locations locally")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .Build();
}
