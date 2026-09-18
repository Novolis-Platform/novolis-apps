using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Merglyph.Core;

namespace Merglyph;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        PlatformWebViewSecurity.Configure();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services
            .AddSingleton<MarkdownDocumentReader>()
            .AddSingleton<DocumentSession>()
            .AddSingleton<MarkdownDocumentPicker>()
            .AddSingleton<DocumentActivationInbox>()
            .AddSingleton<MainPage>();

        var app = builder.Build();
        ActivationBridge.Initialize(app.Services.GetRequiredService<DocumentActivationInbox>());
        return app;
    }
}
