using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Avalonia.Android;
using System.Runtime.Versioning;

namespace PresenceLedger.Android;

/// <summary>The main Avalonia activity for the mobile product.</summary>
[Activity(
    Label = "Presence Ledger",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/PresenceLedgerTheme",
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public sealed class MainActivity : AvaloniaMainActivity
{
    const int ForegroundPermissionRequest = 7401;
    const int BackgroundPermissionRequest = 7402;
    bool _backgroundPermissionRequested;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestObservationPermissions();
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        if (HasForegroundLocationPermission())
            StartObservationService();
    }

    /// <inheritdoc />
    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == ForegroundPermissionRequest)
        {
            RequestBackgroundPermissionOrStart();
        }
        else if (requestCode == BackgroundPermissionRequest)
        {
            StartObservationService();
        }
    }

    void RequestObservationPermissions()
    {
        var permissions = new List<string>
        {
            global::Android.Manifest.Permission.AccessCoarseLocation,
            global::Android.Manifest.Permission.AccessFineLocation,
        };

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            permissions.Add(global::Android.Manifest.Permission.NearbyWifiDevices);
            permissions.Add(global::Android.Manifest.Permission.PostNotifications);
        }

        if (permissions.Any(permission => !IsGranted(permission)))
            RequestPermissions(permissions.ToArray(), ForegroundPermissionRequest);
        else
            RequestBackgroundPermissionOrStart();
    }

    void RequestBackgroundPermissionOrStart()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29)
            && !IsBackgroundLocationGranted()
            && !_backgroundPermissionRequested)
        {
            _backgroundPermissionRequested = true;
            RequestPermissions(
                [global::Android.Manifest.Permission.AccessBackgroundLocation],
                BackgroundPermissionRequest);
            return;
        }

        StartObservationService();
    }

    [SupportedOSPlatform("android29.0")]
    bool IsBackgroundLocationGranted() =>
        IsGranted(global::Android.Manifest.Permission.AccessBackgroundLocation);

    bool HasForegroundLocationPermission() =>
        IsGranted(global::Android.Manifest.Permission.AccessFineLocation)
        || IsGranted(global::Android.Manifest.Permission.AccessCoarseLocation);

    bool IsGranted(string permission) =>
        CheckSelfPermission(permission) == Permission.Granted;

    void StartObservationService()
    {
        if (!HasForegroundLocationPermission())
            return;

        var intent = new Intent(this, typeof(PresenceObservationService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            StartForegroundService(intent);
        else
            StartService(intent);
    }
}
