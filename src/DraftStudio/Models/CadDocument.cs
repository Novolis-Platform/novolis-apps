using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DraftStudio.Models;

/// <summary>In-memory / on-disk <c>novolis.cad</c> document (.cadjson).</summary>
internal sealed class CadDocument
{
    public string Format { get; set; } = "novolis.cad";

    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    public CadGenerator Generator { get; set; } = new();

    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }

    public float UnitScaleMeters { get; set; } = 1f;

    public string LinearUnit { get; set; } = "meter";

    public string AngleUnit { get; set; } = "radian";

    public CadCoordinateSystem CoordinateSystem { get; set; } = new();

    public List<CadLayer> Layers { get; set; } = [];

    public List<CadLinetype> Linetypes { get; set; } = [new() { Name = "Continuous" }];

    public List<CadEntity> Entities { get; set; } = [];

    public CadCamera Camera { get; set; } = new();

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

internal sealed class CadGenerator
{
    public string Name { get; set; } = "DraftStudio";

    public string Version { get; set; } = "2026.1.0";
}

internal sealed class CadCoordinateSystem
{
    public string Handedness { get; set; } = "right";

    public string UpAxis { get; set; } = "y";

    public string ForwardAxis { get; set; } = "z";
}

internal sealed class CadLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "0";

    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }

    public float[]? Color { get; set; }
}

internal sealed class CadLinetype
{
    public string Name { get; set; } = "Continuous";

    public float[]? Pattern { get; set; }
}

internal sealed class CadStyle
{
    public string Linetype { get; set; } = "Continuous";

    public float LineWeightMm { get; set; }

    public float[]? Color { get; set; }

    public int? ColorIndex { get; set; }
}

internal sealed class CadCamera
{
    public float Yaw { get; set; } = 0.9f;

    public float Pitch { get; set; } = 0.45f;

    public float Distance { get; set; } = 24f;

    public float[] Target { get; set; } = [0f, 0.5f, 0f];
}

internal enum DraftViewMode
{
    Draft,
    Model,
}

/// <summary>Single entity; unused fields stay null/default for the active <see cref="Kind"/>.</summary>
internal sealed class CadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public Guid? LayerId { get; set; }

    public Guid? ParentId { get; set; }

    public string Kind { get; set; } = "line";

    public CadStyle? Style { get; set; }

    public string? Material { get; set; }

    public float[]? Color { get; set; }

    public float[]? A { get; set; }

    public float[]? B { get; set; }

    public float[]? Center { get; set; }

    public float Radius { get; set; }

    public float Height { get; set; }

    public float[]? HalfExtents { get; set; }

    public float[]? Min { get; set; }

    public float[]? Max { get; set; }

    public float[]? Normal { get; set; }

    public float StartAngle { get; set; }

    public float EndAngle { get; set; }

    public float RotationY { get; set; }

    public List<float[]>? Points { get; set; }

    public bool Closed { get; set; }

    public int Degree { get; set; }

    public List<float[]>? ControlPoints { get; set; }

    public float[]? Knots { get; set; }

    public float[]? Weights { get; set; }

    public bool Periodic { get; set; }

    public List<float[]>? FitPoints { get; set; }

    [JsonIgnore]
    public bool IsSolid => Kind is "box" or "cylinder" or "sphere" or "cone" or "wedge";

    [JsonIgnore]
    public string Summary
    {
        get
        {
            var name = Name ?? Kind;
            return Kind.ToLowerInvariant() switch
            {
                "line" when A is { Length: >= 3 } && B is { Length: >= 3 } =>
                    $"{name} — line ({A[0]:0.##},{A[2]:0.##})→({B[0]:0.##},{B[2]:0.##})",
                "circle" => $"{name} — circle r={Radius:0.##}",
                "rect" => $"{name} — rect",
                "spline" => $"{name} — spline deg={Degree} cps={ControlPoints?.Count ?? 0}",
                "box" when HalfExtents is { Length: >= 3 } =>
                    $"{name} — box {HalfExtents[0] * 2:0.##}×{HalfExtents[1] * 2:0.##}×{HalfExtents[2] * 2:0.##}",
                "cylinder" => $"{name} — cylinder r={Radius:0.##} h={Height:0.##}",
                "sphere" => $"{name} — sphere r={Radius:0.##}",
                _ => $"{name} — {Kind}",
            };
        }
    }
}
