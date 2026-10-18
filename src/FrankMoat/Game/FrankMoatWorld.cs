using System.Numerics;
using FrankMoat.Actors;
using FrankMoat.Combat;
using FrankMoat.Input;
using FrankMoat.Levels;
using FrankMoat.Narrative;
using FrankMoat.Particles;
using FrankMoat.Rendering;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Game;

internal sealed class FrankMoatWorld
{
    private readonly Dictionary<WeaponId, WeaponSnapshot> _stash = [];
    private Random _random = new(4761);
    private FloorDefinition _floor = null!;
    private float _ambientTimer;
    private float _noiseTimer;
    private Vector3 _noisePosition;
    private float _noiseRadius;
    private Vector3 _lastExplosionPosition;

    public FrankActor Frank { get; } = new();

    public WeaponState Weapon { get; private set; } = new(WeaponCatalog.M1911);

    public List<UndeadActor> Undead { get; } = [];

    public List<Corpse> Corpses { get; } = [];

    public List<RangeProjectile> Projectiles { get; } = [];

    public List<RangePickup> Pickups { get; } = [];

    public List<RangeBarrel> Barrels { get; } = [];

    public List<RaisedWall> Walls { get; } = [];

    public List<WallSegment> Segments { get; } = [];

    public List<GateDefinition> Gates { get; } = [];

    public List<EncounterDefinition> Encounters { get; } = [];

    public List<SecretDefinition> Secrets { get; } = [];

    public SpriteParticleField Particles { get; } = new(1800, 0x4761u);

    public SpriteDecalField Decals { get; } = new(2800);

    public RangeJuice Juice { get; } = new();

    public FaithDialogueQueue Faith { get; } = new();

    public LightingRig Lighting { get; } = new();

    public FloorDefinition Floor => _floor;

    public bool Inspecting { get; set; }

    public bool MapVisible { get; set; }

    public int Kills { get; private set; }

    public bool IsCombat =>
        _floor.Id == FloorId.Level68
        && (Undead.Count > 0 || Encounters.Any(encounter => encounter.HasTriggered
            && !encounter.IsComplete));

    public bool IsCleared =>
        _floor.Id == FloorId.Level68
        && Encounters.Count > 0
        && Encounters.All(encounter => encounter.IsComplete);

    public float NoiseRadius => _noiseRadius;

    public Vector3 LastExplosionPosition => _lastExplosionPosition;

    public void Reset(CampaignState campaign)
    {
        _stash.Clear();
        _random = new Random(campaign.WorldSeed);
        Weapon = new WeaponState(WeaponCatalog.M1911);
        Frank.Position = new Vector3(6f, 0f, 6f);
        Frank.Facing = new Vector3(0f, 0f, 1f);
        Frank.Health = 100;
        Frank.Armor = 25;
        Frank.IFrames = 0f;
        Inspecting = false;
        MapVisible = false;
        Kills = 0;
        Projectiles.Clear();
        Undead.Clear();
        Corpses.Clear();
        Pickups.Clear();
        Barrels.Clear();
        Walls.Clear();
        Segments.Clear();
        Gates.Clear();
        Encounters.Clear();
        Secrets.Clear();
        Particles.Clear();
        Decals.Clear();
        Juice.Clear();
        Faith.Reset();
        _noiseTimer = 0f;
        _noiseRadius = 0f;
        _lastExplosionPosition = Vector3.Zero;
        _ambientTimer = 0f;
    }

    public void LoadFloor(FloorDefinition floor, CampaignState campaign)
    {
        _floor = floor;
        Walls.Clear();
        Walls.AddRange(floor.Walls);
        Segments.Clear();
        foreach (var wall in Walls)
        {
            Segments.AddRange(wall.Segments);
        }

        Gates.Clear();
        foreach (var gate in floor.Gates)
        {
            Gates.Add(new GateDefinition
            {
                Id = gate.Id,
                Position = gate.Position,
                Width = gate.Width,
                RequiredPermission = gate.RequiredPermission,
                IsOpen = campaign.HasFlag($"{gate.Id}:open")
                    || gate.Id == "service-route" && campaign.ServiceRouteUnlocked,
            });
        }

        Encounters.Clear();
        foreach (var encounter in floor.Encounters)
        {
            Encounters.Add(new EncounterDefinition
            {
                Id = encounter.Id,
                TriggerPosition = encounter.TriggerPosition,
                TriggerRadius = encounter.TriggerRadius,
                Spawns = encounter.Spawns,
                HasTriggered = campaign.HasFlag($"{encounter.Id}:started"),
                IsComplete = campaign.HasFlag($"{encounter.Id}:complete"),
            });
        }

        Secrets.Clear();
        foreach (var secret in floor.Secrets)
        {
            Secrets.Add(new SecretDefinition
            {
                Id = secret.Id,
                Position = secret.Position,
                RewardText = secret.RewardText,
                Discovered = campaign.HasFlag($"{secret.Id}:found"),
            });
        }

        Projectiles.Clear();
        Undead.Clear();
        Pickups.Clear();
        Barrels.Clear();
        Corpses.Clear();
        Decals.Clear();
        Particles.Clear();
        Frank.Position = floor.Id == campaign.CurrentFloor
            ? campaign.PlayerPosition
            : floor.StartPosition;
        Frank.Health = campaign.Health;
        Frank.Armor = campaign.Armor;
        SelectWeapon(campaign.CurrentWeapon, campaign);
        Weapon.Reserve = campaign.CurrentReserve;
        Weapon.Magazine = campaign.CurrentMagazine;
        Weapon.Chambered = campaign.CurrentChambered;
        SeedFloor(floor.Id, campaign);
        Lighting.SetMode(floor.Id, campaign.DirectiveActive, false, false);
    }

