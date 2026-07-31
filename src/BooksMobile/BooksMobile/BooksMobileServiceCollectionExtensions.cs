using BooksMobile.Services;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Audio.Voice.Manuscript;

namespace BooksMobile;

/// <summary>Shared DI registration for BooksMobile.</summary>
public static class BooksMobileServiceCollectionExtensions
{
    public static IServiceCollection AddBooksMobileCore(this IServiceCollection services)
    {
        services.AddSingleton<BooksMobileOptions>();
        services.AddSingleton<BooksMobileSession>();
        services.AddSingleton<EdgeTtsManuscriptSynthesizer>();
        services.AddSingleton<IManuscriptSynthesizer>(sp => sp.GetRequiredService<EdgeTtsManuscriptSynthesizer>());
        services.AddSingleton<ChapterSpeechService>();
        services.AddTransient<MainView>();
        return services;
    }
}
