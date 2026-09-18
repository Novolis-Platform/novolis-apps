# Merglyph

Thin MAUI host for offline Markdown viewing via `Novolis.Maui.Markdown`. Product home is **novolis-apps** (`src/Merglyph`).

- **Ship:** Android APK (`android-apk`)
- **Local:** Windows MAUI debug (not an Inno/Ship channel)
- **Solution:** `src/Merglyph/Merglyph.slnx`
- **Privacy:** [PRIVACY.md](PRIVACY.md)

```powershell
dotnet build d:\novolis\novolis-apps\src\Merglyph\Merglyph\Merglyph.csproj -f net10.0-android -p:NovolisMauiTargetFrameworks=net10.0-android
```

See [docs/getting-started.md](../../docs/getting-started.md) and [docs/release.md](../../docs/release.md).
