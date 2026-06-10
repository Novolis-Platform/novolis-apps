namespace ConceptStudio.Models;

internal sealed class ConceptDocument
{
    public string Name { get; set; } = "Untitled ship";

    public List<ConceptPartRecord> Parts { get; set; } = [];

    public List<AnnotationRecord> Annotations { get; set; } = [];

    public OrbitCameraState Camera { get; set; } = new();

    public float UnitScaleMeters { get; set; } = 1f;
}

internal sealed class OrbitCameraState
{
    public float Yaw { get; set; } = 0.9f;

    public float Pitch { get; set; } = 0.35f;

    public float Distance { get; set; } = 40f;

    public float[] Target { get; set; } = [0f, 1f, 0f];
}

internal enum ConceptViewMode
{
    Orbit,
    Plan,
    Profile,
    Bow,
}

internal enum ConceptMaterialKind
{
    Hull,
    HullDark,
    Metal,
    Glass,
    EngineGlow,
}

internal sealed class ConceptPartRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ParentId { get; set; }

    public string Name { get; set; } = "Part";

    public string Kind { get; set; } = "box";

    public float[] Center { get; set; } = [0f, 0.5f, 0f];

    public float[] HalfExtents { get; set; } = [0.5f, 0.5f, 0.5f];

    public float Radius { get; set; } = 0.5f;

    public float Height { get; set; } = 1f;

    public float RotationY { get; set; }

    public float[] Scale { get; set; } = [1f, 1f, 1f];

    public string Material { get; set; } = "hull";

    public float[] Color { get; set; } = [0.72f, 0.35f, 0.28f];

    public bool IsGroup => Kind.Equals("group", StringComparison.OrdinalIgnoreCase);

    public ConceptPartRecord Clone()
    {
        return new ConceptPartRecord
        {
            Id = Guid.NewGuid(),
            ParentId = ParentId,
            Name = Name + " copy",
            Kind = Kind,
            Center = (float[])Center.Clone(),
            HalfExtents = (float[])HalfExtents.Clone(),
            Radius = Radius,
            Height = Height,
            RotationY = RotationY,
            Scale = (float[])Scale.Clone(),
            Material = Material,
            Color = (float[])Color.Clone(),
        };
    }

    public string Summary
    {
        get
        {
            if (IsGroup)
                return $"{Name} — group";

            return Kind.ToLowerInvariant() switch
            {
                "sphere" => $"{Name} — sphere r={Radius:0.##}",
                "cylinder" => $"{Name} — cylinder r={Radius:0.##} h={Height:0.##}",
                "cone" => $"{Name} — cone r={Radius:0.##} h={Height:0.##}",
                "wedge" => $"{Name} — wedge",
                _ => $"{Name} — box {HalfExtents[0]:0.##}×{HalfExtents[1]:0.##}×{HalfExtents[2]:0.##}",
            };
        }
    }
}

internal sealed class AnnotationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string View { get; set; } = "profile";

    public string Type { get; set; } = "linear";

    public float[] From { get; set; } = [0f, 0f, 0f];

    public float[] To { get; set; } = [1f, 0f, 0f];

    public string? Label { get; set; }
}
