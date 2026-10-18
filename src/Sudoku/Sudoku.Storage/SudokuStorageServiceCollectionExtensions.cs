using Microsoft.Extensions.DependencyInjection;

namespace Sudoku.Storage;

/// <summary>Registers the local Sudoku save and recovery services.</summary>
public static class SudokuStorageServiceCollectionExtensions
{
    /// <summary>Adds one process-wide local Sudoku store.</summary>
    public static IServiceCollection AddSudokuStorage(
        this IServiceCollection services,
        string? rootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(_ => new SudokuSaveStore(rootPath));
        return services;
    }
}
