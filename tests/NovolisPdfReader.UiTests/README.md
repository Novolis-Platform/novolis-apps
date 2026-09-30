# Novolis PDF Reader UI tests

Appium smoke against the real MAUI host using **Novolis.Testing.Appium**. Complements the in-process `Novolis.Maui.Agent` pipe (`MauiAgentDump` / `MauiAgentMcp`).

The project compiles in CI. Tests skip unless Appium is listening and a built host is available.

## Prerequisites

Appium must already be running (`http://127.0.0.1:4723/` or `APPIUM_HOST`). This suite does not start Appium or install drivers.

```powershell
dotnet test d:\novolis\novolis-apps\tests\NovolisPdfReader.UiTests\NovolisPdfReader.UiTests.csproj -p:NovolisUseProjectReferences=true
```

## Windows

Build the unpackaged exe, then either rely on the default artifacts path or set `NOVOLIS_PDFREADER_UI_APP`:

```powershell
dotnet build d:\novolis\novolis-apps\src\NovolisPdfReader\NovolisPdfReader.csproj -p:NovolisUseProjectReferences=true -p:NovolisMauiTargetFrameworks=net10.0-windows10.0.19041.0
$env:NOVOLIS_PDFREADER_UI_APP = "d:\novolis\novolis-apps\artifacts\bin\NovolisPdfReader\debug_net10.0-windows10.0.19041.0_win-x64\NovolisPdfReader.exe"
```

The Windows test also handshakes `Novolis.Maui.Agent` on pipe `novolis-maui-agent`.

## Android

```powershell
$env:NOVOLIS_PDFREADER_UI_PLATFORM = "android"
$env:NOVOLIS_PDFREADER_UI_APP = "d:\path\to\NovolisPdfReader.apk"
```
