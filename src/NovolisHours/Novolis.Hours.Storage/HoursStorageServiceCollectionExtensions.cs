using Microsoft.Extensions.DependencyInjection;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;

namespace Novolis.Hours.Storage;

/// <summary>Composes production JSON persistence or real in-memory persistence for a complete Hours application.</summary>
public static class HoursStorageServiceCollectionExtensions
{
    /// <summary>Adds a JSON-file journal rooted at the supplied data directory.</summary>
    public static IServiceCollection AddJsonHoursJournal(this IServiceCollection services, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        services.AddSingleton<HoursChangeFeed>();
        services.AddStorage(builder => builder.AddJsonFilesProvider(options =>
        {
            options.RootPath = rootPath;
            options.UseProcessLock = true;
        }));
        services.AddSingleton<IHoursJournal, JsonHoursJournal>();
        return services;
    }

    /// <summary>Adds a thread-safe in-memory journal for no-mock whole-application feature tests.</summary>
    public static IServiceCollection AddInMemoryHoursJournal(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<HoursChangeFeed>();
        services.AddSingleton<IHoursJournal, InMemoryHoursJournal>();
        return services;
    }
}
