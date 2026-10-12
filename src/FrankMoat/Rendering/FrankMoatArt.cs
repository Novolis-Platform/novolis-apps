using FrankMoat.Actors;
using FrankMoat.Levels;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Rendering;

internal sealed class FrankMoatArt
{
    public required PlanarTextureId White { get; init; }

    public required PlanarTextureId SoftGlow { get; init; }

    public required PlanarTextureId Shadow { get; init; }

    public required PlanarTextureId Spark { get; init; }

    public required PlanarTextureId Smoke { get; init; }

    public required PlanarTextureId Blood { get; init; }

    public required PlanarTextureId Frank { get; init; }

    public required PlanarTextureId PlayerWeapon { get; init; }

    public required IReadOnlyDictionary<WeaponId, PlanarTextureId> WeaponTextures { get; init; }

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

    public required PlanarTextureId CeilingFixture { get; init; }

    public required PlanarTextureId AlarmBeacon { get; init; }

    public required PlanarTextureId AlarmSweep { get; init; }

    public required PlanarTextureId PropSide { get; init; }

    public required PlanarTextureId Interact { get; init; }

    public required PlanarTextureId Counter { get; init; }

    public required PlanarTextureId Console { get; init; }

    public required PlanarTextureId Table { get; init; }

    public required PlanarTextureId Shelf { get; init; }

    public required PlanarTextureId Elevator { get; init; }

    public required PlanarTextureId Cabinet { get; init; }

    public required PlanarTextureId PipeBank { get; init; }

    public required PlanarTextureId PortalFrame { get; init; }

    public required PlanarTextureId GlassPanel { get; init; }

    public required PlanarTextureId Vent { get; init; }

