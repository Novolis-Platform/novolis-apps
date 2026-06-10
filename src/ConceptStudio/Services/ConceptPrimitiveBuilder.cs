using System.Numerics;
using ConceptStudio.Models;
using Novolis.Rendering.Materials;

namespace ConceptStudio.Services;

internal static class ConceptMaterialMapper
{
    public static IMaterial Resolve(ConceptPartRecord part)
    {
        var color = ToVector3(part.Color);
        var kind = part.Material.ToLowerInvariant();
        return kind switch
        {
            "metal" => MaterialPresets.Metal(color, roughness: 0.12f),
            "glass" => MaterialPresets.Glass(color, roughness: 0.02f, ior: 1.45f),
            "engineglow" => MaterialPresets.Emissive(color, strength: 3f),
            "hulldark" => MaterialPresets.Standard(color, roughness: 0.9f),
            _ => MaterialPresets.Standard(color, roughness: 0.85f),
        };
    }

    public static Vector3 ToVector3(float[] values) =>
        values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : new Vector3(0.7f);
}

internal static class ConceptPrimitiveBuilder
{
    public const int DefaultSegments = 24;

    public static Matrix4x4 BuildTransform(ConceptPartRecord part)
    {
        var center = ConceptMaterialMapper.ToVector3(part.Center);
        var scale = part.Scale.Length >= 3
            ? new Vector3(part.Scale[0], part.Scale[1], part.Scale[2])
            : Vector3.One;
        var rot = Matrix4x4.CreateRotationY(part.RotationY * MathF.PI / 180f);
        var scl = Matrix4x4.CreateScale(scale);
        var tr = Matrix4x4.CreateTranslation(center);
        return scl * rot * tr;
    }

    public static void AddPart(Novolis.Rendering.Scene.SceneBuilder builder, ConceptPartRecord part)
    {
        if (part.IsGroup)
            return;

        var material = ConceptMaterialMapper.Resolve(part);
        var transform = BuildTransform(part);

        switch (part.Kind.ToLowerInvariant())
        {
            case "sphere":
                AddSphere(builder, part.Radius, material, transform);
                break;
            case "cylinder":
                AddCylinder(builder, part.Radius, part.Height, material, transform);
                break;
            case "cone":
                AddCone(builder, part.Radius, part.Height, material, transform);
                break;
            case "wedge":
                AddWedge(builder, part.HalfExtents, material, transform);
                break;
            default:
                AddBox(builder, part.HalfExtents, material, transform);
                break;
        }
    }

    public static void AddBox(Novolis.Rendering.Scene.SceneBuilder builder, float[] halfExtents, IMaterial material, Matrix4x4 transform)
    {
        var he = halfExtents.Length >= 3
            ? new Vector3(halfExtents[0], halfExtents[1], halfExtents[2])
            : new Vector3(0.5f);
        var (vertices, indices) = CreateBoxMesh(he);
        builder.AddMesh(vertices, indices, material, transform);
    }

    public static void AddSphere(Novolis.Rendering.Scene.SceneBuilder builder, float radius, IMaterial material, Matrix4x4 transform, int segments = DefaultSegments)
    {
        var (vertices, indices) = CreateUvSphere(radius, segments);
        builder.AddMesh(vertices, indices, material, transform);
    }

    public static void AddCylinder(Novolis.Rendering.Scene.SceneBuilder builder, float radius, float height, IMaterial material, Matrix4x4 transform, int segments = DefaultSegments)
    {
        var (vertices, indices) = CreateCylinderMesh(radius, height, segments);
        builder.AddMesh(vertices, indices, material, transform);
    }

    public static void AddCone(Novolis.Rendering.Scene.SceneBuilder builder, float radius, float height, IMaterial material, Matrix4x4 transform, int segments = DefaultSegments)
    {
        var (vertices, indices) = CreateConeMesh(radius, height, segments);
        builder.AddMesh(vertices, indices, material, transform);
    }

    public static void AddWedge(Novolis.Rendering.Scene.SceneBuilder builder, float[] halfExtents, IMaterial material, Matrix4x4 transform)
    {
        var he = halfExtents.Length >= 3
            ? new Vector3(halfExtents[0], halfExtents[1], halfExtents[2])
            : new Vector3(0.5f);
        var (vertices, indices) = CreateWedgeMesh(he);
        builder.AddMesh(vertices, indices, material, transform);
    }

