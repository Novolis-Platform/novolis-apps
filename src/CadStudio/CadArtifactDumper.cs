using System.Text.Json;
using Avalonia.Controls;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Raylib;

namespace CadStudio;

/// <summary>Writes inspectable CAD artifacts for agents and tests.</summary>
internal sealed class CadArtifactDumper
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public CadArtifactDumper(CadDocumentSession session, CadEditorSettings settings)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public CadDocumentSession Session { get; }

    public CadEditorSettings Settings { get; }

    public string DumpsDirectory => Path.Combine(Settings.DataRoot, "dumps");

    public string ManifestPath => Path.Combine(DumpsDirectory, "last-artifact.json");

    public string AllocatePngPath(string kind) =>
        Path.Combine(DumpsDirectory, $"{kind}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");

    public async Task<CadArtifactResult> DumpAllAsync(
        Window window,
        Control draftViewport,
        RaylibHostControl raylibHost,
        Func<Task> ensureModelViewAsync,
        Func<Task> ensureDraftViewAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(draftViewport);
        ArgumentNullException.ThrowIfNull(raylibHost);
        ArgumentNullException.ThrowIfNull(ensureModelViewAsync);
        ArgumentNullException.ThrowIfNull(ensureDraftViewAsync);

        Directory.CreateDirectory(DumpsDirectory);
        Session.Save();

        await ensureDraftViewAsync().ConfigureAwait(true);
        var draftPng = AllocatePngPath("draft");
        var draftOk = CadViewportExporter.TryExportPlanPng(draftViewport, draftPng);

        await ensureModelViewAsync().ConfigureAwait(true);
        var modelPng = AllocatePngPath("model");
        var modelOk = await CadViewportExporter.ExportModelPngAsync(
                raylibHost,
                modelPng,
                cancellationToken)
            .ConfigureAwait(true) is not null;

        var windowPng = AllocatePngPath("window");
        var windowOk = CadViewportExporter.TryExportPlanPng(window, windowPng);

        var result = new CadArtifactResult
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

        await File.WriteAllTextAsync(
                ManifestPath,
                JsonSerializer.Serialize(result, Json),
                cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(DumpsDirectory, "last-document.path"),
                Session.DocumentPath + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}
