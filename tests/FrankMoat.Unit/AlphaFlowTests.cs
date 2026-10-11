using System.Numerics;
using Avalonia.Input;
using FrankMoat.Game;
using FrankMoat.Input;
using FrankMoat.Levels;
using FrankMoat.Narrative;
using FrankMoat.Rendering;
using FrankMoat.Weapons;
using Novolis.Rendering.Planar;

namespace FrankMoat.Unit;

public sealed class AlphaFlowTests
{
    [Test]
    public async Task Campaign_starts_at_lobby_and_tracks_permissions()
    {
        var campaign = new CampaignState();

        await Assert.That(campaign.CurrentFloor).IsEqualTo(FloorId.Level0);
        await Assert.That(campaign.Owns(WeaponId.M1911)).IsTrue();

        campaign.DirectiveActive = true;
        campaign.SetFlag("directive-4761");
        campaign.ServiceRouteUnlocked = true;

        await Assert.That(campaign.HasFlag("DIRECTIVE-4761")).IsTrue();
        await Assert.That(campaign.ServiceRouteUnlocked).IsTrue();
    }

    [Test]
    public async Task Elevator_requires_directive_then_reaches_founder_floor()
    {
        var campaign = new CampaignState
        {
            DirectiveActive = true,
            CurrentFloor = FloorId.Level0,
        };
        var elevator = new ElevatorSystem();

        await Assert.That(elevator.TryStart(campaign, FloorId.Level69)).IsTrue();
        FloorId? arrived = null;
        for (var i = 0; i < 60; i++)
        {
            arrived ??= elevator.Tick(0.1f);
        }

        await Assert.That(elevator.State).IsEqualTo(ElevatorState.Idle);
        await Assert.That(elevator.StatusText).IsEqualTo("Elevator idle");
        await Assert.That(arrived).IsEqualTo(FloorId.Level69);
    }

    [Test]
    public async Task Five_alpha_weapons_have_distinct_fire_modes()
    {
        await Assert.That(WeaponCatalog.All).Count().IsEqualTo(5);
        await Assert.That(WeaponCatalog.M1911.Mode).IsEqualTo(WeaponFireMode.Semi);
        await Assert.That(WeaponCatalog.Pump12.Mode).IsEqualTo(WeaponFireMode.Pump);
        await Assert.That(WeaponCatalog.Automatic.Mode).IsEqualTo(WeaponFireMode.Automatic);
        await Assert.That(WeaponCatalog.Explosive.Explosive).IsTrue();
    }

