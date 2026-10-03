using Microsoft.Extensions.DependencyInjection;
using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;
using Novolis.Storage.Json;

namespace Novolis.Hours.Storage;

/// <summary>Composes JSON, Azure Table/Azurite, or real in-memory persistence for a complete Hours application.</summary>
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
        services.AddSingleton<IHoursUserStore, RepositoryHoursUserStore>();
        services.AddSingleton<IHoursStorageReadiness, LocalHoursStorageReadiness>();
        return services;
    }

    /// <summary>Adds Azure Table Storage rows for the Hours journal and product profiles.</summary>
    public static IServiceCollection AddAzureTableHoursStorage(
        this IServiceCollection services,
        string connectionString,
        string tablePrefix)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tablePrefix);

        services.AddSingleton<HoursChangeFeed>();
        services.AddStorage(storage => storage.AddAzureTableProvider(options =>
        {
            options.ConnectionString = connectionString;
            options.TablePrefix = tablePrefix;
        }));
        services.AddSingleton<IHoursJournal, AzureHoursJournal>();
        services.AddSingleton<IHoursUserStore, AzureHoursUserStore>();
        services.AddSingleton<IHoursStorageReadiness, AzureHoursStorageReadiness>();
        return services;
    }

    /// <summary>Adds a thread-safe in-memory journal for no-mock whole-application feature tests.</summary>
    public static IServiceCollection AddInMemoryHoursJournal(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<HoursChangeFeed>();
        services.AddSingleton<IHoursJournal, InMemoryHoursJournal>();
        services.AddSingleton<IHoursUserStore, RepositoryHoursUserStore>();
        services.AddSingleton<IHoursStorageReadiness, LocalHoursStorageReadiness>();
        return services;
    }
}