    public static (Vector3[] Vertices, int[] Indices) CreateBoxMesh(Vector3 halfExtents)
    {
        var hx = halfExtents.X;
        var hy = halfExtents.Y;
        var hz = halfExtents.Z;
        var vertices = new Vector3[]
        {
            new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, hy, -hz), new(-hx, hy, -hz),
            new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz),
        };
        var indices = new[]
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            2, 6, 7, 2, 7, 3,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2,
        };
        return (vertices, indices);
    }

    public static (Vector3[] Vertices, int[] Indices) CreateUvSphere(float radius, int segments)
    {
        segments = Math.Clamp(segments, 8, 64);
        var rings = segments;
        var sectors = segments * 2;
        var vertices = new List<Vector3>((rings + 1) * (sectors + 1));
        for (var r = 0; r <= rings; r++)
        {
            var v = r / (float)rings;
            var phi = v * MathF.PI;
            for (var s = 0; s <= sectors; s++)
            {
                var u = s / (float)sectors;
                var theta = u * MathF.Tau;
                var x = MathF.Sin(phi) * MathF.Cos(theta);
                var y = MathF.Cos(phi);
                var z = MathF.Sin(phi) * MathF.Sin(theta);
                vertices.Add(new Vector3(x, y, z) * radius);
            }
        }

        var indices = new List<int>();
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < sectors; s++)
            {
                var a = r * (sectors + 1) + s;
                var b = a + sectors + 1;
                indices.Add(a);
                indices.Add(b);
                indices.Add(a + 1);
                indices.Add(b);
                indices.Add(b + 1);
                indices.Add(a + 1);
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    public static (Vector3[] Vertices, int[] Indices) CreateCylinderMesh(float radius, float height, int segments)
    {
        segments = Math.Clamp(segments, 8, 64);
        var halfH = height * 0.5f;
        var ring = new Vector3[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            ring[i] = new Vector3(MathF.Cos(a) * radius, 0f, MathF.Sin(a) * radius);
        }

        var vertices = new List<Vector3>(segments * 2 + 2);
        var indices = new List<int>();
        for (var i = 0; i < segments; i++)
            vertices.Add(ring[i] + new Vector3(0f, -halfH, 0f));
        for (var i = 0; i < segments; i++)
            vertices.Add(ring[i] + new Vector3(0f, halfH, 0f));

        var bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -halfH, 0f));
        var topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, halfH, 0f));

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var b0 = i;
            var b1 = next;
            var t0 = segments + i;
            var t1 = segments + next;
            indices.AddRange([b0, t0, b1, b1, t0, t1]);
            indices.AddRange([bottomCenter, b1, b0]);
            indices.AddRange([topCenter, t0, t1]);
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    public static (Vector3[] Vertices, int[] Indices) CreateConeMesh(float radius, float height, int segments)
    {
        segments = Math.Clamp(segments, 8, 64);
        var vertices = new List<Vector3>(segments + 2);
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            vertices.Add(new Vector3(MathF.Cos(a) * radius, -height * 0.5f, MathF.Sin(a) * radius));
        }

        var apex = vertices.Count;
        vertices.Add(new Vector3(0f, height * 0.5f, 0f));
        var baseCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -height * 0.5f, 0f));

        var indices = new List<int>();
        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            indices.AddRange([i, apex, next]);
            indices.AddRange([baseCenter, next, i]);
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    public static (Vector3[] Vertices, int[] Indices) CreateWedgeMesh(Vector3 halfExtents)
    {
        var hx = halfExtents.X;
        var hy = halfExtents.Y;
        var hz = halfExtents.Z;
        var vertices = new Vector3[]
        {
            new(-hx, -hy, -hz),
            new(hx, -hy, -hz),
            new(hx, -hy, hz),
            new(-hx, -hy, hz),
            new(-hx, hy, hz),
            new(hx * 0.2f, hy, -hz * 0.3f),
        };
        var indices = new[]
        {
            0, 1, 2, 0, 2, 3,
            3, 2, 4, 2, 5, 4,
            0, 3, 4, 0, 4, 5,
            0, 5, 1, 1, 5, 2,
            1, 2, 5,
        };
        return (vertices, indices);
    }
}