    [Test]
    public async Task Save_round_trip_preserves_floor_and_weapon_state()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "frank-moat-" + Guid.NewGuid().ToString("N"),
            "save.json");
        try
        {
            var store = new CampaignSaveStore(path);
            var campaign = new CampaignState
            {
                CurrentFloor = FloorId.Level68,
                CurrentWeapon = WeaponId.Pump12,
                CurrentReserve = 19,
                CurrentMagazine = 4,
                CurrentChambered = false,
                PlayerX = 12.5f,
                PlayerZ = 18.25f,
            };
            campaign.Grant(WeaponId.Pump12);
            campaign.SetFlag("containment-clearance");
            store.Save(campaign);

            var loaded = store.TryLoad();

            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.CurrentFloor).IsEqualTo(FloorId.Level68);
            await Assert.That(loaded.CurrentWeapon).IsEqualTo(WeaponId.Pump12);
            await Assert.That(loaded.CurrentReserve).IsEqualTo(19);
            await Assert.That(loaded.CurrentMagazine).IsEqualTo(4);
            await Assert.That(loaded.PlayerPosition.X).IsEqualTo(12.5f);
            await Assert.That(loaded.HasFlag("containment-clearance")).IsTrue();
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task Input_edges_survive_until_the_simulation_consumes_them()
    {
        var scene = new PlanarScene();
        scene.Camera.ViewportWidth = 800;
        scene.Camera.ViewportHeight = 600;
        var input = new FrankInputBuffer();

        input.KeyDown(Key.E);
        var first = input.BuildFrame(
            scene,
            new Vector2(400f, 300f),
            800,
            600,
            Vector3.Zero,
            default);
        var repeated = input.BuildFrame(
            scene,
            new Vector2(400f, 300f),
            800,
            600,
            Vector3.Zero,
            default);

        await Assert.That(first.InteractPressed).IsTrue();
        await Assert.That(repeated.InteractPressed).IsTrue();

        input.ConsumeEdges();
        var consumed = input.BuildFrame(
            scene,
            new Vector2(400f, 300f),
            800,
            600,
            Vector3.Zero,
            default);
        await Assert.That(consumed.InteractPressed).IsFalse();
    }

    [Test]
    public async Task Control_mode_hotkey_switches_to_directional_aim()
    {
        var scene = new PlanarScene();
        var input = new FrankInputBuffer();

        input.KeyDown(Key.C);
        var toggle = input.BuildFrame(
            scene,
            new Vector2(400f, 300f),
            800,
            600,
            Vector3.Zero,
            default);
        input.ConsumeEdges();
        input.ToggleControlMode();
        input.KeyDown(Key.Right);
        var directional = input.BuildFrame(
            scene,
            new Vector2(400f, 300f),
            800,
            600,
            Vector3.Zero,
            default);

        await Assert.That(toggle.ControlModePressed).IsTrue();
        await Assert.That(input.ControlMode).IsEqualTo(ControlMode.DirectionalAim);
        await Assert.That(directional.AimWorld.X).IsEqualTo(1f);
        await Assert.That(directional.AimWorld.Z).IsEqualTo(0f);
    }

    [Test]
    public async Task Nearby_interaction_hints_identify_objects_and_locked_routes()
    {
        var campaign = new CampaignState();
        var world = new FrankMoatWorld();
        world.LoadFloor(
            FacilityCatalog.Find(FacilityCatalog.Build(), FloorId.Level0),
            campaign);
        world.Frank.Position = new Vector3(7f, 0f, 6f);

        var reception = world.NearestInteraction(campaign);

        await Assert.That(reception).IsNotNull();
        await Assert.That(reception!.Label).IsEqualTo("CHECK RECEPTION STATION");
        await Assert.That(reception.IsAvailable).IsTrue();
        await Assert.That(reception.IsInRange(world.Frank.Position)).IsTrue();

        world.Frank.Position = new Vector3(21f, 0f, 24f);
        var elevator = world.NearestInteraction(campaign);

        await Assert.That(elevator).IsNotNull();
        await Assert.That(elevator!.IsAvailable).IsFalse();
        await Assert.That(elevator.LockReason).Contains("DIRECTIVE");
    }

    [Test]
    public async Task Campaign_worlds_keep_the_alpha_flow_connected()
    {
        var floors = FacilityCatalog.Build();
        var campaign = new CampaignState();
        var lobby = new FrankMoatWorld();
        lobby.LoadFloor(FacilityCatalog.Find(floors, FloorId.Level0), campaign);
        lobby.Frank.Position = new Vector3(7f, 0f, 6f);

        var reception = lobby.Interact(campaign);

        await Assert.That(reception).IsNotNull();
        await Assert.That(campaign.DirectiveActive).IsTrue();
        await Assert.That(campaign.HasFlag("directive-4761")).IsTrue();

        campaign.CurrentFloor = FloorId.Level69;
        var preserve = new FrankMoatWorld();
        preserve.LoadFloor(FacilityCatalog.Find(floors, FloorId.Level69), campaign);
        preserve.Frank.Position = new Vector3(7f, 0f, 23f);
        var loadout = preserve.Interact(campaign);

        await Assert.That(loadout).IsNotNull();
        await Assert.That(campaign.Level69LoadoutChosen).IsTrue();
        await Assert.That(campaign.ServiceRouteUnlocked).IsTrue();
        await Assert.That(campaign.Owns(WeaponId.Explosive)).IsTrue();

        campaign.CurrentFloor = FloorId.Level68;
        var containment = new FrankMoatWorld();
        containment.LoadFloor(FacilityCatalog.Find(floors, FloorId.Level68), campaign);

        await Assert.That(containment.Encounters).Count().IsEqualTo(2);
        await Assert.That(containment.Gates.Single().IsOpen).IsFalse();
    }

    [Test]
    public async Task Elevator_routes_remain_locked_until_their_campaign_flags_exist()
    {
        var campaign = new CampaignState();

        await Assert.That(ElevatorSystem.CanTravel(campaign, FloorId.Level69)).IsFalse();

        campaign.DirectiveActive = true;
        await Assert.That(ElevatorSystem.CanTravel(campaign, FloorId.Level69)).IsTrue();

        campaign.CurrentFloor = FloorId.Level69;
        await Assert.That(ElevatorSystem.CanTravel(campaign, FloorId.Level68)).IsFalse();
        campaign.ServiceRouteUnlocked = true;
        await Assert.That(ElevatorSystem.CanTravel(campaign, FloorId.Level68)).IsTrue();

        campaign.CurrentFloor = FloorId.Level68;
        await Assert.That(ElevatorSystem.CanTravel(campaign, FloorId.Level69)).IsFalse();
        campaign.Level68IncidentResolved = true;
        await Assert.That(ElevatorSystem.CanTravel(campaign, FloorId.Level69)).IsTrue();
    }

    [Test]
    public async Task Reception_interaction_activates_directive_and_prioritized_instruction()
    {
        var campaign = new CampaignState();
        var world = new FrankMoatWorld();
        var lobby = FacilityCatalog.Find(FacilityCatalog.Build(), FloorId.Level0);
        world.LoadFloor(lobby, campaign);
        world.Frank.Position = new Vector3(7f, 0f, 6f);

        var result = world.Interact(campaign);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Message).Contains("Reception");
        await Assert.That(world.Faith.IsInstruction).IsTrue();
        await Assert.That(world.Faith.Text).Contains("Directive 4761");
        await Assert.That(campaign.CurrentObjectiveId).IsEqualTo("reach-private-elevator");
    }

    [Test]
    public async Task Level_68_gate_starts_locked_and_opens_after_clearance()
    {
        var campaign = new CampaignState
        {
            CurrentFloor = FloorId.Level68,
        };
        var world = new FrankMoatWorld();
        var floor = FacilityCatalog.Find(FacilityCatalog.Build(), FloorId.Level68);
        world.LoadFloor(floor, campaign);
        world.Frank.Position = floor.Gates.Single().Position;

        var locked = world.Interact(campaign);

        await Assert.That(locked).IsNotNull();
        await Assert.That(locked!.Message).Contains("locked");
        await Assert.That(world.Gates.Single().IsOpen).IsFalse();

        campaign.SetFlag("containment-clearance");
        var opened = world.Interact(campaign);

        await Assert.That(opened).IsNotNull();
        await Assert.That(opened!.Message).Contains("gate open");
        await Assert.That(world.Gates.Single().IsOpen).IsTrue();
        await Assert.That(campaign.HasFlag("containment-gate:open")).IsTrue();
    }

    [Test]
    public async Task Combat_floor_noise_alerts_the_seeded_perimeter()
    {
        var campaign = new CampaignState
        {
            CurrentFloor = FloorId.Level68,
        };
        var world = new FrankMoatWorld();
        var scene = new PlanarScene();
        world.LoadFloor(
            FacilityCatalog.Find(FacilityCatalog.Build(), FloorId.Level68),
            campaign);
        world.ApplyCollision(scene);
        world.Frank.Position = new Vector3(5f, 0f, 8f);
        world.Frank.Facing = Vector3.UnitX;

        world.Tick(1f / 60f, scene.Collision, ShootInput(Vector3.UnitX), campaign);

        await Assert.That(world.NoiseRadius).IsEqualTo(14f);

        world.Frank.Position = new Vector3(20f, 0f, 8f);
        world.Tick(1f / 60f, scene.Collision, EmptyInput(), campaign);
        await Assert.That(world.Undead).Count().IsEqualTo(3);
        await Assert.That(world.Undead.All(enemy => enemy.Alerted)).IsTrue();
    }

    [Test]
    public async Task Same_seed_and_input_produce_the_same_melee_damage()
    {
        var first = PrepareTriggeredCombat();
        var second = PrepareTriggeredCombat();
        first.World.Frank.Position = new Vector3(17f, 0f, 8f);
        second.World.Frank.Position = new Vector3(17f, 0f, 8f);

        first.World.Tick(1f / 60f, first.Scene.Collision, MeleeInput(), first.Campaign);
        second.World.Tick(1f / 60f, second.Scene.Collision, MeleeInput(), second.Campaign);

        await Assert.That(first.World.Undead[0].Health).IsEqualTo(second.World.Undead[0].Health);
        await Assert.That(first.World.Undead[0].Health).IsEqualTo(24);
    }

    [Test]
    public async Task Optional_maintenance_cache_is_reachable_and_grants_the_launcher()
    {
        var campaign = new CampaignState
        {
            CurrentFloor = FloorId.Level68,
        };
        var world = new FrankMoatWorld();
        world.LoadFloor(
            FacilityCatalog.Find(FacilityCatalog.Build(), FloorId.Level68),
            campaign);
        world.Frank.Position = new Vector3(8f, 0f, 28f);

        var result = world.Interact(campaign);

        await Assert.That(result).IsNotNull();
        await Assert.That(campaign.HasFlag("maintenance-cache:found")).IsTrue();
        await Assert.That(campaign.Owns(WeaponId.Explosive)).IsTrue();
    }

    [Test]
    public async Task Lighting_modes_follow_floor_lockdown_combat_and_transit()
    {
        var lighting = new LightingRig();

        lighting.SetMode(FloorId.Level0, directive: false, combat: false, elevator: false);
        await Assert.That(lighting.Mode).IsEqualTo(LightingMode.Normal);

        lighting.SetMode(FloorId.Level0, directive: true, combat: false, elevator: false);
        await Assert.That(lighting.Mode).IsEqualTo(LightingMode.Emergency);

        lighting.SetMode(FloorId.Level69, directive: true, combat: false, elevator: false);
        await Assert.That(lighting.Mode).IsEqualTo(LightingMode.Preserved);

        lighting.SetMode(FloorId.Level68, directive: true, combat: false, elevator: false);
        await Assert.That(lighting.Mode).IsEqualTo(LightingMode.DarkContainment);

        lighting.SetMode(FloorId.Level68, directive: true, combat: true, elevator: false);
        await Assert.That(lighting.Mode).IsEqualTo(LightingMode.Combat);

        lighting.SetMode(FloorId.Level68, directive: true, combat: false, elevator: true);
        await Assert.That(lighting.Mode).IsEqualTo(LightingMode.ElevatorTransit);
    }

    [Test]
    public async Task Faith_prioritizes_instructions_and_pauses_ambient_lines_in_combat()
    {
        var faith = new FaithDialogueQueue();
        faith.Say("Ambient", seconds: 0.1f, priority: 10);
        faith.Say("Instruction", seconds: 1f, priority: 100, instruction: true);

        await Assert.That(faith.Text).IsEqualTo("Ambient");
        faith.Tick(0.2f, combat: false);
        await Assert.That(faith.Text).IsEqualTo("Instruction");
        await Assert.That(faith.IsInstruction).IsTrue();

        faith.Say("Combat ambient", seconds: 1f, priority: 50);
        faith.Dismiss();
        await Assert.That(faith.Text).IsEqualTo("Combat ambient");
        faith.Tick(0.5f, combat: true);
        await Assert.That(faith.RemainingSeconds).IsEqualTo(1f);
    }

    private static FrankInputFrame EmptyInput() =>
        new(
            0f,
            0f,
            Vector3.UnitZ,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            0,
            0);

    private static FrankInputFrame ShootInput(Vector3 aim) =>
        EmptyInput() with
        {
            AimWorld = aim,
            ShootPressed = true,
        };

    private static FrankInputFrame MeleeInput() =>
        EmptyInput() with
        {
            MeleePressed = true,
        };

    private static CombatFixture PrepareTriggeredCombat()
    {
        var campaign = new CampaignState
        {
            CurrentFloor = FloorId.Level68,
        };
        var world = new FrankMoatWorld();
        var scene = new PlanarScene();
        world.LoadFloor(
            FacilityCatalog.Find(FacilityCatalog.Build(), FloorId.Level68),
            campaign);
        world.ApplyCollision(scene);
        world.Frank.Position = new Vector3(20f, 0f, 8f);
        world.Tick(1f / 60f, scene.Collision, EmptyInput(), campaign);
        return new CombatFixture(world, scene, campaign);
    }

    private sealed record CombatFixture(
        FrankMoatWorld World,
        PlanarScene Scene,
        CampaignState Campaign);
}
