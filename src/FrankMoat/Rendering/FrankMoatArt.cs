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

    public required PlanarTextureId Counter { get; init; }

    public required PlanarTextureId Console { get; init; }

    public required PlanarTextureId Table { get; init; }

    public required PlanarTextureId Shelf { get; init; }

    public required PlanarTextureId Elevator { get; init; }

    public required PlanarTextureId Cabinet { get; init; }

    public required PlanarTextureId PipeBank { get; init; }

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

    public PlanarTextureId Prop(PropKind kind) => kind switch
    {
        PropKind.Counter => Counter,
        PropKind.Console => Console,
        PropKind.Table => Table,
        PropKind.Shelf => Shelf,
        PropKind.Elevator => Elevator,
        PropKind.Cabinet => Cabinet,
        _ => PipeBank,
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
            Floor = Surface(registry, new Rgba32(36, 40, 48), new Rgba32(48, 52, 60), "floor"),
            Steel = Surface(registry, new Rgba32(62, 68, 76), new Rgba32(88, 96, 104), "steel"),
            Concrete = Surface(registry, new Rgba32(54, 56, 58), new Rgba32(68, 70, 72), "concrete"),
            Emergency = Surface(registry, new Rgba32(76, 26, 24), new Rgba32(132, 34, 28), "emergency"),
            Founder = Surface(registry, new Rgba32(78, 58, 40), new Rgba32(132, 98, 58), "founder"),
            FlashCone = Cone(registry),
            Interact = registry.Cross(20, new Rgba32(240, 210, 100, 220), "interact"),
            Counter = Furniture(registry, PropKind.Counter),
            Console = Furniture(registry, PropKind.Console),
            Table = Furniture(registry, PropKind.Table),
            Shelf = Furniture(registry, PropKind.Shelf),
            Elevator = Furniture(registry, PropKind.Elevator),
            Cabinet = Furniture(registry, PropKind.Cabinet),
            PipeBank = Furniture(registry, PropKind.PipeBank),
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
        const int size = 96;
        var pixels = new Rgba32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tileX = x / 24;
                var tileY = y / 24;
                var alternate = (tileX + tileY) % 3 == 0;
                var edge = x % 24 == 0 || y % 24 == 0;
                pixels[y * size + x] = edge
                    ? dark
                    : alternate
                        ? Rgba32.Lerp(dark, light, 0.28f)
                        : Rgba32.Lerp(dark, light, 0.12f);
            }
        }

        return registry.Register(pixels, size, size, name);
    }

    private static PlanarTextureId Furniture(
        PlanarTextureRegistry registry,
        PropKind kind)
    {
        const int size = 64;
        var pixels = new Rgba32[size * size];
        var edge = new Rgba32(20, 23, 28, 245);
        var body = kind switch
        {
            PropKind.Counter => new Rgba32(136, 94, 52, 255),
            PropKind.Console => new Rgba32(60, 90, 104, 255),
            PropKind.Table => new Rgba32(112, 82, 48, 255),
            PropKind.Shelf => new Rgba32(94, 101, 108, 255),
            PropKind.Elevator => new Rgba32(78, 88, 98, 255),
            PropKind.Cabinet => new Rgba32(74, 82, 88, 255),
            _ => new Rgba32(48, 92, 100, 255),
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var filled = kind switch
                {
                    PropKind.Counter => x is >= 4 and < 60 && y is >= 16 and < 52,
                    PropKind.Console => x is >= 16 and < 48 && y is >= 14 and < 58,
                    PropKind.Table => x is >= 6 and < 58 && y is >= 12 and < 52,
                    PropKind.Shelf => x is >= 5 and < 59 && y is >= 10 and < 55,
                    PropKind.Elevator => x is >= 5 and < 59 && y is >= 5 and < 59,
                    PropKind.Cabinet => x is >= 12 and < 52 && y is >= 6 and < 58,
                    _ => x is >= 4 and < 60 && y is >= 22 and < 44,
                };
                if (!filled)
                {
                    continue;
                }

                var border = x <= 6 || x >= 57 || y <= 6 || y >= 56;
                pixels[y * size + x] = border ? edge : body;
            }
        }

        if (kind == PropKind.Console)
        {
            for (var y = 18; y < 33; y++)
            for (var x = 20; x < 44; x++)
                pixels[y * size + x] = new Rgba32(105, 205, 202, 240);
        }
        else if (kind == PropKind.Elevator)
        {
            for (var y = 11; y < 54; y++)
            {
                pixels[y * size + 31] = new Rgba32(184, 148, 70, 255);
                pixels[y * size + 32] = new Rgba32(184, 148, 70, 255);
            }
        }
        else if (kind == PropKind.PipeBank)
        {
            for (var y = 24; y < 40; y++)
            for (var x = 8; x < 56; x++)
                if (x % 12 < 4)
                    pixels[y * size + x] = new Rgba32(166, 192, 198, 230);
        }

        return registry.Register(pixels, size, size, $"prop-{kind.ToString().ToLowerInvariant()}");
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
