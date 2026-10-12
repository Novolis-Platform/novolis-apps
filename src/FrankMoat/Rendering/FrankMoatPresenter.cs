using System.Numerics;
using FrankMoat.Actors;
using FrankMoat.Game;
using FrankMoat.Levels;
using FrankMoat.Particles;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.Planar;

namespace FrankMoat.Rendering;

internal sealed class FrankMoatPresenter
{
    private readonly FrankMoatArt _art;
    private readonly RaisedWallDrawer _walls = new();
    private readonly List<PlanarSpriteInstance> _dynamic = [];
    private FloorId? _builtFloor;

    public FrankMoatPresenter(FrankMoatArt art) => _art = art;

    public void BuildStatic(PlanarScene scene, FrankMoatWorld world)
    {
        scene.Sprites.Clear();
        scene.StaticPolygons.Clear();
        _dynamic.Clear();
        _builtFloor = world.Floor.Id;

        var bounds = FloorBounds(world.Floor);
        const float tileSize = 4f;
        for (var z = bounds.MinZ; z < bounds.MaxZ; z += tileSize)
        {
            for (var x = bounds.MinX; x < bounds.MaxX; x += tileSize)
            {
                var width = MathF.Min(tileSize, bounds.MaxX - x);
                var height = MathF.Min(tileSize, bounds.MaxZ - z);
                scene.Sprites.Add(
                    new PlanarSpriteInstance
                    {
                        Texture = _art.Floor,
                        Tint = Rgba32.White,
                        SortKey = -100000,
                        Transform =
                        {
                            Position = new Vector3(x + width * 0.5f, 0f, z + height * 0.5f),
                            Scale = new Vector3(width, 1f, height),
                        },
                    });
            }
        }
        BuildRoomGuides(scene, world.Floor);
        BuildProps(scene, world.Floor);
        _walls.Build(scene, world.Walls, _art);
    }

