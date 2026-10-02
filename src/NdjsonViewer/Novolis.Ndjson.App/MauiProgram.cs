using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Novolis.Maui.GraphicalProfile;
using Novolis.IO.Ndjson;

namespace Novolis.Ndjson.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseGraphicalProfile();

        builder.Services
            .AddSingleton<NdjsonFileReader>()
            .AddSingleton<NdjsonDocumentSession>()
            .AddSingleton<NdjsonFilePicker>()
            .AddSingleton<DocumentActivationInbox>()
            .AddSingleton<MainPage>();

        var app = builder.Build();
        ActivationBridge.Initialize(app.Services.GetRequiredService<DocumentActivationInbox>());
        return app;
    }
}
