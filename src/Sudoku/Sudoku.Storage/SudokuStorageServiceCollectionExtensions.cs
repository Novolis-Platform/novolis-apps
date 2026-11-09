using Microsoft.Extensions.DependencyInjection;

namespace Sudoku.Storage;

/// <summary>Registers the local Sudoku save, recovery, and progress services.</summary>
public static class SudokuStorageServiceCollectionExtensions
{
    /// <summary>Adds one process-wide local Sudoku store and campaign progress.</summary>
    public static IServiceCollection AddSudokuStorage(
        this IServiceCollection services,
        string? rootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(_ => new SudokuSaveStore(rootPath));
        services.AddSingleton(provider =>
        {
            var saves = provider.GetRequiredService<SudokuSaveStore>();
            return new SudokuProgressStore(saves.RootPath);
        });
        return services;
    }
}
