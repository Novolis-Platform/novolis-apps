using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Torrent;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseGraphicalProfile();
        builder.Services.AddSingleton(
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45),
            });
        builder.Services.AddSingleton<TorrentTransferService>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
