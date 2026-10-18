using Microsoft.Extensions.DependencyInjection;
using Sudoku.Storage;

namespace Sudoku.App;

/// <summary>Registers the shared Sudoku application composition.</summary>
public static class SudokuAppServiceCollectionExtensions
{
    /// <summary>Adds local storage and the current-game coordinator.</summary>
    public static IServiceCollection AddSudokuApp(
        this IServiceCollection services,
        string? storageRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSudokuStorage(storageRootPath);
        services.AddSingleton<SudokuGameService>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
