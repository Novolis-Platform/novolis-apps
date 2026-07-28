using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DraftStudio.Models;

namespace DraftStudio.Services;

/// <summary>Tessellates analytic solids from <c>novolis.cad</c> into <c>novolis.cad.phys</c>.</summary>
internal sealed class CadPhysExporter
{
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public CadPhysDocument Build(CadDocument document, string? baseDocumentRelative = "draft.cadjson")
    {
        var now = DateTime.UtcNow.ToString("O");
        var phys = new CadPhysDocument
        {
            Name = document.Name,
            UnitScaleMeters = document.UnitScaleMeters,
            BaseDocument = baseDocumentRelative,
            CreatedAt = now,
            ModifiedAt = now,
            Generator = new CadGenerator { Name = "DraftStudio", Version = "2026.1.0" },
        };

        foreach (var entity in document.Entities.Where(e => e.IsSolid))
        {
            switch (entity.Kind.ToLowerInvariant())
            {
                case "box" when entity.Center is not null && entity.HalfExtents is { Length: >= 3 }:
                {
                    var he = CadVec.To(entity.HalfExtents);
                    var (mesh, verts, inds) = BoxMeshLocal(he);
                    mesh.EntityId = entity.Id;
                    mesh.Name = (entity.Name ?? "box") + "-mesh";
                    mesh.Material = entity.Material;
                    phys.Meshes.Add(mesh);
                    phys.Colliders.Add(new CadCollider
                    {
                        EntityId = entity.Id,
                        Kind = "box",
                        Center = (float[])entity.Center.Clone(),
                        HalfExtents = (float[])entity.HalfExtents.Clone(),
                        Body = new CadColliderBody { Mass = 1f, InertiaDiagonal = BoxInertia(1f, he), Kinematic = false },
                    });
                    _ = verts;
                    _ = inds;
                    break;
                }
                case "sphere" when entity.Center is not null:
                {
                    var (mesh, _) = SphereMeshLocal(entity.Radius, 16, 24);
                    mesh.EntityId = entity.Id;
                    mesh.Name = (entity.Name ?? "sphere") + "-mesh";
                    mesh.Material = entity.Material;
                    phys.Meshes.Add(mesh);
                    phys.Colliders.Add(new CadCollider
                    {
                        EntityId = entity.Id,
                        Kind = "sphere",
                        Center = (float[])entity.Center.Clone(),
                        Radius = entity.Radius,
                        Body = new CadColliderBody
                        {
                            Mass = 1f,
                            InertiaDiagonal =
                            [
                                0.4f * entity.Radius * entity.Radius,
                                0.4f * entity.Radius * entity.Radius,
                                0.4f * entity.Radius * entity.Radius,
                            ],
                        },
                    });
                    break;
                }
                case "cylinder" when entity.Center is not null:
                {
                    var (mesh, _) = CylinderMeshLocal(entity.Radius, entity.Height, 24);
                    mesh.EntityId = entity.Id;
                    mesh.Name = (entity.Name ?? "cylinder") + "-mesh";
                    mesh.Material = entity.Material;
                    phys.Meshes.Add(mesh);
                    phys.Colliders.Add(new CadCollider
                    {
                        EntityId = entity.Id,
                        Kind = "capsule",
                        A = CadVec.Xyz(entity.Center[0], entity.Center[1] - entity.Height * 0.5f, entity.Center[2]),
                        B = CadVec.Xyz(entity.Center[0], entity.Center[1] + entity.Height * 0.5f, entity.Center[2]),
                        Radius = entity.Radius,
                        Body = new CadColliderBody { Mass = 1f },
                    });
                    break;
                }
            }
        }

        return phys;
    }

    public void Write(CadPhysDocument phys, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(phys, _json));
    }

    private static float[] BoxInertia(float mass, Vector3 he)
    {
        var w = he.X * 2f;
        var h = he.Y * 2f;
        var d = he.Z * 2f;
        return
        [
            mass * (h * h + d * d) / 12f,
            mass * (w * w + d * d) / 12f,
            mass * (w * w + h * h) / 12f,
        ];
    }

    private static (CadMesh Mesh, List<Vector3> Verts, List<int> Inds) BoxMeshLocal(Vector3 he)
    {
        var verts = new List<Vector3>
        {
            new(-he.X, -he.Y, -he.Z), new(he.X, -he.Y, -he.Z), new(he.X, he.Y, -he.Z), new(-he.X, he.Y, -he.Z),
            new(-he.X, -he.Y, he.Z), new(he.X, -he.Y, he.Z), new(he.X, he.Y, he.Z), new(-he.X, he.Y, he.Z),
        };
        var inds = new List<int>
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            2, 6, 7, 2, 7, 3,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2,
        };
        return (ToMesh(verts, inds), verts, inds);
    }

    private static (CadMesh Mesh, List<Vector3> Verts) SphereMeshLocal(float radius, int rings, int slices)
    {
        var verts = new List<Vector3>();
        var inds = new List<int>();
        for (var y = 0; y <= rings; y++)
        {
            var v = y / (float)rings;
            var phi = v * MathF.PI;
            for (var x = 0; x <= slices; x++)
            {
                var u = x / (float)slices;
                var theta = u * MathF.PI * 2f;
                verts.Add(new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta) * radius,
                    MathF.Cos(phi) * radius,
                    MathF.Sin(phi) * MathF.Sin(theta) * radius));
            }
        }

        for (var y = 0; y < rings; y++)
        {
            for (var x = 0; x < slices; x++)
            {
                var i0 = y * (slices + 1) + x;
                var i1 = i0 + slices + 1;
                inds.Add(i0);
                inds.Add(i1);
                inds.Add(i0 + 1);
                inds.Add(i0 + 1);
                inds.Add(i1);
                inds.Add(i1 + 1);
            }
        }

        return (ToMesh(verts, inds), verts);
    }

    private static (CadMesh Mesh, List<Vector3> Verts) CylinderMeshLocal(float radius, float height, int slices)
    {
        var verts = new List<Vector3>();
        var inds = new List<int>();
        var hy = height * 0.5f;
        for (var i = 0; i <= slices; i++)
        {
            var a = i / (float)slices * MathF.PI * 2f;
            var x = MathF.Cos(a) * radius;
            var z = MathF.Sin(a) * radius;
            verts.Add(new Vector3(x, -hy, z));
            verts.Add(new Vector3(x, hy, z));
        }

        for (var i = 0; i < slices; i++)
        {
            var i0 = i * 2;
            inds.Add(i0);
            inds.Add(i0 + 1);
            inds.Add(i0 + 2);
            inds.Add(i0 + 2);
            inds.Add(i0 + 1);
            inds.Add(i0 + 3);
        }

        return (ToMesh(verts, inds), verts);
    }

    private static CadMesh ToMesh(List<Vector3> verts, List<int> inds) =>
        new()
        {
            Vertices = verts.Select(CadVec.From).ToList(),
            Indices = inds,
            Space = "local",
            Winding = "ccw",
        };
}
