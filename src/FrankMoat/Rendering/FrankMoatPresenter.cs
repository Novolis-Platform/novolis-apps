using System.Numerics;
using FrankMoat.Actors;
using FrankMoat.Game;
using FrankMoat.Levels;
using FrankMoat.Particles;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Rendering;

internal sealed class FrankMoatPresenter
{
    private readonly FrankMoatArt _art;
    private readonly RaisedWallDrawer _walls = new();
    private readonly List<PlanarSpriteInstance> _dynamic = [];
    private PlanarSpriteInstance? _floor;
    private FloorId? _builtFloor;

    public FrankMoatPresenter(FrankMoatArt art) => _art = art;

    public void BuildStatic(PlanarScene scene, FrankMoatWorld world)
    {
        scene.Sprites.Clear();
        scene.StaticPolygons.Clear();
        _dynamic.Clear();
        _floor = null;
        _builtFloor = world.Floor.Id;

        var bounds = FloorBounds(world.Floor);
        _floor = new PlanarSpriteInstance
        {
            Texture = _art.Floor,
            Tint = Rgba32.White,
            SortKey = 0,
            Transform =
            {
                Position = new Vector3(
                    (bounds.MinX + bounds.MaxX) * 0.5f,
                    0f,
                    (bounds.MinZ + bounds.MaxZ) * 0.5f),
                Scale = new Vector3(bounds.MaxX - bounds.MinX, 1f, bounds.MaxZ - bounds.MinZ),
            },
        };
        scene.Sprites.Add(_floor);
        _walls.Build(scene, world.Walls, _art);
    }

    public void Sync(PlanarScene scene, FrankMoatWorld world, int pixelWidth, int pixelHeight)
    {
        if (_builtFloor != world.Floor.Id)
        {
            BuildStatic(scene, world);
        }

        foreach (var sprite in _dynamic)
        {
            scene.Sprites.Remove(sprite);
        }
        _dynamic.Clear();

        SyncLights(scene, world);
        SyncGates(scene, world);
        SyncDecals(scene, world);
        SyncCorpses(scene, world);
        SyncPickups(scene, world);
        SyncBarrels(scene, world);
        SyncEnemies(scene, world);
        SyncProjectiles(scene, world);
        SyncPlayer(scene, world);
        SyncParticles(scene, world);
        SyncFlashlight(scene, world);
        _walls.TickOcclusion(world.Frank.Position, world.Juice.MuzzleTimer);
    }

    public void Clear(PlanarScene scene)
    {
        foreach (var sprite in _dynamic)
        {
            scene.Sprites.Remove(sprite);
        }

        _dynamic.Clear();
        scene.Sprites.Clear();
        scene.StaticPolygons.Clear();
        _floor = null;
        _builtFloor = null;
    }

    private void SyncLights(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var fixture in world.Floor.Fixtures)
        {
            var tint = world.Lighting.FixtureTint(fixture);
            if (tint.A == 0)
            {
                continue;
            }

            Add(
                scene,
                _art.SoftGlow,
                PlanarElevation.Elevate(fixture.Position, 0.03f),
                new Vector3(fixture.Radius, 1f, fixture.Radius),
                tint,
                5);
        }