    public void ApplyCollision(PlanarScene scene)
    {
        scene.Collision.Clear();
        foreach (var wall in Walls)
        {
            scene.Collision.AddStatic(new PlanarCollider(wall.Footprint));
        }

        foreach (var gate in Gates.Where(gate => !gate.IsOpen))
        {
            var footprint = PlanarScenePrimitives.Rectangle(
                gate.Position.X - gate.Width * 0.5f,
                gate.Position.Z - 0.42f,
                gate.Position.X + gate.Width * 0.5f,
                gate.Position.Z + 0.42f);
            scene.Collision.AddStatic(new PlanarCollider(footprint));
        }

        foreach (var prop in _floor.Props.Where(prop => prop.BlocksMovement))
        {
            scene.Collision.AddStatic(new PlanarCollider(prop.GetFootprint()));
        }
    }

    public void Tick(
        float dt,
        PlanarCollisionWorld collision,
        FrankInputFrame input,
        CampaignState campaign)
    {
        if (Inspecting)
        {
            Faith.Tick(dt, IsCombat);
            Lighting.Tick(dt, false);
            return;
        }

        if (input.AimWorld.LengthSquaredXz() > 0.0001f)
        {
            Frank.Facing = input.AimWorld.NormalizeXz();
        }

        var movement = new Vector3(input.MoveX, 0f, input.MoveZ);
        if (movement.LengthSquaredXz() > 1f)
        {
            movement = movement.NormalizeXz();
        }

        var speed = input.RunHeld ? FrankActor.RunSpeed : FrankActor.WalkSpeed;
        Frank.Position = collision.MoveCircle(
            Frank.Position,
            movement * speed * dt,
            FrankActor.Radius);
        Frank.IFrames = MathF.Max(0f, Frank.IFrames - dt);
        _noiseTimer = MathF.Max(0f, _noiseTimer - dt);

        HandleWeaponSelect(input, campaign);
        if (input.MeleePressed)
        {
            TryMelee();
        }
        TickWeapon(dt, input);
        TickProjectiles(dt, collision);
        TickUndead(dt, collision);
        TickEncounters(campaign);
        TickPickups(campaign);
        Particles.Tick(dt);
        Juice.Tick(dt);
        Faith.Tick(dt, IsCombat);
        Lighting.Tick(dt, Juice.MuzzleTimer > 0f || Juice.ExplosionTimer > 0f);

        _ambientTimer -= dt;
        if (_ambientTimer <= 0f)
        {
            _ambientTimer = 0.08f;
            ParticleEmit.AmbientDust(Particles, Frank.Position, _random);
        }
    }

