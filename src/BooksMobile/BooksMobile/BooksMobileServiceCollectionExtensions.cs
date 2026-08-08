using BooksMobile.Services;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Manuscript.Export.Audio;

namespace BooksMobile;

/// <summary>Shared DI registration for BooksMobile.</summary>
public static class BooksMobileServiceCollectionExtensions
{
    public static IServiceCollection AddBooksMobileCore(this IServiceCollection services)
    {
        services.AddSingleton<BooksMobileOptions>();
        services.AddSingleton<BooksMobileSession>();
        services.AddSingleton<EdgeTtsSynthesizer>();
        services.AddSingleton<ISynthesizer>(sp => sp.GetRequiredService<EdgeTtsSynthesizer>());
        services.AddSingleton<ChapterSpeechService>();
        services.AddTransient<MainView>();
        return services;
    }
}