        if (world.Lighting.Mode == LightingMode.Emergency
            || world.Lighting.Mode == LightingMode.Combat)
        {
            Add(
                scene,
                _art.SoftGlow,
                PlanarElevation.Elevate(new Vector3(45f, 0f, 30f), 0.06f),
                new Vector3(5f, 1f, 5f),
                new Rgba32(220, 30, 20, 70),
                6);
        }
    }

    private void SyncGates(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var gate in world.Gates.Where(gate => !gate.IsOpen))
        {
            Add(
                scene,
                _art.Emergency,
                PlanarElevation.Elevate(gate.Position, 0.24f),
                new Vector3(gate.Width, 1f, 0.72f),
                new Rgba32(210, 54, 42, 210),
                42);
            Add(
                scene,
                _art.Interact,
                PlanarElevation.Elevate(gate.Position + new Vector3(0f, 0f, 0.7f), 0.3f),
                new Vector3(0.32f, 1f, 0.32f),
                new Rgba32(240, 210, 100, 220),
                75);
        }
    }

    private void SyncDecals(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var decal in world.Decals.Span)
        {
            var texture = decal.Kind switch
            {
                (int)DecalKind.BloodFloor or (int)DecalKind.BloodWall => _art.Blood,
                (int)DecalKind.Scorch => _art.SoftGlow,
                _ => _art.Spark,
            };
            Add(
                scene,
                texture,
                PlanarElevation.Elevate(decal.Position, decal.Elevation),
                new Vector3(decal.Width, 1f, decal.Height),
                decal.Color,
                20);
        }
    }

    private void SyncCorpses(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var corpse in world.Corpses)
        {
            Add(
                scene,
                _art.Enemy(corpse.Kind),
                PlanarElevation.Elevate(corpse.Position, 0.06f),
                new Vector3(0.9f, 1f, 0.55f),
                new Rgba32(90, 30, 30, 220),
                25,
                corpse.Rotation);
        }
    }

    private void SyncPickups(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var pickup in world.Pickups.Where(pickup => !pickup.Taken))
        {
            Add(
                scene,
                _art.Pickup,
                PlanarElevation.Elevate(pickup.Position, 0.22f),
                new Vector3(0.5f, 1f, 0.5f),
                new Rgba32(250, 204, 92, 240),
                35);
        }
    }

    private void SyncBarrels(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var barrel in world.Barrels.Where(barrel => barrel.Alive))
        {
            Add(
                scene,
                _art.Barrel,
                PlanarElevation.Elevate(barrel.Position, 0.2f),
                new Vector3(0.7f, 1f, 0.7f),
                Rgba32.White,
                45);
        }
    }

    private void SyncEnemies(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var enemy in world.Undead)
        {
            var tint = enemy.HitFlash > 0f
                ? new Rgba32(255, 200, 200)
                : enemy.Alerted
                    ? Rgba32.White
                    : new Rgba32(160, 160, 170, 220);
            Add(
                scene,
                _art.Enemy(enemy.Kind),
                PlanarElevation.Elevate(enemy.Position, 0.14f),
                new Vector3(enemy.Radius * 2.5f, 1f, enemy.Radius * 2.5f),
                tint,
                50 + (int)(enemy.Position.Z * 2f),
                -enemy.Facing.Atan2Xz());
        }
    }

    private void SyncProjectiles(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var projectile in world.Projectiles)
        {
            Add(
                scene,
                projectile.Explosive ? _art.SoftGlow : _art.Spark,
                PlanarElevation.Elevate(projectile.Position, 0.42f),
                new Vector3(
                    projectile.Explosive ? 0.28f : 0.12f,
                    1f,
                    projectile.Explosive ? 0.28f : 0.12f),
                projectile.FromFrank
                    ? new Rgba32(255, 230, 120, 230)
                    : new Rgba32(255, 80, 70, 220),
                80);
        }
    }

    private void SyncPlayer(PlanarScene scene, FrankMoatWorld world)
    {
        var tint = world.Frank.IFrames > 0f
            && (int)(world.Frank.IFrames * 20f) % 2 == 0
            ? new Rgba32(255, 180, 180)
            : Rgba32.White;
        Add(
            scene,
            _art.Frank,
            PlanarElevation.Elevate(world.Frank.Position, 0.16f),
            new Vector3(0.95f, 1f, 0.95f),
            tint,
            70 + (int)(world.Frank.Position.Z * 2f),
            -world.Frank.Facing.Atan2Xz());
    }

    private void SyncParticles(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var particle in world.Particles.Span)
        {
            var texture = particle.Kind switch
            {
                (int)ParticleKind.Blood => _art.Blood,
                (int)ParticleKind.Smoke => _art.Smoke,
                (int)ParticleKind.SoftGlow => _art.SoftGlow,
                (int)ParticleKind.Shell => _art.Pickup,
                _ => _art.Spark,
            };
            var t = particle.MaxLife <= 0f
                ? 0f
                : 1f - Math.Clamp(particle.Life / particle.MaxLife, 0f, 1f);
            var size = particle.SizeStart + (particle.SizeEnd - particle.SizeStart) * t;
            Add(
                scene,
                texture,
                PlanarElevation.Elevate(particle.Position, particle.Elevation),
                new Vector3(size, 1f, size),
                particle.ColorStart,
                90,
                particle.Rotation);
        }
    }

    private void SyncFlashlight(PlanarScene scene, FrankMoatWorld world)
    {
        var strength = world.Lighting.FlashlightStrength;
        var tint = new Rgba32(
            255,
            236,
            200,
            (byte)Math.Clamp((int)(90f * strength), 30, 220));
        Add(
            scene,
            _art.FlashCone,
            PlanarElevation.Elevate(world.Frank.Position + world.Frank.Facing * 3.5f, 0.11f),
            new Vector3(6.2f * strength, 1f, 9f * strength),
            tint,
            12,
            -world.Frank.Facing.Atan2Xz() + MathF.PI * 0.5f);
    }

    private PlanarSpriteInstance Add(
        PlanarScene scene,
        PlanarTextureId texture,
        Vector3 position,
        Vector3 scale,
        Rgba32 tint,
        int sortKey,
        float rotation = 0f)
    {
        var sprite = new PlanarSpriteInstance
        {
            Texture = texture,
            Tint = tint,
            SortKey = sortKey,
            Transform =
            {
                Position = position,
                Scale = scale,
                RotationY = rotation,
            },
        };
        scene.Sprites.Add(sprite);
        _dynamic.Add(sprite);
        return sprite;
    }

    private static (float MinX, float MinZ, float MaxX, float MaxZ) FloorBounds(FloorDefinition floor)
    {
        var minX = floor.Rooms.Min(room => room.MinX) - 1.4f;
        var minZ = floor.Rooms.Min(room => room.MinZ) - 1.4f;
        var maxX = floor.Rooms.Max(room => room.MaxX) + 1.4f;
        var maxZ = floor.Rooms.Max(room => room.MaxZ) + 1.4f;
        return (minX, minZ, maxX, maxZ);
    }
}
