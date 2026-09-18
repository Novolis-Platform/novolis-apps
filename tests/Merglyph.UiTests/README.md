# Merglyph UI smoke tests

These tests exercise the built MAUI application through Appium. They are intentionally small and complement the fast `Merglyph.Core.Tests` TUnit suite.

The project is compiled on pull requests. Running it requires a provisioned Appium host and a built application artifact.

## Prerequisites

Install Appium and the driver for the platform you want to test:

```text
npm install -g appium
appium driver install uiautomator2
appium driver install windows
```

Windows UI automation additionally requires WinAppDriver 1.2.1. Android requires a booted emulator or connected device.

Start Appium on its default endpoint (`http://127.0.0.1:4723`) before executing the tests.

## Android

Point `MERGLYPH_UI_APP` at the signed APK and set the platform:

```text
MERGLYPH_UI_PLATFORM=android
MERGLYPH_UI_APP=/path/to/Merglyph-android.apk
dotnet test tests/Merglyph.UiTests/Merglyph.UiTests.csproj -c Release
```

The test launches `dev.novolis.merglyph/dev.novolis.merglyph.MainActivity` through UIAutomator2.

## Windows

Point `MERGLYPH_UI_APP` at the unpackaged user-land executable:

```text
MERGLYPH_UI_PLATFORM=windows
MERGLYPH_UI_APP=C:\path\to\Merglyph.exe
dotnet test tests/Merglyph.UiTests/Merglyph.UiTests.csproj -c Release
```

The Windows release is intentionally unpackaged and runs entirely in user-land. The UI test launches the executable directly through the Appium Windows driver.

## Coverage

The smoke test verifies that the real app launches and exposes the stable accessibility surface used by automation:

- `OpenDocument`
- `DocumentName`
- `DocumentViewer`

Markdown parsing, Mermaid rendering, CSP generation, input size limits, and supported extensions remain covered by the deterministic core TUnit suite.
