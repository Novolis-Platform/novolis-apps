# Design — app platform

## Source of truth

[`build/apps.json`](../build/apps.json) owns app identity, per-app solutions, Local vs Ship, projects, Windows/Android metadata, data roots, and release flags.

```text
apps.json → per-app .slnx + per-app Linux CI .slnx + Novolis.Apps.slnx
         → coverage-aware Linux / Android / Windows validation rows
         → selected-app/channel release matrix
         → Publish-NovolisApp.ps1 / Publish-NovolisAndroidApk
```

Tooling: `tools/AppsManifest` (`validate`, `generate-solutions`, `ci-matrix`, `release-matrix`, `list`).
`ci-matrix` emits separate platform arrays. Fast coverage selects affected apps
or one representative per stack for root-policy changes; full coverage is
explicit (`--all` or `--coverage full`).

## Boundaries

| Boundary | Rule |
|----------|------|
| Repository | One product repo (`novolis-apps`) |
| Solution | One `.slnx` per app (tests included); aggregate is convenience only |
| Platform | Reusable channels (`windows-inno`, `android-apk`) selected per app |
| Product | Permissions, storage, credentials, and privacy inventory per app |

Android and MAUI hosts are excluded from per-app Linux CI solutions. Android
rows build only declared Android heads, and Windows rows exist only when an app
explicitly opts into Windows validation. Release/signing remains separate from
merge validation.

## Stack isolation

MAUI apps (`Novolis.Maui.*` packages) and Avalonia apps coexist in the same
repo via coverage-aware, platform-filtered matrices. They do not share Linux
solution graphs that force Android or MAUI workloads onto every runner.
