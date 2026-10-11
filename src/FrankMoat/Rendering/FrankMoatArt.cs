using FrankMoat.Actors;
using FrankMoat.Levels;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Rendering;

internal sealed class FrankMoatArt
{
    public required PlanarTextureId White { get; init; }

    public required PlanarTextureId SoftGlow { get; init; }

    public required PlanarTextureId Spark { get; init; }

    public required PlanarTextureId Smoke { get; init; }

    public required PlanarTextureId Blood { get; init; }

    public required PlanarTextureId Frank { get; init; }

    public required PlanarTextureId Shambler { get; init; }

    public required PlanarTextureId Runner { get; init; }

    public required PlanarTextureId Security { get; init; }

    public required PlanarTextureId Barrel { get; init; }

    public required PlanarTextureId Pickup { get; init; }

    public required PlanarTextureId Floor { get; init; }

    public required PlanarTextureId Steel { get; init; }

    public required PlanarTextureId Concrete { get; init; }

    public required PlanarTextureId Emergency { get; init; }

    public required PlanarTextureId Founder { get; init; }

    public required PlanarTextureId FlashCone { get; init; }

    public required PlanarTextureId Interact { get; init; }

    public PlanarTextureId Wall(WallMaterial material) => material switch
    {
        WallMaterial.Steel => Steel,
        WallMaterial.Emergency => Emergency,
        WallMaterial.Founder => Founder,
        _ => Concrete,
    };

    public PlanarTextureId Enemy(EnemyKind kind) => kind switch
    {
        EnemyKind.Runner => Runner,
        EnemyKind.ContaminatedSecurity => Security,
        _ => Shambler,
    };

    public static FrankMoatArt Create(PlanarTextureRegistry registry)
    {
        return new FrankMoatArt
        {
            White = registry.Solid(Rgba32.White, "white"),
            SoftGlow = registry.Disc(16, new Rgba32(255, 255, 255), 2f, "glow"),
            Spark = registry.Cross(16, new Rgba32(255, 236, 170, 230), "spark"),
            Smoke = registry.Disc(16, new Rgba32(90, 90, 96, 170), 1.4f, "smoke"),
            Blood = registry.Disc(16, new Rgba32(170, 16, 20, 230), 1.8f, "blood"),
            Frank = Actor(registry, 72, 88, 52, 36, 44, 28, "frank"),
            Shambler = Actor(registry, 130, 58, 48, 80, 24, 24, "shambler"),
            Runner = Actor(registry, 170, 74, 46, 112, 32, 26, "runner"),
            Security = Actor(registry, 70, 84, 102, 30, 48, 72, "security"),
            Barrel = Drum(registry),
            Pickup = Crate(registry),
            Floor = Surface(registry, new Rgba32(36, 40, 48), new Rgba32(50, 54, 60), "floor"),
            Steel = Surface(registry, new Rgba32(62, 68, 76), new Rgba32(96, 102, 110), "steel"),
            Concrete = Surface(registry, new Rgba32(54, 56, 58), new Rgba32(72, 72, 70), "concrete"),
            Emergency = Surface(registry, new Rgba32(76, 26, 24), new Rgba32(148, 34, 25), "emergency"),
            Founder = Surface(registry, new Rgba32(78, 58, 40), new Rgba32(148, 112, 62), "founder"),
            FlashCone = Cone(registry),
            Interact = registry.Cross(20, new Rgba32(240, 210, 100, 220), "interact"),
        };
    }

    private static PlanarTextureId Actor(
        PlanarTextureRegistry registry,
        byte bodyR,
        byte bodyG,
        byte bodyB,
        byte vestR,
        byte vestG,
        byte vestB,
        string name)
    {
        const int size = 32;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - (center + 1f);
                if (MathF.Sqrt(dx * dx + dy * dy) < 11f)
                {
                    var vest = MathF.Abs(dx) < 5.5f && dy is > -2f and < 7f;
                    pixels[y * size + x] = vest
                        ? new Rgba32(vestR, vestG, vestB)
                        : new Rgba32(bodyR, bodyG, bodyB);
                }

                var hx = x - center;
                var hy = y - (center - 7f);
                if (hx * hx + hy * hy < 16f)
                {
                    pixels[y * size + x] = new Rgba32(
                        (byte)Math.Min(255, bodyR + 16),
                        (byte)Math.Min(255, bodyG + 10),
                        (byte)Math.Min(255, bodyB + 8));
                }
            }
        }

        return registry.Register(pixels, size, size, name);
    }

    private static PlanarTextureId Drum(PlanarTextureRegistry registry)
    {
        const int size = 24;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = MathF.Sqrt(
                    (x - center) * (x - center) + (y - center) * (y - center));
                if (distance < 10f)
                {
                    pixels[y * size + x] = distance > 8.2f
                        ? new Rgba32(90, 18, 16)
                        : new Rgba32(176, 32, 28);
                }
            }
        }

        return registry.Register(pixels, size, size, "barrel");
    }

    private static PlanarTextureId Crate(PlanarTextureRegistry registry)
    {
        const int size = 20;
        var pixels = new Rgba32[size * size];
        for (var y = 3; y < 17; y++)
        {
            for (var x = 3; x < 17; x++)
            {
                var edge = x is 3 or 16 || y is 3 or 16;
                pixels[y * size + x] = edge
                    ? new Rgba32(220, 176, 64)
                    : new Rgba32(132, 92, 48);
            }
        }

        return registry.Register(pixels, size, size, "pickup");
    }

    private static PlanarTextureId Surface(
        PlanarTextureRegistry registry,
        Rgba32 dark,
        Rgba32 light,
        string name)
    {
        const int size = 64;
        var pixels = new Rgba32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var stripe = ((x / 8) + (y / 8)) % 2 == 0;
                pixels[y * size + x] = stripe ? dark : light;
            }
        }

        return registry.Register(pixels, size, size, name);
    }

    private static PlanarTextureId Cone(PlanarTextureRegistry registry)
    {
        const int size = 96;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - center) / center;
                var dy = (y - center) / center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= 1f && y < size * 0.86f)
                {
                    var alpha = (byte)(100f * MathF.Pow(1f - distance, 1.5f));
                    pixels[y * size + x] = new Rgba32(255, 236, 200, alpha);
                }
            }
        }

        return registry.Register(pixels, size, size, "flash-cone");
    }
}