    public WorldInteractionResult? Interact(CampaignState campaign)
    {
        if (_floor.Id == FloorId.Level0)
        {
            if (Near(Frank.Position, new Vector3(7f, 0f, 6f), 2.2f))
            {
                if (!campaign.Level0Briefed)
                {
                    campaign.Level0Briefed = true;
                    campaign.DirectiveActive = true;
                    campaign.ExpressReturnLocked = true;
                    campaign.SetFlag("directive-4761");
                    campaign.CompleteObjective("reach-private-elevator");
                    Faith.Say(
                        "Directive 4761 active. Please proceed to the founder elevator. This is not a drill.",
                        6f,
                        priority: 100,
                        instruction: true);
                    Faith.Say(
                        "WASD moves. Mouse aims. E interacts. Tab displays the floor map.",
                        6f,
                        priority: 90,
                        instruction: true);
                    return new WorldInteractionResult("Reception station checked. The building has other plans.");
                }

                return new WorldInteractionResult("FAITH has no further lobby paperwork for Frank.");
            }

            if (campaign.DirectiveActive
                && Near(Frank.Position, new Vector3(21f, 0f, 24f), 3.2f))
            {
                return new WorldInteractionResult(
                    "The private elevator recognizes Frank.",
                    ElevatorDestination: FloorId.Level69);
            }

            return new WorldInteractionResult("The lobby is waiting for a routine interaction.");
        }

        if (_floor.Id == FloorId.Level69)
        {
            if (Near(Frank.Position, new Vector3(10f, 0f, 7f), 2.7f))
            {
                return new WorldInteractionResult(
                    "The founder's recording begins.",
                    Recording: new FounderRecording(
                        "WELCOME TO THE PRESERVE",
                        [
                            "If you are hearing this, the building has finally become interesting.",
                            "The collection is not a museum. It is a continuity plan with better lighting.",
                            "Choose a practical loadout. The prototype remains behind the glass until you earn it.",
                        ]));
            }

            if (campaign.Level69LoadoutChosen
                && Near(Frank.Position, new Vector3(36.5f, 0f, 29.5f), 3.3f))
            {
                return new WorldInteractionResult(
                    "Service elevator route to Level 68 is available.",
                    ElevatorDestination: FloorId.Level68);
            }

            if (!campaign.Level69LoadoutChosen
                && Near(Frank.Position, new Vector3(7f, 0f, 23f), 7f))
            {
                campaign.Level69LoadoutChosen = true;
                campaign.ServiceRouteUnlocked = true;
                campaign.SetFlag("service-route");
                campaign.CompleteObjective("reach-level-68");
                foreach (var weapon in WeaponCatalog.All)
                {
                    campaign.Grant(weapon.Id);
                }

                SelectWeapon(WeaponId.Pump12, campaign);
                Faith.Say(
                    "Initial loadout accepted. The pump shotgun is now available because this is a serious facility.",
                    6f,
                    priority: 100,
                    instruction: true);
                return new WorldInteractionResult(
                    "Loadout selected. The service route is unlocked.");
            }

            foreach (var secret in Secrets.Where(secret => !secret.Discovered))
            {
                if (Near(Frank.Position, secret.Position, 1.6f))
                {
                    secret.Discovered = true;
                    campaign.SetFlag($"{secret.Id}:found");
                    Faith.Say(secret.RewardText, 5f);
                    return new WorldInteractionResult(secret.RewardText);
                }
            }

            return new WorldInteractionResult("A preserved exhibit waits for a more deliberate inspection.");
        }

        if (_floor.Id == FloorId.Level68)
        {
            foreach (var gate in Gates.Where(gate => !gate.IsOpen))
            {
                if (Near(Frank.Position, gate.Position, 2.7f))
                {
                    if (!campaign.HasFlag("containment-clearance"))
                    {
                        return new WorldInteractionResult(
                            "Containment gate locked. Restore the security office first.");
                    }

                    OpenGate(gate, campaign);
                    return new WorldInteractionResult(
                        "Containment gate open. FAITH recommends not celebrating yet.");
                }
            }

            foreach (var secret in Secrets.Where(secret => !secret.Discovered))
            {
                if (Near(Frank.Position, secret.Position, 1.6f))
                {
                    secret.Discovered = true;
                    campaign.SetFlag($"{secret.Id}:found");
                    campaign.Grant(WeaponId.Explosive);
                    Faith.Say(secret.RewardText, 5f);
                    return new WorldInteractionResult(secret.RewardText);
                }
            }

            if (IsCleared && Near(Frank.Position, new Vector3(45f, 0f, 30f), 3.5f))
            {
                return new WorldInteractionResult(
                    "The upward service route is stable enough to use.",
                    ElevatorDestination: FloorId.Level69);
            }

            return new WorldInteractionResult("The facility offers no useful response.");
        }

        return null;
    }

