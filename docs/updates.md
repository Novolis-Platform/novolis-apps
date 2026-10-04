# Direct-release updates

Novolis apps can offer updates from the app's GitHub Releases provenance
repository. This is a direct-distribution path for apps that are intentionally
not delivered through Google Play or an app store. It does not replace store
delivery: a store-managed build must leave update decisions to its store.

## Release contract

Each declared direct-release app has an `update` entry in
[`build/apps.json`](../build/apps.json):

```json
"update": {
  "enabled": true,
  "appId": "Novolis.CadStudio3D",
  "repository": "https://github.com/Novolis-Platform/novolis-apps",
  "channel": "stable",
  "distribution": "direct-github"
}
```

The release workflow publishes `Novolis.Update.json` next to the release
assets. The document identifies the app, channel, version, release URL,
platform/runtime target, artifact filename, byte length, and SHA-256 hash.
`SHA256SUMS.txt` also covers the update document. The app selects its own
artifact from the validated manifest; it never guesses from an untrusted
filename.

## Host flow

The neutral `Novolis.Registry.Updates` package owns polling, ETags, retry
backoff, version comparison, snoozing, durable state, verified downloads, and
the platform handoff seam. It does not know about Avalonia, MAUI, installers,
or stores.

Avalonia hosts compose it with `Novolis.Avalonia.Updates`:

```csharp
var updates = new UpdateStatusView
{
    Coordinator = coordinator,
    HostActions = hostActions,
    NotificationMode = UpdateNotificationMode.Toast,
};
```

MAUI hosts use the equivalent `Novolis.Maui.Updates.UpdateStatusView`.
Both controls provide an inline status card, stable automation IDs, release
link, download/progress, snooze, reveal, and host-owned install handoff.
Notifications are deliberately host seams so each product can choose an
inline label, toast, or more prominent popup.

The v1 default is **notify, link, download, and hand off**. It does not
silently replace a running executable or install an APK without platform
confirmation.

## Store and platform rules

- `direct-github` builds may poll the declared GitHub Releases repository.
- `store-managed` builds are rejected by the coordinator before the source is
  contacted.
- Windows direct builds can download an installer or portable archive and let
  the host reveal or hand it off.
- Android direct builds download an APK and hand it to the Android package
  installer. The APK must retain the app's signing identity and increase
  `versionCode`; changing the key cannot update an existing installation.
- Store-targeted Android builds continue through `play-store.yml` and do not
  use this updater path.

## Verification

The normal unit and integration suites exercise manifest validation, GitHub
selection, conditional requests, cancellation, hashes, state promotion,
rollback, concurrency, storage failures, and platform handoff fakes.

Product-level Appium smoke tests are opt-in:

```powershell
$env:NOVOLIS_UPDATE_WINDOWS_SMOKE = "1"
$env:NOVOLIS_UPDATE_WINDOWS_APP = "C:\path\to\app.exe"
dotnet test d:\novolis\novolis-apps\tests\Update.Smoke\Update.Smoke.csproj
```

Use `NOVOLIS_UPDATE_MAUI_WINDOWS_SMOKE` with
`NOVOLIS_UPDATE_MAUI_WINDOWS_APP` for MAUI Windows, or
`NOVOLIS_UPDATE_ANDROID_SMOKE` with
`NOVOLIS_UPDATE_ANDROID_APK`,
`NOVOLIS_UPDATE_ANDROID_PACKAGE`, and an Appium Android device for sideload
coverage. These tests inspect `UpdateStatusView` through its stable
accessibility/automation ID and remain skipped when a device gate is absent.

