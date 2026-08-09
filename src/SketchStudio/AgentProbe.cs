using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Agent.Protocol.Dto;

namespace SketchStudio;

/// <summary>
/// Headless client that drives a live Sketch Studio Avalonia host over LocalIpc
/// (pipe <see cref="App.AgentEndpoint"/>). Launch the GUI with NOVOLIS_AVALONIA_AGENT=1 first.
/// </summary>
internal static class AgentProbe
{
    static readonly string[] RequiredIds =
    [
        "sketch.viewport",
        "sketch.status",
        "sketch.tool.pen",
        "sketch.tool.fill",
        "sketch.tool.select",
        "sketch.layers",
        "sketch.file.new",
        "sketch.export.savePng",
    ];

    public static async Task<int> RunAsync()
    {
        Console.WriteLine($"Connecting to {App.AgentEndpoint}…");
        Environment.SetEnvironmentVariable(UiTransportEndpoints.EndpointEnvVar, App.AgentEndpoint);

        await using var client = new UiAgentClient();
        Exception? last = null;
        for (var attempt = 1; attempt <= 40; attempt++)
        {
            try
            {
                await client.ConnectDefaultAsync().ConfigureAwait(false);
                last = null;
                break;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        if (!client.IsConnected)
        {
            Console.Error.WriteLine($"Failed to connect after retries: {last?.Message}");
            Console.Error.WriteLine("Start Sketch Studio with $env:NOVOLIS_AVALONIA_AGENT='1' first.");
            return 1;
        }

        var hello = await client.HelloAsync().ConfigureAwait(false);
        Console.WriteLine($"hello ok={hello.Success} title={hello.AppTitle} pid={hello.ProcessId}");
        if (!hello.Success || hello.AppTitle is null
            || !hello.AppTitle.Contains("Sketch Studio", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Unexpected hello (expected Sketch Studio title).");
            return 2;
        }

        var tree = await client.TreeAsync().ConfigureAwait(false);
        if (!tree.Success)
        {
            Console.Error.WriteLine($"tree failed: {tree.Error}");
            return 3;
        }

        var ids = tree.Nodes
            .Select(n => n.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        Console.WriteLine($"tree nodes={tree.Nodes.Length} tagged={ids.Count}");

        var missing = RequiredIds.Where(id => !ids.Contains(id)).ToArray();
        if (missing.Length > 0)
        {
            Console.Error.WriteLine("Missing agent ids: " + string.Join(", ", missing));
            return 4;
        }

        foreach (var id in RequiredIds)
            Console.WriteLine($"  found {id}");

        async Task<bool> Click(string id)
        {
            var r = await client.ClickAsync(id).ConfigureAwait(false);
            Console.WriteLine($"click {id} ok={r.Success} err={r.Error}");
            return r.Success;
        }

        if (!await Click("sketch.tool.fill").ConfigureAwait(false))
            return 5;
        await Task.Delay(150).ConfigureAwait(false);

        if (!await Click("sketch.tool.select").ConfigureAwait(false))
            return 5;
        await Task.Delay(150).ConfigureAwait(false);

        if (!await Click("sketch.tool.pen").ConfigureAwait(false))
            return 5;
        await Task.Delay(150).ConfigureAwait(false);

        var status = await client.GetAsync(["sketch.status"]).ConfigureAwait(false);
        var statusText = status.Controls?.FirstOrDefault()?.Text ?? "";
        Console.WriteLine($"status text={statusText}");

        var shot = await client.ScreenshotAsync(maxWidth: 1200).ConfigureAwait(false);
        if (!shot.Success || shot.Png is not { Length: > 0 })
        {
            Console.Error.WriteLine($"screenshot failed: {shot.Error}");
            return 6;
        }

        var dir = Path.Combine(Path.GetTempPath(), "novolis-avalonia-agent");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "sketch-studio-agent-probe.png");
        await File.WriteAllBytesAsync(path, shot.Png).ConfigureAwait(false);
        Console.WriteLine($"SCREENSHOT {path} ({shot.Width}x{shot.Height})");
        Console.WriteLine("AGENT_PROBE_OK");
        return 0;
    }
}
