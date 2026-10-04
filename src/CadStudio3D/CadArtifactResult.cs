namespace CadStudio3D;

internal sealed class CadArtifactResult
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
