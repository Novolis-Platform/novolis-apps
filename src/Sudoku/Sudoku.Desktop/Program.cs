using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sudoku.App;
using SudokuApplication = Sudoku.App.App;

namespace Sudoku.Desktop;

/// <summary>Desktop process entry point for Novolis Sudoku.</summary>
internal static class Program
{
    /// <summary>Starts the Avalonia desktop lifetime.</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services => services.AddSudokuApp())
            .Build();

        SudokuApplication.Services = host.Services;
        host.Start();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>Builds the platform-detected Avalonia application.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SudokuApplication>()
            .UsePlatformDetect()
            .LogToTrace();
}