    public IReadOnlyList<InteractionHint> GetInteractionHints(CampaignState campaign) =>
        _floor.Id switch
        {
            FloorId.Level0 =>
            [
                new InteractionHint(
                    "reception",
                    new Vector3(7f, 0f, 6f),
                    "CHECK RECEPTION STATION",
                    2.2f,
                    true,
                    string.Empty),
                new InteractionHint(
                    "founder-elevator",
                    new Vector3(21f, 0f, 24f),
                    "USE PRIVATE ELEVATOR",
                    3.2f,
                    campaign.DirectiveActive,
                    "DIRECTIVE 4761 REQUIRED"),
            ],
            FloorId.Level69 =>
            [
                new InteractionHint(
                    "founder-recording",
                    new Vector3(10f, 0f, 7f),
                    "PLAY FOUNDER RECORDING",
                    2.7f,
                    true,
                    string.Empty),
                ..LoadoutHints(campaign),
                new InteractionHint(
                    "service-elevator",
                    new Vector3(36.5f, 0f, 29.5f),
                    "USE SERVICE ELEVATOR",
                    3.3f,
                    campaign.Level69LoadoutChosen,
                    "SELECT A LOADOUT FIRST"),
                ..SecretHints(campaign, "founder-bar", "INSPECT PRESERVE SECRET", new Vector3(34f, 0f, 7f)),
            ],
            FloorId.Level68 =>
            [
                ..GateHints(campaign),
                ..SecretHints(campaign, "maintenance-cache", "SEARCH MAINTENANCE CACHE", new Vector3(8f, 0f, 28f)),
                new InteractionHint(
                    "upward-route",
                    new Vector3(45f, 0f, 30f),
                    "USE UPWARD SERVICE ROUTE",
                    3.5f,
                    IsCleared,
                    "STABILIZE LEVEL 68 FIRST"),
            ],
            _ => [],
        };

    public InteractionHint? NearestInteraction(
        CampaignState campaign,
        float maxDistance = 8f) =>
        GetInteractionHints(campaign)
            .Where(hint => hint.DistanceFrom(Frank.Position) <= maxDistance)
            .OrderBy(hint => hint.DistanceFrom(Frank.Position))
            .FirstOrDefault();

    public string ObjectiveText(CampaignState campaign) =>
        campaign.CurrentObjectiveId switch
        {
            "reach-private-elevator" =>
                "Reach the founder elevator",
            "reach-level-68" =>
                "Take the service elevator to Level 68",
            "containment-clearance" =>
                "Restore the containment gate",
            "reach-upward-route" =>
                "Reach the upward service route",
            _ => _floor.PrimaryObjective.Title,
        };

    private IReadOnlyList<InteractionHint> LoadoutHints(CampaignState campaign) =>
        campaign.Level69LoadoutChosen
            ? []
            :
            [
                new InteractionHint(
                    "loadout",
                    new Vector3(7f, 0f, 23f),
                    "CHOOSE LOADOUT",
                    7f,
                    true,
                    string.Empty),
            ];

    private IReadOnlyList<InteractionHint> GateHints(CampaignState campaign) =>
        Gates
            .Where(gate => !gate.IsOpen)
            .Select(gate => new InteractionHint(
                gate.Id,
                gate.Position,
                "OPEN CONTAINMENT GATE",
                2.7f,
                campaign.HasFlag("containment-clearance"),
                campaign.HasFlag("containment-clearance")
                    ? string.Empty
                    : "RESTORE SECURITY OFFICE FIRST"))
            .ToArray();

    private IReadOnlyList<InteractionHint> SecretHints(
        CampaignState campaign,
        string id,
        string label,
        Vector3 position) =>
        campaign.HasFlag($"{id}:found")
            ? []
            :
            [
                new InteractionHint(id, position, label, 1.6f, true, string.Empty),
            ];

    public string MapText() =>
        string.Join(
            "  ·  ",
            _floor.Rooms.Select(room =>
                room.Label + (IsInside(Frank.Position, room) ? " •" : string.Empty)));

    public void CaptureTo(CampaignState campaign)
    {
        campaign.PlayerPosition = Frank.Position;
        campaign.Health = Frank.Health;
        campaign.Armor = Frank.Armor;
        campaign.CurrentWeapon = Weapon.Spec.Id;
        campaign.CurrentReserve = Weapon.Reserve;
        campaign.CurrentMagazine = Weapon.Magazine;
        campaign.CurrentChambered = Weapon.Chambered;
    }

