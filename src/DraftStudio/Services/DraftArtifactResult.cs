using System.Text.Json;
using Avalonia.Controls;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Raylib;

namespace DraftStudio.Services;

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
