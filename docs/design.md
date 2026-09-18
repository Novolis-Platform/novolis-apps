# Design — app platform

## Source of truth

[`build/apps.json`](../build/apps.json) owns app identity, per-app solutions, Local vs Ship, projects, Windows/Android metadata, data roots, and release flags.

```text
apps.json → per-app .slnx + Novolis.Apps.slnx (aggregate)
         → changed-app PR/merge matrices
         → selected-app/channel release matrix
         → Publish-NovolisApp.ps1 / Publish-NovolisAndroidApk
```

Tooling: `tools/AppsManifest` (`validate`, `generate-solutions`, `ci-matrix`, `release-matrix`, `list`).

## Boundaries

| Boundary | Rule |
|----------|------|
| Repository | One product repo (`novolis-apps`) |
| Solution | One `.slnx` per app (tests included); aggregate is convenience only |
| Platform | Reusable channels (`windows-inno`, `android-apk`) selected per app |
| Product | Permissions, storage, credentials, and privacy inventory per app |

Android and MAUI hosts are excluded from the Linux aggregate and from default Linux CI legs. Merglyph never runs a Windows MAUI workload in PR/release.

## Stack isolation

MAUI apps (`Novolis.Maui.*` packages) and Avalonia apps coexist in the same repo via path-filtered / changed-app matrices. They do not share solution graphs that force both workloads onto every runner.