    private void SeedFloor(FloorId floor, CampaignState campaign)
    {
        switch (floor)
        {
            case FloorId.Level69:
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Pump12,
                    Position = new Vector3(5f, 0f, 22f),
                });
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Lever1894,
                    Position = new Vector3(9f, 0f, 22f),
                });
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Automatic,
                    Position = new Vector3(13f, 0f, 22f),
                });
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Explosive,
                    Position = new Vector3(17f, 0f, 22f),
                });
                break;
            case FloorId.Level68:
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Ammo12,
                    Position = new Vector3(8f, 0f, 8f),
                });
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Health,
                    Position = new Vector3(8f, 0f, 12f),
                });
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Armor,
                    Position = new Vector3(18f, 0f, 7f),
                });
                Pickups.Add(new RangePickup
                {
                    Kind = PickupKind.Ammo40,
                    Position = new Vector3(34f, 0f, 29f),
                });
                Barrels.Add(new RangeBarrel { Position = new Vector3(23f, 0f, 8f) });
                Barrels.Add(new RangeBarrel { Position = new Vector3(40f, 0f, 25f) });
                break;
        }

        if (campaign.Level69LoadoutChosen)
        {
            foreach (var pickup in Pickups.Where(pickup =>
                         pickup.Kind is PickupKind.Pump12
                             or PickupKind.Lever1894
                             or PickupKind.Automatic
                             or PickupKind.Explosive))
            {
                pickup.Taken = true;
            }
        }
    }

    private void HandleWeaponSelect(FrankInputFrame input, CampaignState campaign)
    {
        if (input.WeaponSlot is >= 1 and <= 5)
        {
            var weapon = WeaponCatalog.All[input.WeaponSlot - 1].Id;
            SelectWeapon(weapon, campaign);
        }
        else if (input.PreviousWeaponPressed)
        {
            var owned = WeaponCatalog.All
                .Where(spec => campaign.Owns(spec.Id))
                .Select(spec => spec.Id)
                .ToList();
            if (owned.Count > 0)
            {
                var index = owned.IndexOf(Weapon.Spec.Id);
                SelectWeapon(owned[(index - 1 + owned.Count) % owned.Count], campaign);
            }
        }
    }

    private void SelectWeapon(WeaponId id, CampaignState campaign)
    {
        if (!campaign.Owns(id) && id != WeaponId.M1911)
        {
            return;
        }

        if (Weapon.Spec.Id == id)
        {
            return;
        }

        _stash[Weapon.Spec.Id] = new WeaponSnapshot(
            Weapon.Reserve,
            Weapon.Magazine,
            Weapon.Chambered);
        Weapon.Equip(WeaponCatalog.FromId(id));
        if (_stash.TryGetValue(id, out var saved))
        {
            Weapon.Reserve = saved.Reserve;
            Weapon.Magazine = saved.Magazine;
            Weapon.Chambered = saved.Chambered;
        }
        else
        {
            Weapon.Reserve = Weapon.Spec.ReserveStart;
            Weapon.Magazine = Weapon.Spec.MagazineSize;
            Weapon.Chambered = Weapon.Spec.ChamberPlusOne;
        }

        campaign.CurrentWeapon = id;
    }

    private void TickWeapon(float dt, FrankInputFrame input)
    {
        Weapon.Cooldown = MathF.Max(0f, Weapon.Cooldown - dt);
        Weapon.TickReload(dt);
        if (input.ReloadPressed)
        {
            Weapon.BeginReload();
        }

        var wantsShot = Weapon.Spec.Mode == WeaponFireMode.Automatic
            ? input.ShootHeld
            : input.ShootPressed || input.AgentFireHeld;
        if (!wantsShot)
        {
            return;
        }

        if (Weapon.Reloading && Weapon.Spec.TubeFeed && Weapon.RoundsInGun > 0)
        {
            Weapon.InterruptReload();
        }

        if (!Weapon.CanFire)
        {
            if (Weapon.RoundsInGun <= 0)
            {
                Weapon.BeginReload();
            }

            return;
        }

        Fire();
    }

    private void TryMelee()
    {
        var target = Undead
            .OrderBy(enemy => Vector3PlanarExtensions.DistanceXz(Frank.Position, enemy.Position))
            .FirstOrDefault();
        if (target is null
            || Vector3PlanarExtensions.DistanceXz(Frank.Position, target.Position) > 1.45f)
        {
            EmitNoise(5f);
            return;
        }

        HurtUndead(target, 24, Frank.Facing);
        Juice.AddShake(0.08f);
        Faith.Say("Quick melee registered. Frank remains professionally unimpressed.", 2.6f);
    }

    private void Fire()
    {
        var spec = Weapon.Spec;
        Weapon.ConsumeRound();
        Weapon.Cooldown = spec.FireIntervalSeconds;
        Juice.AddShake(spec.RecoilShake);
        Juice.FlashMuzzle();
        ParticleEmit.Muzzle(Particles, Frank.Position, Frank.Facing, spec, _random);
        EmitNoise(spec.Explosive ? 28f : 14f);

        var baseAngle = Frank.Facing.Atan2Xz();
        for (var i = 0; i < spec.PelletCount; i++)
        {
            var spread = spec.SpreadDegrees * (MathF.PI / 180f);
            var angle = baseAngle + (_random.NextSingle() - 0.5f) * spread;
            var direction = Vector3PlanarExtensions.FromHeadingXz(angle);
            Projectiles.Add(new RangeProjectile
            {
                Position = Frank.Position + direction * 0.48f,
                Velocity = direction * spec.ProjectileSpeed,
                Damage = spec.Damage,
                Explosive = spec.Explosive,
                BlastRadius = spec.BlastRadius,
            });
        }
    }

    private void TickProjectiles(float dt, PlanarCollisionWorld collision)
    {
        for (var i = Projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = Projectiles[i];
            projectile.Life -= dt;
            var speed = projectile.Velocity.Length();
            if (projectile.Life <= 0f || speed < 0.01f)
            {
                if (projectile.Explosive)
                {
                    Explode(projectile.Position);
                }

                Projectiles.RemoveAt(i);
                continue;
            }

            var direction = projectile.Velocity / speed;
            var travel = speed * dt;
            if (TryHitProjectile(projectile, direction, travel))
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            projectile.Position += projectile.Velocity * dt;
            if (_random.NextSingle() < 0.72f)
            {
                ParticleEmit.Tracer(Particles, projectile.Position, projectile.FromFrank);
            }
        }
    }

    private bool TryHitProjectile(
        RangeProjectile projectile,
        Vector3 direction,
        float travel)
    {
        var best = travel + 1f;
        UndeadActor? enemy = null;
        RangeBarrel? barrel = null;
        var hitFrank = false;
        PlanarHit? wall = null;

        foreach (var segment in Segments)
        {
            if (PlanarRay.TryHitSegment(
                    projectile.Position,
                    direction,
                    travel,
                    segment.Start,
                    segment.End,
                    out var hit)
                && hit.Distance < best)
            {
                best = hit.Distance;
                wall = hit;
                enemy = null;
                barrel = null;
                hitFrank = false;
            }
        }

        if (projectile.FromFrank)
        {
            foreach (var foe in Undead)
            {
                if (PlanarRay.TryHitCircle(
                        projectile.Position,
                        direction,
                        travel,
                        foe.Position,
                        foe.Radius,
                        out var distance)
                    && distance < best)
                {
                    best = distance;
                    enemy = foe;
                    wall = null;
                    barrel = null;
                    hitFrank = false;
                }
            }

            foreach (var drum in Barrels.Where(barrel => barrel.Alive))
            {
                if (PlanarRay.TryHitCircle(
                        projectile.Position,
                        direction,
                        travel,
                        drum.Position,
                        0.38f,
                        out var distance)
                    && distance < best)
                {
                    best = distance;
                    barrel = drum;
                    wall = null;
                    enemy = null;
                    hitFrank = false;
                }
            }
        }
        else if (PlanarRay.TryHitCircle(
                     projectile.Position,
                     direction,
                     travel,
                     Frank.Position,
                     FrankActor.Radius,
                     out var frankDistance)
            && frankDistance < best)
        {
            best = frankDistance;
            hitFrank = true;
            wall = null;
            enemy = null;
            barrel = null;
        }

        if (enemy is not null)
        {
            if (projectile.Explosive)
            {
                Explode(projectile.Position + direction * best);
            }
            else
            {
                HurtUndead(enemy, projectile.Damage, direction);
            }

            return true;
        }

        if (barrel is not null)
        {
            barrel.Health -= projectile.Damage;
            if (!barrel.Alive)
            {
                Explode(barrel.Position);
            }
            else
            {
                ParticleEmit.Tracer(Particles, barrel.Position, true);
            }

            return true;
        }

        if (hitFrank)
        {
            HurtFrank(projectile.Damage);
            return true;
        }

        if (wall is { } wallHit)
        {
            if (projectile.Explosive)
            {
                Explode(wallHit.Point);
            }
            else
            {
                ParticleEmit.Tracer(Particles, wallHit.Point, projectile.FromFrank);
                ParticleEmit.Impact(Particles, wallHit.Point, wallHit.Normal, _random);
                Decals.Add(new SpriteDecal
                {
                    Position = wallHit.Point + wallHit.Normal * 0.02f,
                    Elevation = 0.65f,
                    Width = 0.2f,
                    Height = 0.2f,
                    Rotation = _random.NextSingle() * MathF.PI,
                    Color = new Rgba32(220, 190, 110, 190),
                    Kind = (int)DecalKind.SparkMark,
                });
            }

            return true;
        }

        return false;
    }

    private void TickUndead(float dt, PlanarCollisionWorld collision)
    {
        for (var i = Undead.Count - 1; i >= 0; i--)
        {
            var foe = Undead[i];
            foe.HitFlash = MathF.Max(0f, foe.HitFlash - dt);
            var toFrank = Frank.Position - foe.Position;
            var distance = toFrank.LengthXz();
            if (!foe.Alerted
                && (_noiseTimer > 0f
                    && Vector3PlanarExtensions.DistanceXz(foe.Position, _noisePosition) < _noiseRadius
                    || distance < 7f))
            {
                foe.Alerted = true;
            }

            if (!foe.Alerted)
            {
                continue;
            }

            if (distance > 0.01f)
            {
                foe.Facing = toFrank.NormalizeXz();
            }

            if (foe.IsRanged && distance > 5.5f)
            {
                foe.AttackTimer -= dt;
                if (foe.AttackTimer <= 0f)
                {
                    foe.AttackTimer = 1.2f;
                    Projectiles.Add(new RangeProjectile
                    {
                        Position = foe.Position + foe.Facing * 0.4f,
                        Velocity = foe.Facing * 34f,
                        Damage = 9,
                        FromFrank = false,
                    });
                    EmitNoise(12f, foe.Position);
                }

                continue;
            }

            if (distance > foe.Radius + FrankActor.Radius + 0.08f)
            {
                var step = foe.Facing * foe.Speed * dt;
                foe.Position = collision.MoveCircle(foe.Position, step, foe.Radius);
                continue;
            }

            foe.AttackTimer -= dt;
            if (foe.AttackTimer > 0f)
            {
                continue;
            }

            foe.AttackTimer = foe.Kind == EnemyKind.Runner ? 0.45f : 0.7f;
            HurtFrank(foe.Kind == EnemyKind.ContaminatedSecurity ? 14 : 10);
        }
    }

    private void TickEncounters(CampaignState campaign)
    {
        foreach (var encounter in Encounters)
        {
            if (!encounter.HasTriggered
                && Near(Frank.Position, encounter.TriggerPosition, encounter.TriggerRadius))
            {
                encounter.HasTriggered = true;
                campaign.SetFlag($"{encounter.Id}:started");
                foreach (var spawn in encounter.Spawns)
                {
                    var enemy = UndeadActor.Create(spawn.Kind, spawn.Position);
                    enemy.EncounterId = encounter.Id;
                    enemy.Alerted = true;
                    Undead.Add(enemy);
                }

                Faith.Say(
                    encounter.Id == "large-containment-incident"
                        ? "Containment incident classified as large. Frank, this is the part where the paperwork loses."
                        : "Hostile signatures detected. Security response is now regrettably local.",
                    5f,
                    priority: 80);
                EmitNoise(22f);
            }

            if (encounter.HasTriggered
                && !encounter.IsComplete
                && !Undead.Any(enemy =>
                    string.Equals(enemy.EncounterId, encounter.Id, StringComparison.Ordinal)))
            {
                encounter.IsComplete = true;
                campaign.SetFlag($"{encounter.Id}:complete");
                if (encounter.Id == "security-breach")
                {
                    campaign.SetFlag("containment-clearance");
                    campaign.CurrentObjectiveId = "containment-clearance";
                    Faith.Say(
                        "Security office restored. The containment gate is now authorized.",
                        5f,
                        priority: 100,
                        instruction: true);
                }
                else if (encounter.Id == "large-containment-incident")
                {
                    campaign.Level68IncidentResolved = true;
                    campaign.CompleteObjective("reach-upward-route");
                    Faith.Say(
                        "Level 68 stabilized. The upward route is available. Going home remains a later problem.",
                        6f,
                        priority: 100,
                        instruction: true);
                }
            }
        }
    }

    private void TickPickups(CampaignState campaign)
    {
        foreach (var pickup in Pickups)
        {
            if (pickup.Taken
                || Vector3PlanarExtensions.DistanceXz(Frank.Position, pickup.Position) > 0.8f)
            {
                continue;
            }

            pickup.Taken = true;
            switch (pickup.Kind)
            {
                case PickupKind.Pump12:
                    campaign.Grant(WeaponId.Pump12);
                    break;
                case PickupKind.Lever1894:
                    campaign.Grant(WeaponId.Lever1894);
                    break;
                case PickupKind.Automatic:
                    campaign.Grant(WeaponId.Automatic);
                    break;
                case PickupKind.Explosive:
                    campaign.Grant(WeaponId.Explosive);
                    break;
                case PickupKind.Ammo45:
                    AddReserve(WeaponId.M1911, 28);
                    break;
                case PickupKind.Ammo12:
                    AddReserve(WeaponId.Pump12, 16);
                    break;
                case PickupKind.Ammo3030:
                    AddReserve(WeaponId.Lever1894, 12);
                    break;
                case PickupKind.Ammo9:
                    AddReserve(WeaponId.Automatic, 45);
                    break;
                case PickupKind.Ammo40:
                    AddReserve(WeaponId.Explosive, 3);
                    break;
                case PickupKind.Health:
                    Frank.Health = Math.Min(100, Frank.Health + 40);
                    break;
                case PickupKind.Armor:
                    Frank.Armor = Math.Min(75, Frank.Armor + 30);
                    break;
            }

            Faith.Say(
                pickup.Label + " recovered. The facility inventory still works.",
                3.8f,
                priority: 55);
        }
    }

    private void AddReserve(WeaponId id, int amount)
    {
        if (Weapon.Spec.Id == id)
        {
            Weapon.Reserve += amount;
            return;
        }

        if (_stash.TryGetValue(id, out var saved))
        {
            _stash[id] = saved with { Reserve = saved.Reserve + amount };
            return;
        }

        var spec = WeaponCatalog.FromId(id);
        _stash[id] = new WeaponSnapshot(
            spec.ReserveStart + amount,
            spec.MagazineSize,
            spec.ChamberPlusOne);
    }

    private void HurtUndead(UndeadActor foe, int damage, Vector3 incoming)
    {
        foe.Health -= damage;
        foe.HitFlash = 0.08f;
        foe.Alerted = true;
        ParticleEmit.Blood(
            Particles,
            foe.Position,
            incoming,
            foe.Kind == EnemyKind.ContaminatedSecurity ? 1.2f : 1f,
            _random);
        Decals.Add(new SpriteDecal
        {
            Position = foe.Position + incoming * 0.15f,
            Width = 0.35f + _random.NextSingle() * 0.35f,
            Height = 0.22f,
            Rotation = _random.NextSingle() * MathF.PI,
            Color = new Rgba32(120, 10, 14, 150),
            Kind = (int)DecalKind.BloodFloor,
        });

        if (foe.Health > 0)
        {
            return;
        }

        Kills++;
        ParticleEmit.Blood(Particles, foe.Position, incoming, 1.8f, _random);
        Corpses.Add(new Corpse
        {
            Kind = foe.Kind,
            Position = foe.Position,
            Rotation = _random.NextSingle() * MathF.PI,
        });
        Decals.Add(new SpriteDecal
        {
            Position = foe.Position,
            Width = 0.9f,
            Height = 0.55f,
            Rotation = _random.NextSingle() * MathF.PI,
            Color = new Rgba32(90, 8, 12, 200),
            Kind = (int)DecalKind.BloodFloor,
        });
        Undead.Remove(foe);
    }

    private void Explode(Vector3 position)
    {
        _lastExplosionPosition = position;
        ParticleEmit.Explosion(Particles, position, 1.25f, _random);
        Juice.AddShake(0.55f);
        Juice.FlashExplosion();
        Decals.Add(new SpriteDecal
        {
            Position = position,
            Width = 1.6f,
            Height = 1.6f,
            Color = new Rgba32(28, 18, 12, 210),
            Kind = (int)DecalKind.Scorch,
        });

        foreach (var foe in Undead.ToArray())
        {
            var distance = Vector3PlanarExtensions.DistanceXz(foe.Position, position);
            if (distance < 3.4f)
            {
                HurtUndead(foe, Math.Max(1, (int)(100f * (1f - distance / 3.4f))), foe.Position - position);
            }
        }

        if (Vector3PlanarExtensions.DistanceXz(Frank.Position, position) < 2.6f)
        {
            HurtFrank(28);
        }
    }

    private void HurtFrank(int amount)
    {
        if (Frank.IFrames > 0f)
        {
            return;
        }

        var armorDamage = Math.Min(Frank.Armor, amount / 2);
        Frank.Armor -= armorDamage;
        Frank.Health = Math.Max(0, Frank.Health - amount + armorDamage);
        Frank.IFrames = 0.35f;
        Juice.AddShake(0.28f);
        ParticleEmit.Blood(Particles, Frank.Position, -Frank.Facing, 0.6f, _random);
    }

    private void OpenGate(GateDefinition gate, CampaignState campaign)
    {
        gate.IsOpen = true;
        campaign.SetFlag($"{gate.Id}:open");
    }

    private void EmitNoise(float radius, Vector3? origin = null)
    {
        _noisePosition = origin ?? Frank.Position;
        _noiseRadius = radius;
        _noiseTimer = 1.2f;
    }

    private static bool Near(Vector3 left, Vector3 right, float radius) =>
        Vector3PlanarExtensions.DistanceXz(left, right) <= radius;

    private static bool IsInside(Vector3 position, RoomDefinition room) =>
        position.X >= room.MinX
        && position.X <= room.MaxX
        && position.Z >= room.MinZ
        && position.Z <= room.MaxZ;

    private readonly record struct WeaponSnapshot(
        int Reserve,
        int Magazine,
        bool Chambered);
}