    public void Sync(
        PlanarScene scene,
        FrankMoatWorld world,
        CampaignState campaign,
        int pixelWidth,
        int pixelHeight)
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
        SyncInteractionMarkers(scene, world, campaign);
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
        _builtFloor = null;
    }

    private void SyncLights(PlanarScene scene, FrankMoatWorld world)
    {
        scene.Lighting.Clear();
        scene.Lighting.AmbientColor = Rgba32.White;
        scene.Lighting.AmbientIntensity = world.Lighting.AmbientIntensity;
        foreach (var fixture in world.Floor.Fixtures)
        {
            var tint = world.Lighting.FixtureTint(fixture);
            if (tint.A == 0)
            {
                continue;
            }

            if (fixture.RotatingAlarm)
            {
                var angle = world.Lighting.AlarmAngle;
                var direction = Vector3PlanarExtensions.FromHeadingXz(angle);
                var beaconTint = new Rgba32(
                    255,
                    48,
                    36,
                    (byte)Math.Clamp((int)(150f * fixture.Intensity), 80, 220));
                Add(
                    scene,
                    _art.SoftGlow,
                    PlanarElevation.Elevate(fixture.Position, 0.08f),
                    new Vector3(fixture.Radius * 0.62f, 1f, fixture.Radius * 0.62f),
                    new Rgba32(255, 34, 28, 105),
                    5,
                    layer: PlanarDrawLayer.Additive,
                    receivesLighting: false);
                Add(
                    scene,
                    _art.AlarmSweep,
                    PlanarElevation.Elevate(
                        fixture.Position + direction * (fixture.Radius * 0.2f),
                        0.12f),
                    new Vector3(fixture.Radius * 0.32f, 1f, fixture.Radius * 0.95f),
                    new Rgba32(255, 42, 32, 122),
                    7,
                    -angle + MathF.PI * 0.5f,
                    PlanarDrawLayer.Additive,
                    false);
                Add(
                    scene,
                    _art.AlarmBeacon,
                    PlanarElevation.Elevate(fixture.Position, 0.22f),
                    new Vector3(0.82f, 1f, 0.82f),
                    beaconTint,
                    10,
                    angle,
                    PlanarDrawLayer.Additive,
                    false);
                scene.Lighting.Sources.Add(
                    new PlanarLightSource(
                        fixture.Position,
                        new Rgba32(255, 40, 32),
                        fixture.Radius,
                        fixture.Intensity,
                        PlanarLightKind.Spot,
                        direction,
                        0.95f));
                continue;
            }

            Add(
                scene,
                _art.SoftGlow,
                PlanarElevation.Elevate(fixture.Position, 0.03f),
                new Vector3(fixture.Radius * 0.46f, 1f, fixture.Radius * 0.46f),
                tint,
                5,
                layer: PlanarDrawLayer.BehindWorld,
                receivesLighting: false);
            scene.Lighting.Sources.Add(
                new PlanarLightSource(
                    fixture.Position,
                    new Rgba32(fixture.Color.R, fixture.Color.G, fixture.Color.B),
                    fixture.Radius,
                    fixture.Intensity * 1.1f,
                    PlanarLightKind.Point,
                    Vector3.Zero,
                    MathF.PI));
            Add(
                scene,
                _art.CeilingFixture,
                PlanarElevation.Elevate(fixture.Position, 0.06f),
                new Vector3(0.9f, 1f, 0.34f),
                new Rgba32(232, 236, 238, 235),
                8,
                layer: PlanarDrawLayer.BehindWorld,
                receivesLighting: false);

            if (fixture.EmergencyOnly
                && world.Lighting.Mode is LightingMode.Emergency or LightingMode.Combat)
            {
                Add(
                    scene,
                    _art.SoftGlow,
                    PlanarElevation.Elevate(fixture.Position, 0.07f),
                    new Vector3(fixture.Radius * 0.34f, 1f, fixture.Radius * 0.34f),
                    new Rgba32(226, 48, 38, 70),
                    6,
                    layer: PlanarDrawLayer.BehindWorld,
                    receivesLighting: false);
            }
        }
    }

    private static void BuildRoomGuides(PlanarScene scene, FloorDefinition floor)
    {
        foreach (var room in floor.Rooms)
        {
            var polygon = new PlanarStaticPolygon(
                PlanarScenePrimitives.Rectangle(
                    room.MinX,
                    room.MinZ,
                    room.MaxX,
                    room.MaxZ),
                new Rgba32(78, 90, 104, 24))
            {
                DrawFilled = true,
                DrawOutline = true,
                OutlineColor = new Rgba32(122, 138, 154, 70),
                SortKey = -99990,
            };
            scene.StaticPolygons.Add(polygon);
        }
    }

    private void BuildProps(PlanarScene scene, FloorDefinition floor)
    {
        foreach (var prop in floor.Props)
        {
            scene.Sprites.Add(
                new PlanarSpriteInstance
                {
                    Texture = _art.Shadow,
                    Tint = Rgba32.White,
                    SortKey = DepthSort(prop.Position.Z) - 2,
                    Transform =
                    {
                        Position = PlanarElevation.Elevate(prop.Position, 0.025f),
                        Scale = new Vector3(prop.Size.X * 1.16f, 1f, prop.Size.Y * 1.2f),
                        RotationY = prop.Rotation,
                    },
                });

            var footprint = prop.GetFootprint();
            var firstPart = scene.StaticPolygons.Count;
            var height = prop.Height > 0f
                ? prop.Height
                : FurnitureHeight(prop.Kind);
            PlanarWallComposer.Add(
                scene,
                footprint.Extrude(PlanarElevation.Offset(height)),
                _art.PropSide,
                textureMeters: MathF.Max(prop.Size.X, prop.Size.Y),
                frontFacesOnly: true,
                capTexture: _art.Prop(prop.Kind),
                faceFill: _art.PropSideTint(prop.Kind),
                capFill: _art.PropCapTint(prop.Kind));

            for (var i = firstPart; i < scene.StaticPolygons.Count; i++)
            {
                var part = scene.StaticPolygons[i];
                part.Layer = PlanarDrawLayer.World;
                part.SortKey = DepthSort(MinimumZ(part.Shape))
                    + (part.WallPart == PlanarWallPartKind.Cap ? -1 : 1);
                part.DrawOutline = true;
                part.OutlineColor = new Rgba32(12, 16, 22, 230);
            }
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
                DepthSort(gate.Position.Z) + 2);
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
                20,
                layer: decal.Kind == (int)DecalKind.BloodWall
                    ? PlanarDrawLayer.Foreground
                    : PlanarDrawLayer.BehindWorld,
                receivesLighting: false);
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
                DepthSort(corpse.Position.Z),
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
                DepthSort(pickup.Position.Z) + 2);
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
                DepthSort(barrel.Position.Z) + 2);
        }
    }

    private void SyncEnemies(PlanarScene scene, FrankMoatWorld world)
    {
        foreach (var enemy in world.Undead)
        {
            Add(
                scene,
                _art.Shadow,
                PlanarElevation.Elevate(enemy.Position, 0.035f),
                new Vector3(enemy.Radius * 2.3f, 1f, enemy.Radius * 1.9f),
                Rgba32.White,
                44,
                layer: PlanarDrawLayer.BehindWorld,
                receivesLighting: false);
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
                DepthSort(enemy.Position.Z) + 4,
                -enemy.Facing.Atan2Xz(),
                PlanarDrawLayer.World);
            if (enemy.IsRanged)
            {
                Add(
                    scene,
                    _art.Weapon(WeaponId.Automatic),
                    PlanarElevation.Elevate(enemy.Position + enemy.Facing * 0.38f, 0.2f),
                    new Vector3(0.2f, 1f, 0.8f),
                    new Rgba32(74, 84, 94, 255),
                    DepthSort(enemy.Position.Z) + 6,
                    -enemy.Facing.Atan2Xz(),
                    PlanarDrawLayer.World);
            }
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
                80,
                layer: PlanarDrawLayer.Additive,
                receivesLighting: false);
        }
    }

    private void SyncInteractionMarkers(
        PlanarScene scene,
        FrankMoatWorld world,
        CampaignState campaign)
    {
        foreach (var hint in world.GetInteractionHints(campaign))
        {
            var distance = hint.DistanceFrom(world.Frank.Position);
            if (distance > 10f)
            {
                continue;
            }

            var tint = hint.IsAvailable
                ? new Rgba32(246, 202, 76, (byte)Math.Clamp(220 - (int)(distance * 12f), 80, 220))
                : new Rgba32(226, 74, 62, (byte)Math.Clamp(205 - (int)(distance * 10f), 80, 205));
            Add(
                scene,
                _art.SoftGlow,
                PlanarElevation.Elevate(hint.Position, 0.08f),
                new Vector3(1.35f, 1f, 1.35f),
                new Rgba32(tint.R, tint.G, tint.B, (byte)(tint.A / 3)),
                58 + (int)(hint.Position.Z * 2f),
                layer: PlanarDrawLayer.Additive,
                receivesLighting: false);
            Add(
                scene,
                _art.Interact,
                PlanarElevation.Elevate(hint.Position, 0.28f),
                new Vector3(0.68f, 1f, 0.68f),
                tint,
                64 + (int)(hint.Position.Z * 2f),
                layer: PlanarDrawLayer.Foreground,
                receivesLighting: false);
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
            _art.Shadow,
            PlanarElevation.Elevate(world.Frank.Position, 0.035f),
            new Vector3(0.92f, 1f, 0.92f),
            Rgba32.White,
            65,
            -world.Frank.Facing.Atan2Xz());
        Add(
            scene,
            _art.Frank,
            PlanarElevation.Elevate(world.Frank.Position, 0.16f),
            new Vector3(1.32f, 1f, 1.32f),
            tint,
            DepthSort(world.Frank.Position.Z) + 4,
            -world.Frank.Facing.Atan2Xz(),
            PlanarDrawLayer.World);
        Add(
            scene,
            _art.Weapon(world.Weapon.Spec.Id),
            PlanarElevation.Elevate(
                world.Frank.Position
                + world.Frank.Facing * (0.34f + WeaponVisualCatalog.For(world.Weapon.Spec.Id).Length * 0.22f),
                0.22f),
            new Vector3(
                WeaponVisualCatalog.For(world.Weapon.Spec.Id).Width,
                1f,
                WeaponVisualCatalog.For(world.Weapon.Spec.Id).Length),
            Rgba32.White,
            DepthSort(world.Frank.Position.Z) + 6,
            -world.Frank.Facing.Atan2Xz(),
            PlanarDrawLayer.World);
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
            var color = Rgba32.Lerp(particle.ColorStart, particle.ColorEnd, t);
            var layer = particle.Kind switch
            {
                (int)ParticleKind.Blood => PlanarDrawLayer.Foreground,
                (int)ParticleKind.Smoke => PlanarDrawLayer.BehindWorld,
                (int)ParticleKind.SoftGlow or (int)ParticleKind.Spark =>
                    PlanarDrawLayer.Additive,
                _ => PlanarDrawLayer.World,
            };
            Add(
                scene,
                texture,
                PlanarElevation.Elevate(particle.Position, particle.Elevation),
                new Vector3(size, 1f, size),
                color,
                90,
                particle.Rotation,
                layer,
                receivesLighting: false);
        }
    }

    private void SyncFlashlight(PlanarScene scene, FrankMoatWorld world)
    {
        var strength = world.Lighting.FlashlightStrength;
        var tint = new Rgba32(
            255,
            236,
            200,
            (byte)Math.Clamp((int)(92f * strength), 58, 132));
        Add(
            scene,
            _art.FlashCone,
            PlanarElevation.Elevate(world.Frank.Position + world.Frank.Facing * 1.9f, 0.11f),
            new Vector3(2.6f * strength, 1f, 5.4f * strength),
            tint,
            12,
            -world.Frank.Facing.Atan2Xz() + MathF.PI * 0.5f,
            PlanarDrawLayer.Additive,
            false);
        scene.Lighting.Sources.Add(
            new PlanarLightSource(
                world.Frank.Position + world.Frank.Facing * 0.35f,
                new Rgba32(255, 236, 200),
                7.2f * strength,
                1.15f * strength,
                PlanarLightKind.Spot,
                world.Frank.Facing,
                0.72f));
        if (world.Juice.MuzzleTimer > 0f)
        {
            scene.Lighting.Sources.Add(
                new PlanarLightSource(
                    world.Frank.Position + world.Frank.Facing * 0.55f,
                    new Rgba32(255, 164, 72),
                    3.5f,
                    2.4f,
                    PlanarLightKind.Point,
                    Vector3.Zero,
                    MathF.PI));
        }

        if (world.Juice.ExplosionTimer > 0f)
        {
            var explosionStrength = Math.Clamp(world.Juice.ExplosionTimer / 0.24f, 0f, 1f);
            Add(
                scene,
                _art.SoftGlow,
                PlanarElevation.Elevate(world.LastExplosionPosition, 0.34f),
                new Vector3(
                    2.2f + explosionStrength * 1.3f,
                    1f,
                    2.2f + explosionStrength * 1.3f),
                new Rgba32(
                    255,
                    104,
                    28,
                    (byte)(100 + explosionStrength * 120f)),
                96,
                layer: PlanarDrawLayer.Additive,
                receivesLighting: false);
            scene.Lighting.Sources.Add(
                new PlanarLightSource(
                    world.LastExplosionPosition,
                    new Rgba32(255, 106, 36),
                    7.5f,
                    2.2f * explosionStrength,
                    PlanarLightKind.Point,
                    Vector3.Zero,
                    MathF.PI));
        }
    }

    private static float FurnitureHeight(PropKind kind) => kind switch
    {
        PropKind.Counter => 0.68f,
        PropKind.Console => 0.82f,
        PropKind.Table => 0.46f,
        PropKind.Shelf => 1.15f,
        PropKind.Elevator => 1.25f,
        PropKind.Cabinet => 1f,
        PropKind.PortalFrame => 1.35f,
        PropKind.GlassPanel => 0.82f,
        PropKind.Vent => 0.34f,
        PropKind.HazardStripe => 0.24f,
        _ => 0.62f,
    };

    private static int DepthSort(float worldZ) =>
        -(int)MathF.Round(worldZ * 100f);

    private static float MinimumZ(Novolis.Math.Topology.Polygon shape)
    {
        var minimum = float.MaxValue;
        foreach (var vertex in shape)
        {
            minimum = MathF.Min(minimum, vertex.Z);
        }

        return minimum == float.MaxValue ? 0f : minimum;
    }

    private PlanarSpriteInstance Add(
        PlanarScene scene,
        PlanarTextureId texture,
        Vector3 position,
        Vector3 scale,
        Rgba32 tint,
        int sortKey,
        float rotation = 0f,
        PlanarDrawLayer layer = PlanarDrawLayer.World,
        bool receivesLighting = true)
    {
        var sprite = new PlanarSpriteInstance
        {
            Texture = texture,
            Tint = tint,
            Layer = layer,
            SortKey = sortKey,
            ReceivesLighting = receivesLighting,
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
