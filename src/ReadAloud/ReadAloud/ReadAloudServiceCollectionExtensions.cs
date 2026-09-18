using Microsoft.Extensions.DependencyInjection;
using Novolis.Manuscript.Export.Audio;
using ReadAloud.Services;
using ReadAloud.Views;

namespace ReadAloud;

/// <summary>Shared DI registration for Read Aloud.</summary>
public static class ReadAloudServiceCollectionExtensions
{
    public static IServiceCollection AddReadAloudCore(this IServiceCollection services)
    {
        services.AddSingleton<EdgeTtsSynthesizer>();
        services.AddSingleton<ISynthesizer>(sp => sp.GetRequiredService<EdgeTtsSynthesizer>());
        services.AddSingleton<SpeechService>();
        services.AddTransient<MainView>();
        return services;
    }
}
