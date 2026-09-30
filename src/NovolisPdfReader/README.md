# Novolis PDF Reader

Thin MAUI host for local PDF reading via `Novolis.Maui.PdfViewer`. Product home
is **novolis-apps** (`src/NovolisPdfReader`).

- **Ship:** Windows Inno (`windows-inno`) and Android APK (`android-apk`) via the **Release** workflow (`NovolisPdfReader`)
- **Local:** Windows MAUI + Android
- **Branding:** Novolis mark (`MauiIcon` / splash PNG rasters)
- **Privacy:** [PRIVACY.md](PRIVACY.md)

```powershell
dotnet run --project d:\novolis\novolis-apps\src\NovolisPdfReader\NovolisPdfReader.csproj -f net10.0-windows10.0.19041.0 -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
```

```powershell
pwsh -File d:\novolis\novolis-apps\scripts\deploy-novolispdfreader-android.ps1
```

See [docs/getting-started.md](../../docs/getting-started.md) and [docs/release.md](../../docs/release.md).
