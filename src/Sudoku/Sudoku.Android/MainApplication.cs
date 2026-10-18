using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.IO.Platform.Android;
using Sudoku.App;
using SudokuApplication = Sudoku.App.App;

namespace Sudoku.Android;

/// <summary>Android application host that initializes Sudoku services before Avalonia.</summary>
[Application]
public class MainApplication : AvaloniaAndroidApplication<SudokuApplication>
{
    private IHost? _host;

    /// <summary>JNI constructor.</summary>
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc />
    public override void OnCreate()
    {
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        var locations = AndroidAppStorage.Open(
            context,
            "Sudoku",
            AndroidStoragePreference.AppPrivate);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
                services.AddSudokuApp(locations.RootDirectory))
            .Build();
        SudokuApplication.Services = _host.Services;
        _host.Start();
        base.OnCreate();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
