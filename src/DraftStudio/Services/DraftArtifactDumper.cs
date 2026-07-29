using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DraftStudio.Core;
using Novolis.Avalonia.Raylib;

namespace DraftStudio.Services;

/// <summary>Writes inspectable artifacts (cadjson path, UI/draft PNG, model PNG) for agents and tests.</summary>
internal sealed class DraftArtifactDumper
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public DraftArtifactDumper(DraftSession session, DraftSettingsStore settings)
    {
        Session = session;
        Settings = settings;
    }

    public DraftSession Session { get; }

    public DraftSettingsStore Settings { get; }

    public string DumpsDirectory => Path.Combine(Settings.DataRoot, "dumps");

    public string ManifestPath => Path.Combine(DumpsDirectory, "last-artifact.json");

    public string AllocatePngPath(string kind) =>
        Path.Combine(DumpsDirectory, $"{kind}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");

    public async Task<DraftArtifactResult> DumpAllAsync(
        Window window,
        Control draftViewport,
        RaylibHostControl raylibHost,
        Func<Task> ensureModelViewAsync,
        Func<Task> ensureDraftViewAsync,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DumpsDirectory);
        Session.Save();

        await ensureDraftViewAsync().ConfigureAwait(true);
        var draftPng = AllocatePngPath("draft");
        var draftOk = TryRenderControlPng(draftViewport, draftPng);

        await ensureModelViewAsync().ConfigureAwait(true);
        var modelPng = AllocatePngPath("model");
        var modelOk = await TryCaptureModelPngAsync(raylibHost, modelPng, cancellationToken).ConfigureAwait(true);

        var windowPng = AllocatePngPath("window");
        var windowOk = TryRenderControlPng(window, windowPng);

        var result = new DraftArtifactResult
        {
            DocumentPath = Session.DocumentPath,
            DraftPngPath = draftOk ? draftPng : null,
            ModelPngPath = modelOk ? modelPng : null,
            WindowPngPath = windowOk ? windowPng : null,
            ManifestPath = ManifestPath,
            CapturedAtUtc = DateTime.UtcNow.ToString("O"),
            EntityCount = Session.Document.Entities.Count,
            DrawElevation = Settings.Settings.DrawElevation,
        };

        await File.WriteAllTextAsync(ManifestPath, JsonSerializer.Serialize(result, Json), cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(DumpsDirectory, "last-document.path"),
                Session.DocumentPath + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    public static bool TryRenderControlPng(Control control, string path)
    {
        try
        {
            control.UpdateLayout();
            var w = Math.Max(1, (int)Math.Ceiling(control.Bounds.Width));
            var h = Math.Max(1, (int)Math.Ceiling(control.Bounds.Height));
            if (w < 2 || h < 2)
                return false;

            using var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            bitmap.Render(control);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            using var stream = File.Create(path);
            bitmap.Save(stream);
            return stream.Length > 32;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> TryCaptureModelPngAsync(
        RaylibHostControl host,
        string path,
        CancellationToken cancellationToken = default)
    {
        host.SetHostActive(true);
        host.EnsureHostStarted();

        for (var i = 0; i < 40; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            host.RequestFrame();
            await Task.Delay(40, cancellationToken).ConfigureAwait(true);
            if (host.HasPresentedFrame && host.TrySaveLastPresentedFramePng(path))
                return true;
        }

        return false;
    }
}

internal sealed class DraftArtifactResult
{
    public string DocumentPath { get; init; } = "";

    public string? DraftPngPath { get; init; }

    public string? ModelPngPath { get; init; }

    public string? WindowPngPath { get; init; }

    public string ManifestPath { get; init; } = "";

    public string CapturedAtUtc { get; init; } = "";

    public int EntityCount { get; init; }

    public float DrawElevation { get; init; }
}
