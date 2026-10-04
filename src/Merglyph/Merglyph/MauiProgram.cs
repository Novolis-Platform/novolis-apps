using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Merglyph.Core;
using Novolis.Maui.Activation;
using Novolis.Maui.GraphicalProfile;

namespace Merglyph;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        PlatformWebViewSecurity.Configure();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseGraphicalProfile();

        builder.Services
            .AddSingleton<MarkdownDocumentReader>()
            .AddSingleton<DocumentSession>()
            .AddSingleton<MarkdownDocumentPicker>()
            .AddSingleton<RecentDocumentStore>()
            .AddSingleton<MauiActivationInbox<DocumentOpenRequest>>()
            .AddSingleton<MainPage>();

        var app = builder.Build();
        MauiActivationBridge<DocumentOpenRequest>.Initialize(
            app.Services.GetRequiredService<MauiActivationInbox<DocumentOpenRequest>>());
        return app;
    }
}
