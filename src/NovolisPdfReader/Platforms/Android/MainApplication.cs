using Android.App;
using Android.Runtime;

namespace NovolisPdfReader;

/// <summary>Android application entry point.</summary>
[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