    public required PlanarTextureId HazardStripe { get; init; }

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
        PropKind.PortalFrame => PortalFrame,
        PropKind.GlassPanel => GlassPanel,
        PropKind.Vent => Vent,
        PropKind.HazardStripe => HazardStripe,
        _ => PipeBank,
    };

    public PlanarTextureId Weapon(WeaponId id) =>
        WeaponTextures.TryGetValue(id, out var texture)
            ? texture
            : PlayerWeapon;

    public Rgba32 WallSideTint(WallMaterial material) => material switch
    {
        WallMaterial.Steel => new Rgba32(152, 168, 184),
        WallMaterial.Emergency => new Rgba32(190, 76, 64),
        WallMaterial.Founder => new Rgba32(188, 148, 98),
        _ => new Rgba32(170, 178, 186),
    };

    public Rgba32 WallCapTint(WallMaterial material) => material switch
    {
        WallMaterial.Steel => new Rgba32(218, 228, 236),
        WallMaterial.Emergency => new Rgba32(255, 128, 100),
        WallMaterial.Founder => new Rgba32(242, 198, 132),
        _ => new Rgba32(226, 232, 238),
    };

    public Rgba32 PropSideTint(PropKind kind) => kind switch
    {
        PropKind.Counter => new Rgba32(148, 105, 66),
        PropKind.Console => new Rgba32(78, 112, 128),
        PropKind.Table => new Rgba32(126, 92, 58),
        PropKind.Shelf => new Rgba32(108, 120, 132),
        PropKind.Elevator => new Rgba32(92, 104, 118),
        PropKind.Cabinet => new Rgba32(88, 98, 108),
        PropKind.PortalFrame => new Rgba32(106, 124, 138),
        PropKind.GlassPanel => new Rgba32(48, 134, 150),
        PropKind.Vent => new Rgba32(86, 96, 106),
        PropKind.HazardStripe => new Rgba32(184, 126, 42),
        _ => new Rgba32(62, 112, 122),
    };

    public Rgba32 PropCapTint(PropKind kind) => kind switch
    {
        PropKind.Counter => new Rgba32(194, 135, 76),
        PropKind.Console => new Rgba32(104, 164, 176),
        PropKind.Table => new Rgba32(170, 118, 68),
        PropKind.Shelf => new Rgba32(142, 154, 166),
        PropKind.Elevator => new Rgba32(122, 136, 150),
        PropKind.Cabinet => new Rgba32(116, 126, 136),
        PropKind.PortalFrame => new Rgba32(142, 174, 186),
        PropKind.GlassPanel => new Rgba32(84, 220, 224),
        PropKind.Vent => new Rgba32(124, 140, 150),
        PropKind.HazardStripe => new Rgba32(242, 188, 62),
        _ => new Rgba32(90, 148, 156),
    };

    public static FrankMoatArt Create(PlanarTextureRegistry registry)
    {
        return new FrankMoatArt
        {
            White = registry.Solid(Rgba32.White, "white"),
            SoftGlow = registry.Disc(64, new Rgba32(255, 255, 255), 2.6f, "glow"),
            Shadow = registry.Disc(48, new Rgba32(12, 15, 20, 210), 1.2f, "shadow"),
            Spark = registry.Cross(16, new Rgba32(255, 236, 170, 230), "spark"),
            Smoke = registry.Disc(16, new Rgba32(90, 90, 96, 170), 1.4f, "smoke"),
            Blood = registry.Disc(16, new Rgba32(170, 16, 20, 230), 1.8f, "blood"),
            Frank = Actor(registry, 72, 88, 52, 36, 44, 28, "frank"),
            PlayerWeapon = registry.Capsule(40, new Rgba32(38, 43, 50, 255), "frank-weapon"),
            WeaponTextures = WeaponCatalog.All.ToDictionary(
                weapon => weapon.Id,
                weapon =>
                {
                    var visual = WeaponVisualCatalog.For(weapon.Id);
                    return Weapon(registry, visual, $"weapon-{weapon.Id}");
                }),
            Shambler = Actor(
                registry,
                130,
                58,
                48,
                80,
                24,
                24,
                "shambler",
                EnemyKind.Shambler),
            Runner = Actor(
                registry,
                170,
                74,
                46,
                112,
                32,
                26,
                "runner",
                EnemyKind.Runner),
            Security = Actor(
                registry,
                70,
                84,
                102,
                30,
                48,
                72,
                "security",
                EnemyKind.ContaminatedSecurity),
            Barrel = Drum(registry),
            Pickup = Crate(registry),
            Floor = Surface(registry, new Rgba32(44, 50, 60), new Rgba32(62, 70, 82), "floor"),
            Steel = Surface(registry, new Rgba32(62, 68, 76), new Rgba32(88, 96, 104), "steel"),
            Concrete = Surface(registry, new Rgba32(54, 56, 58), new Rgba32(68, 70, 72), "concrete"),
            Emergency = Surface(registry, new Rgba32(76, 26, 24), new Rgba32(132, 34, 28), "emergency"),
            Founder = Surface(registry, new Rgba32(78, 58, 40), new Rgba32(132, 98, 58), "founder"),
            FlashCone = Cone(registry),
            CeilingFixture = Fixture(registry),
            AlarmBeacon = Beacon(registry),
            AlarmSweep = Cone(registry, new Rgba32(255, 42, 32), "alarm-sweep"),
            PropSide = Surface(
                registry,
                new Rgba32(28, 33, 40),
                new Rgba32(62, 70, 80),
                "prop-side"),
            Interact = registry.Cross(20, new Rgba32(240, 210, 100, 220), "interact"),
            Counter = Furniture(registry, PropKind.Counter),
            Console = Furniture(registry, PropKind.Console),
            Table = Furniture(registry, PropKind.Table),
            Shelf = Furniture(registry, PropKind.Shelf),
            Elevator = Furniture(registry, PropKind.Elevator),
            Cabinet = Furniture(registry, PropKind.Cabinet),
            PipeBank = Furniture(registry, PropKind.PipeBank),
            PortalFrame = Furniture(registry, PropKind.PortalFrame),
            GlassPanel = Furniture(registry, PropKind.GlassPanel),
            Vent = Furniture(registry, PropKind.Vent),
            HazardStripe = Furniture(registry, PropKind.HazardStripe),
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
        string name,
        EnemyKind? enemyKind = null)
    {
        const int size = 48;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = default(Rgba32);
                var leg = y is >= 29 and < 43
                    && (x is >= 14 and < 21 || x is >= 27 and < 34);
                var torso = enemyKind switch
                {
                    EnemyKind.Runner => x is >= 15 and < 33 && y is >= 15 and < 38,
                    EnemyKind.ContaminatedSecurity => x is >= 8 and < 40 && y is >= 13 and < 37,
                    _ => x is >= 10 and < 38 && y is >= 13 and < 36,
                };
                var headDx = x - center;
                var headDy = y - 10.5f;
                var head = headDx * headDx + headDy * headDy <= 8.5f * 8.5f;
                if (leg || torso || head)
                {
                    var edge = (leg && (x is 14 or 20 or 27 or 33 || y is 29 or 42))
                        || (torso && (x is 11 or 36 || y is 15 or 34))
                        || (head && headDx * headDx + headDy * headDy > 6.7f * 6.7f);
                    pixel = edge
                        ? new Rgba32(22, 26, 31)
                        : head
                            ? new Rgba32(
                                (byte)Math.Min(255, bodyR + 18),
                                (byte)Math.Min(255, bodyG + 14),
                                (byte)Math.Min(255, bodyB + 10))
                            : new Rgba32(bodyR, bodyG, bodyB);
                }

                if (torso && x is >= 18 and < 30 && y is >= 18 and < 31)
                {
                    pixel = new Rgba32(vestR, vestG, vestB);
                }

                if (enemyKind == EnemyKind.Runner
                    && torso
                    && (x is >= 11 and < 16 || x is >= 32 and < 37)
                    && y is >= 21 and < 33)
                {
                    pixel = new Rgba32(bodyR, bodyG, bodyB);
                }

                if (enemyKind == EnemyKind.ContaminatedSecurity
                    && torso
                    && y is >= 16 and <= 19
                    && x is >= 12 and < 36)
                {
                    pixel = new Rgba32(186, 210, 220);
                }

                if (torso && y is >= 15 and <= 18 && x is >= 16 and <= 31)
                {
                    pixel = new Rgba32(206, 178, 96);
                }

                if (x is >= 7 and < 13 && y is >= 18 and < 32)
                {
                    pixel = new Rgba32(30, 35, 42);
                }

                if (head && y is >= 7 and <= 10 && x is >= 21 and <= 26)
                {
                    pixel = new Rgba32(228, 210, 160);
                }

                pixels[y * size + x] = pixel;
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
            PropKind.PortalFrame => new Rgba32(94, 112, 126, 255),
            PropKind.GlassPanel => new Rgba32(48, 134, 150, 210),
            PropKind.Vent => new Rgba32(72, 80, 88, 255),
            PropKind.HazardStripe => new Rgba32(208, 148, 44, 255),
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
                    PropKind.PortalFrame =>
                        x is < 10 or >= 54 || y is < 10 or >= 54,
                    PropKind.GlassPanel => x is >= 5 and < 59 && y is >= 5 and < 59,
                    PropKind.Vent => x is >= 5 and < 59 && y is >= 16 and < 48,
                    PropKind.HazardStripe => x is >= 5 and < 59 && y is >= 14 and < 50,
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
        else if (kind == PropKind.GlassPanel)
        {
            for (var x = 11; x < 53; x += 8)
            for (var y = 8; y < 56; y++)
                pixels[y * size + x] = new Rgba32(150, 244, 242, 210);
        }
        else if (kind == PropKind.Vent)
        {
            for (var y = 20; y < 44; y += 6)
            for (var x = 10; x < 54; x++)
                pixels[y * size + x] = new Rgba32(164, 180, 188, 210);
        }
        else if (kind == PropKind.HazardStripe)
        {
            for (var y = 16; y < 50; y++)
            for (var x = 8; x < 56; x++)
                if ((x + y) % 12 < 6)
                    pixels[y * size + x] = new Rgba32(32, 36, 40, 255);
        }

        return registry.Register(pixels, size, size, $"prop-{kind.ToString().ToLowerInvariant()}");
    }

    private static PlanarTextureId Cone(PlanarTextureRegistry registry) =>
        Cone(registry, new Rgba32(255, 236, 200), "flash-cone");

    private static PlanarTextureId Cone(
        PlanarTextureRegistry registry,
        Rgba32 color,
        string name)
    {
        const int size = 160;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var depth = Math.Clamp((y - size * 0.08f) / (size * 0.84f), 0f, 1f);
                var halfWidth = 0.025f + depth * 0.46f;
                var horizontal = MathF.Abs(x - center) / center;
                if (horizontal <= halfWidth)
                {
                    var edge = 1f - horizontal / halfWidth;
                    var core = MathF.Pow(edge, 0.65f);
                    var alpha = (byte)Math.Clamp(
                        (int)(34f + 180f * core * (0.9f - depth * 0.25f)),
                        0,
                        224);
                    pixels[y * size + x] = new Rgba32(color.R, color.G, color.B, alpha);
                }
            }
        }

        return registry.Register(pixels, size, size, name);
    }

    private static PlanarTextureId Beacon(PlanarTextureRegistry registry)
    {
        const int size = 48;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = MathF.Sqrt(
                    (x - center) * (x - center)
                    + (y - center) * (y - center));
                pixels[y * size + x] = distance switch
                {
                    <= 8f => new Rgba32(255, 72, 54, 255),
                    <= 11f => new Rgba32(116, 18, 22, 255),
                    <= 17f => new Rgba32(255, 46, 36, 110),
                    _ => default,
                };
            }
        }

        for (var x = 14; x < 34; x++)
        {
            pixels[23 * size + x] = new Rgba32(255, 220, 156, 230);
            pixels[24 * size + x] = new Rgba32(255, 220, 156, 230);
        }

        return registry.Register(pixels, size, size, "alarm-beacon");
    }

    private static PlanarTextureId Weapon(
        PlanarTextureRegistry registry,
        WeaponVisualDefinition visual,
        string name)
    {
        const int size = 96;
        var pixels = new Rgba32[size * size];
        var center = size / 2f;
        var bodyHalf = Math.Clamp((int)(visual.Width / 0.42f * 8f), 5, 14);
        var receiverStart = visual.Heavy ? 28 : 34;
        var receiverEnd = visual.Heavy ? 62 : 58;
        for (var y = 7; y < 88; y++)
        {
            var halfWidth = y < receiverStart
                ? Math.Max(2, bodyHalf / 2)
                : y < receiverEnd
                    ? bodyHalf
                    : Math.Max(3, bodyHalf - 2);
            for (var x = 0; x < size; x++)
            {
                if (Math.Abs(x - center) > halfWidth)
                {
                    continue;
                }

                var edge = Math.Abs(x - center) >= halfWidth - 1
                    || y is 7 or 87;
                pixels[y * size + x] = edge
                    ? new Rgba32(18, 21, 26, 255)
                    : visual.Body;
            }
        }

        for (var y = receiverStart + 4; y < receiverEnd - 3; y++)
        {
            pixels[y * size + (int)center] = visual.Accent;
            if (visual.Heavy && y % 4 == 0)
            {
                pixels[y * size + (int)center - 1] = visual.Accent;
                pixels[y * size + (int)center + 1] = visual.Accent;
            }
        }

        for (var x = (int)center - 3; x <= (int)center + 3; x++)
        {
            pixels[5 * size + x] = new Rgba32(244, 224, 158, 255);
            pixels[6 * size + x] = new Rgba32(244, 224, 158, 255);
        }

        return registry.Register(pixels, size, size, name);
    }

    private static PlanarTextureId Fixture(PlanarTextureRegistry registry)
    {
        const int width = 64;
        const int height = 24;
        var pixels = new Rgba32[width * height];
        for (var y = 3; y < height - 3; y++)
        {
            for (var x = 5; x < width - 5; x++)
            {
                var edge = x is 5 or 6 or 57 or 58 || y is 3 or 4 or 19 or 20;
                pixels[y * width + x] = edge
                    ? new Rgba32(28, 33, 40, 240)
                    : y is >= 8 and < 16
                        ? new Rgba32(218, 226, 230, 225)
                        : new Rgba32(82, 92, 102, 255);
            }
        }

        for (var x = 14; x < 50; x++)
        {
            pixels[10 * width + x] = new Rgba32(255, 240, 186, 255);
            pixels[11 * width + x] = new Rgba32(255, 240, 186, 255);
        }

        return registry.Register(pixels, width, height, "ceiling-fixture");
    }
}
