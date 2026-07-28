namespace DraftStudio.Models;

/// <summary><c>novolis.cad.phys</c> document (.cadphys.json).</summary>
internal sealed class CadPhysDocument
{
    public string Format { get; set; } = "novolis.cad.phys";

    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    public CadGenerator Generator { get; set; } = new();

    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }

    public float UnitScaleMeters { get; set; } = 1f;

    public string LinearUnit { get; set; } = "meter";

    public string AngleUnit { get; set; } = "radian";

    public string UpAxis { get; set; } = "y";

    public string? BaseDocument { get; set; }

    public List<CadMesh> Meshes { get; set; } = [];

    public List<CadCollider> Colliders { get; set; } = [];
}

internal sealed class CadMesh
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public Guid? EntityId { get; set; }

    public List<float[]> Vertices { get; set; } = [];

    public List<int> Indices { get; set; } = [];

    public List<float[]>? Normals { get; set; }

    public string Winding { get; set; } = "ccw";

    public string Space { get; set; } = "local";

    public string? Material { get; set; }
}

internal sealed class CadCollider
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? EntityId { get; set; }

    public string Kind { get; set; } = "box";

    public float[]? Center { get; set; }

    public float[]? HalfExtents { get; set; }

    public float Radius { get; set; }

    public float[]? A { get; set; }

    public float[]? B { get; set; }

    public Guid? MeshId { get; set; }

    public bool IsTrigger { get; set; }

    public CadColliderBody? Body { get; set; }
}

internal sealed class CadColliderBody
{
    public float Mass { get; set; } = 1f;

    public float[] InertiaDiagonal { get; set; } = [1f, 1f, 1f];

    public bool Kinematic { get; set; }
}
