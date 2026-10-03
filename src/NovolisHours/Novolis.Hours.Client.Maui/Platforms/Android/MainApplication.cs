using Android.App;
using Android.Runtime;

namespace Novolis.Hours.Client.Maui;

/// <summary>Android application entry point for the Novolis Hours client.</summary>
[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
