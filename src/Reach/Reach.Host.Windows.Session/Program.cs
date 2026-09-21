using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Windows.Audio;
using Novolis.Windows.Clipboard;
using Novolis.Windows.Display;
using Novolis.Windows.Input;

namespace Novolis.Reach.Host.Windows.Session;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<WindowsInputController>();
        builder.Services.AddSingleton<WindowsClipboardService>();
        builder.Services.AddSingleton<WindowsDisplayTopology>();
        builder.Services.AddSingleton<WindowsLoopbackAudioCapture>();
        builder.Services.AddSingleton<ReachSessionHost>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ReachSessionHost>());
        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
