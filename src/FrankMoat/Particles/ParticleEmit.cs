using System.Numerics;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Particles;

internal static class ParticleEmit
{
    public static void Muzzle(
        SpriteParticleField field,
        Vector3 origin,
        Vector3 aim,
        WeaponSpec weapon,
        Random random)
    {
        var direction = aim.NormalizeXz();
        var mouth = origin + direction * 0.55f;
        var count = weapon.PelletCount > 1 ? 22 : 12;
        for (var i = 0; i < count; i++)
        {
            var angle = random.NextSingle() * MathF.PI * 2f;
            var speed = 3f + random.NextSingle() * 6f;
            field.Emit(new SpriteParticle
            {
                Position = mouth,
                Velocity = Vector3PlanarExtensions.FromHeadingXz(angle) * speed + direction * 4f,
                Elevation = 0.55f,
                ElevationVelocity = 1.2f + random.NextSingle(),
                MaxLife = 0.1f + random.NextSingle() * 0.08f,
                ColorStart = new Rgba32(255, 244, 190, 255),
                ColorEnd = new Rgba32(255, 90, 20, 0),
                SizeStart = 0.2f,
                SizeEnd = 0.04f,
                Kind = (int)ParticleKind.Spark,
                Drag = 9f,
            });
        }

        for (var i = 0; i < 5; i++)
        {
            field.Emit(new SpriteParticle
            {
                Position = mouth,
                Velocity = direction * (0.8f + i * 0.15f),
                Elevation = 0.5f,
                ElevationVelocity = 0.4f,
                MaxLife = 0.35f + random.NextSingle() * 0.2f,
                ColorStart = new Rgba32(70, 72, 78, 140),
                ColorEnd = new Rgba32(40, 42, 46, 0),
                SizeStart = 0.28f,
                SizeEnd = 0.7f,
                Kind = (int)ParticleKind.Smoke,
                Drag = 1.6f,
            });
        }
    }

    public static void Tracer(
        SpriteParticleField field,
        Vector3 position,
        bool fromFrank)
    {
        field.Emit(new SpriteParticle
        {
            Position = position,
            MaxLife = 0.09f,
            ColorStart = fromFrank
                ? new Rgba32(255, 230, 120, 220)
                : new Rgba32(255, 80, 70, 200),
            ColorEnd = new Rgba32(255, 80, 30, 0),
            SizeStart = 0.1f,
            SizeEnd = 0.03f,
            Kind = (int)ParticleKind.SoftGlow,
            Elevation = 0.45f,
        });
    }

    public static void Blood(
        SpriteParticleField field,
        Vector3 origin,
        Vector3 incoming,
        float scale,
        Random random)
    {
        var direction = incoming.NormalizeXz(new Vector3(0f, 0f, 1f));
        var count = (int)(22 * scale);
        for (var i = 0; i < count; i++)
        {
            var spread = (random.NextSingle() - 0.5f) * 1.4f;
            var speed = 2f + random.NextSingle() * 7f * scale;
            var vx = direction.X * speed + -direction.Z * spread * speed;
            var vz = direction.Z * speed + direction.X * spread * speed;
            field.Emit(new SpriteParticle
            {
                Position = origin,
                Velocity = new Vector3(vx, 0f, vz),
                Elevation = 0.4f,
                ElevationVelocity = 1.5f + random.NextSingle() * 3f,
                MaxLife = 0.35f + random.NextSingle() * 0.45f,
                ColorStart = new Rgba32(168, 14, 18, 240),
                ColorEnd = new Rgba32(70, 8, 10, 0),
                SizeStart = 0.1f * scale,
                SizeEnd = 0.22f * scale,
                Kind = (int)ParticleKind.Blood,
                Drag = 2.8f,
            });
        }
    }

    public static void Explosion(
        SpriteParticleField field,
        Vector3 origin,
        float scale,
        Random random)
    {
        for (var ring = 0; ring < 3; ring++)
        {
            var count = 18 + ring * 10;
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * MathF.PI * 2f;
                var speed = (4f + ring * 2.2f) * scale;
                field.Emit(new SpriteParticle
                {
                    Position = origin,
                    Velocity = Vector3PlanarExtensions.FromHeadingXz(angle) * speed,
                    Elevation = 0.3f,
                    ElevationVelocity = 2f + ring,
                    MaxLife = 0.4f + ring * 0.12f,
                    ColorStart = ring == 0
                        ? new Rgba32(255, 246, 190, 255)
                        : new Rgba32(255, (byte)(90 + ring * 20), 24, 230),
                    ColorEnd = new Rgba32(40, 16, 8, 0),
                    SizeStart = (0.22f + ring * 0.1f) * scale,
                    SizeEnd = (0.45f + ring * 0.18f) * scale,
                    Kind = ring == 0
                        ? (int)ParticleKind.Spark
                        : (int)ParticleKind.SoftGlow,
                    Drag = 2.2f,
                });
            }
        }

        for (var i = 0; i < (int)(18 * scale); i++)
        {
            field.Emit(new SpriteParticle
            {
                Position = origin,
                Velocity = new Vector3(
                    (random.NextSingle() - 0.5f) * 3f,
                    0f,
                    (random.NextSingle() - 0.5f) * 3f),
                Elevation = 0.4f,
                ElevationVelocity = 1f,
                MaxLife = 0.8f,
                ColorStart = new Rgba32(70, 68, 64, 160),
                ColorEnd = new Rgba32(30, 30, 28, 0),
                SizeStart = 0.4f * scale,
                SizeEnd = 1.2f * scale,
                Kind = (int)ParticleKind.Smoke,
                Drag = 1.1f,
            });
        }
    }

    public static void AmbientDust(SpriteParticleField field, Vector3 around, Random random)
    {
        field.Emit(new SpriteParticle
        {
            Position = around + new Vector3(
                (random.NextSingle() - 0.5f) * 10f,
                0f,
                (random.NextSingle() - 0.5f) * 10f),
            Velocity = new Vector3((random.NextSingle() - 0.5f) * 0.3f, 0f, 0.15f),
            Elevation = random.NextSingle() * 1.4f,
            ElevationVelocity = 0.05f,
            MaxLife = 1.6f,
            ColorStart = new Rgba32(180, 170, 140, 50),
            ColorEnd = new Rgba32(80, 76, 60, 0),
            SizeStart = 0.08f,
            SizeEnd = 0.18f,
            Kind = (int)ParticleKind.Ember,
            Drag = 0.4f,
        });
    }
}
