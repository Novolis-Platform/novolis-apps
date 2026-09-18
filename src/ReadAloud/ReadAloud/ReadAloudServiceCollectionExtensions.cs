using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Speech;
using ReadAloud.Services;
using ReadAloud.Views;

namespace ReadAloud;

/// <summary>Shared DI registration for Read Aloud.</summary>
public static class ReadAloudServiceCollectionExtensions
{
    public static IServiceCollection AddReadAloudCore(this IServiceCollection services)
    {
        services.AddNovolisSpeech();
        services.AddSingleton<SpeechService>();
        services.AddTransient<MainView>();
        return services;
    }
}
