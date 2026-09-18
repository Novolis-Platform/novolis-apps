using BooksMobile.Services;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Speech;

namespace BooksMobile;

/// <summary>Shared DI registration for BooksMobile.</summary>
public static class BooksMobileServiceCollectionExtensions
{
    public static IServiceCollection AddBooksMobileCore(this IServiceCollection services)
    {
        services.AddSingleton<BooksMobileOptions>();
        services.AddSingleton<BooksMobileSession>();
        services.AddNovolisSpeech();
        services.AddSingleton<ChapterSpeechService>();
        services.AddSingleton<ReviewSelectionImporter>();
        services.AddTransient<MainView>();
        return services;
    }
}
