using System.Numerics;
using FrankMoat.Actors;

namespace FrankMoat.Levels;

internal static class FacilityCatalog
{
    public static IReadOnlyList<FloorDefinition> Build()
        =>
        [
            BuildLobby(),
            BuildFounderFloor(),
            BuildCombatFloor(),
        ];

    public static FloorDefinition Find(IReadOnlyList<FloorDefinition> floors, FloorId id) =>
        floors.First(floor => floor.Id == id);

    private static FloorDefinition BuildLobby() =>
        new()
        {
            Id = FloorId.Level0,
            Name = "LEVEL 0 · MORNING LOBBY",
            StartPosition = new Vector3(6f, 0f, 6f),
            Rooms =
            [
                new RoomDefinition("lobby", "Main Lobby", 1.2f, 1.2f, 40.8f, 28.8f),
                new RoomDefinition("reception", "Reception", 2.5f, 3f, 13f, 10f),
                new RoomDefinition("loading", "Loading Route", 28f, 3f, 40f, 10f),
                new RoomDefinition("private-lift", "Founder Elevator", 18f, 21f, 25f, 28f),
            ],
            Walls = OuterWalls(42f, 30f, WallMaterial.Concrete,
                [
                    WallFactory.Box(13f, 2f, 14f, 12f, 1.2f, WallMaterial.Steel),
                    WallFactory.Box(27f, 2f, 28f, 12f, 1.2f, WallMaterial.Steel),
                    WallFactory.Box(15f, 19f, 27f, 20f, 1.2f, WallMaterial.Steel),
                    WallFactory.Box(15f, 20f, 16f, 28f, 1.2f, WallMaterial.Steel),
                    WallFactory.Box(26f, 20f, 27f, 28f, 1.2f, WallMaterial.Steel),
                ]),
            Gates = [],
            Encounters = [],
            Secrets = [],
            Fixtures =
            [
                new LightFixtureDefinition(new Vector3(7f, 0f, 7f), new(210, 225, 255), 8f, 1f),
                new LightFixtureDefinition(new Vector3(20f, 0f, 7f), new(210, 225, 255), 8f, 1f),
                new LightFixtureDefinition(new Vector3(34f, 0f, 7f), new(210, 225, 255), 8f, 1f),
                new LightFixtureDefinition(new Vector3(20f, 0f, 24f), new(210, 225, 255), 7f, 1f),
                new LightFixtureDefinition(new Vector3(4f, 0f, 5f), new(210, 225, 255), 5f, 0.8f),
            ],
            Props =
            [
                new FacilityProp("reception-counter", PropKind.Counter, new Vector3(8f, 0f, 8f), new Vector2(4.8f, 1.3f)),
                new FacilityProp("reception-console", PropKind.Console, new Vector3(7f, 0f, 6f), new Vector2(1.1f, 0.9f)),
                new FacilityProp("lobby-table", PropKind.Table, new Vector3(21f, 0f, 7f), new Vector2(3.2f, 1.8f)),
                new FacilityProp("private-elevator", PropKind.Elevator, new Vector3(21f, 0f, 24f), new Vector2(4.2f, 2.8f)),
                new FacilityProp("loading-cabinet", PropKind.Cabinet, new Vector3(34f, 0f, 7f), new Vector2(2.2f, 1.2f)),
            ],
            PrimaryObjective = new ObjectiveDefinition(
                "morning",
                "Finish the morning round",
                "Check the reception station and follow the routine before anything gets worse."),
            EntryPrompt = "The lobby is open. The lobby is always open.",
            ExitPrompt = "Founder elevator access confirmed.",
        };

    private static FloorDefinition BuildFounderFloor() =>
        new()
        {
            Id = FloorId.Level69,
            Name = "LEVEL 69 · FOUNDER PRESERVE",
            StartPosition = new Vector3(6f, 0f, 6f),
            Rooms =
            [
                new RoomDefinition("office", "Founder Office", 2f, 2f, 16f, 12f),
                new RoomDefinition("gallery", "Memorial Gallery", 18f, 2f, 31f, 12f),
                new RoomDefinition("range", "Private Range", 2f, 17f, 22f, 31f),
                new RoomDefinition("vault", "Prototype Vault", 25f, 17f, 38f, 25f),
                new RoomDefinition("bunker", "Emergency Bunker", 25f, 27f, 38f, 33f),
            ],
            Walls = OuterWalls(40f, 35f, WallMaterial.Founder,
                [
                    WallFactory.Box(16f, 1.5f, 17f, 15f, 1.4f, WallMaterial.Founder),
                    WallFactory.Box(23f, 1.5f, 24f, 33.5f, 1.4f, WallMaterial.Founder),
                    WallFactory.Box(2f, 15f, 23f, 16f, 1.4f, WallMaterial.Founder),
                    WallFactory.Box(24f, 25f, 39f, 26f, 1.4f, WallMaterial.Founder),
                ]),
            Gates =
            [
                new GateDefinition
                {
                    Id = "service-route",
                    Position = new Vector3(36.5f, 0f, 29.5f),
                    Width = 2.2f,
                    RequiredPermission = "service-route",
                },
            ],
            Encounters = [],
            Secrets =
            [
                new SecretDefinition
                {
                    Id = "founder-bar",
                    Position = new Vector3(34f, 0f, 7f),
                    RewardText = "A founder memorandum explains why the bar has a blast door.",
                },
            ],
            Fixtures =
            [
                new LightFixtureDefinition(new Vector3(8f, 0f, 7f), new(255, 222, 164), 9f, 1.2f),
                new LightFixtureDefinition(new Vector3(26f, 0f, 7f), new(255, 222, 164), 9f, 1.2f),
                new LightFixtureDefinition(new Vector3(10f, 0f, 24f), new(220, 230, 255), 8f, 1f),
                new LightFixtureDefinition(new Vector3(31f, 0f, 21f), new(255, 220, 150), 6f, 1f),
                new LightFixtureDefinition(new Vector3(31f, 0f, 30f), new(255, 220, 150), 5f, 1f),
            ],
            Props =
            [
                new FacilityProp("founder-console", PropKind.Console, new Vector3(10f, 0f, 7f), new Vector2(1.2f, 0.9f)),
                new FacilityProp("gallery-shelf-a", PropKind.Shelf, new Vector3(24f, 0f, 7f), new Vector2(3.8f, 1.1f)),
                new FacilityProp("gallery-shelf-b", PropKind.Shelf, new Vector3(29f, 0f, 7f), new Vector2(3.8f, 1.1f)),
                new FacilityProp("loadout-table", PropKind.Table, new Vector3(11f, 0f, 25f), new Vector2(5.8f, 1.8f)),
                new FacilityProp("prototype-cabinet", PropKind.Cabinet, new Vector3(31f, 0f, 21f), new Vector2(3.2f, 2.2f)),
                new FacilityProp("service-elevator", PropKind.Elevator, new Vector3(36.5f, 0f, 29.5f), new Vector2(4.2f, 2.8f)),
            ],
            PrimaryObjective = new ObjectiveDefinition(
                "choose-loadout",
                "Choose an initial loadout",
                "Visit the collection, inspect the range, and select what Frank will carry downstairs."),
            EntryPrompt = "The founder's private preserve survived the lockdown.",
            ExitPrompt = "Service route available. The express elevator remains locked.",
        };

    private static FloorDefinition BuildCombatFloor() =>
        new()
        {
            Id = FloorId.Level68,
            Name = "LEVEL 68 · CONTAINMENT",
            StartPosition = new Vector3(5f, 0f, 5f),
            Rooms =
            [
                new RoomDefinition("service", "Service Arrival", 2f, 2f, 12f, 12f),
                new RoomDefinition("security", "Security Office", 15f, 2f, 27f, 12f),
                new RoomDefinition("processing", "Processing Floor", 30f, 2f, 48f, 18f),
                new RoomDefinition("coolant", "Coolant Loop", 2f, 20f, 21f, 33f),
                new RoomDefinition("containment", "Containment Hall", 25f, 21f, 48f, 33f),
            ],
            Walls = OuterWalls(50f, 36f, WallMaterial.Concrete,
                [
                    WallFactory.Box(13f, 1.5f, 14f, 24f, 1.4f, WallMaterial.Steel),
                    WallFactory.Box(28f, 1.5f, 29f, 20f, 1.4f, WallMaterial.Steel),
                    WallFactory.Box(21f, 20f, 22f, 34.5f, 1.4f, WallMaterial.Steel),
                    WallFactory.Box(2f, 18f, 26.5f, 19f, 1.4f, WallMaterial.Concrete),
                    WallFactory.Box(30.5f, 18f, 49f, 19f, 1.4f, WallMaterial.Concrete),
                    WallFactory.Box(39f, 20f, 40f, 34.5f, 1.4f, WallMaterial.Steel),
                ]),
            Gates =
            [
                new GateDefinition
                {
                    Id = "containment-gate",
                    Position = new Vector3(28.5f, 0f, 18.5f),
                    Width = 4f,
                    RequiredPermission = "containment-clearance",
                },
            ],
            Encounters =
            [
                new EncounterDefinition
                {
                    Id = "security-breach",
                    TriggerPosition = new Vector3(20f, 0f, 8f),
                    TriggerRadius = 4.4f,
                    Spawns =
                    [
                        new EnemySpawnDefinition(EnemyKind.Shambler, new Vector3(18f, 0f, 8f)),
                        new EnemySpawnDefinition(EnemyKind.Runner, new Vector3(22f, 0f, 9f)),
                        new EnemySpawnDefinition(
                            EnemyKind.ContaminatedSecurity,
                            new Vector3(24f, 0f, 6f)),
                    ],
                },
                new EncounterDefinition
                {
                    Id = "large-containment-incident",
                    TriggerPosition = new Vector3(34f, 0f, 26f),
                    TriggerRadius = 5.5f,
                    Spawns =
                    [
                        new EnemySpawnDefinition(EnemyKind.Shambler, new Vector3(34f, 0f, 27f)),
                        new EnemySpawnDefinition(EnemyKind.Shambler, new Vector3(38f, 0f, 28f)),
                        new EnemySpawnDefinition(EnemyKind.Runner, new Vector3(42f, 0f, 27f)),
                        new EnemySpawnDefinition(
                            EnemyKind.ContaminatedSecurity,
                            new Vector3(44f, 0f, 30f)),
                    ],
                },
            ],
            Secrets =
            [
                new SecretDefinition
                {
                    Id = "maintenance-cache",
                    Position = new Vector3(8f, 0f, 28f),
                    RewardText = "Maintenance cache recovered: ammunition and a very judgmental memo.",
                },
            ],
            Fixtures =
            [
                new LightFixtureDefinition(new Vector3(7f, 0f, 7f), new(160, 190, 225), 7f, 0.7f),
                new LightFixtureDefinition(new Vector3(21f, 0f, 7f), new(210, 220, 235), 7f, 0.8f),
                new LightFixtureDefinition(new Vector3(39f, 0f, 8f), new(180, 200, 225), 7f, 0.8f),
                new LightFixtureDefinition(new Vector3(10f, 0f, 27f), new(130, 170, 210), 7f, 0.65f),
                new LightFixtureDefinition(new Vector3(33f, 0f, 27f), new(170, 200, 225), 8f, 0.8f),
                new LightFixtureDefinition(new Vector3(45f, 0f, 30f), new(200, 80, 60), 5f, 0.8f, true),
            ],
            Props =
            [
                new FacilityProp("service-elevator", PropKind.Elevator, new Vector3(6f, 0f, 6f), new Vector2(4.2f, 2.8f)),
                new FacilityProp("security-console", PropKind.Console, new Vector3(20f, 0f, 8f), new Vector2(1.3f, 1f)),
                new FacilityProp("security-cabinets", PropKind.Cabinet, new Vector3(24f, 0f, 6f), new Vector2(3.2f, 1.2f)),
                new FacilityProp("coolant-pipes-a", PropKind.PipeBank, new Vector3(8f, 0f, 27f), new Vector2(5.2f, 1.1f)),
                new FacilityProp("coolant-pipes-b", PropKind.PipeBank, new Vector3(15f, 0f, 30f), new Vector2(4.2f, 1.1f)),
                new FacilityProp("containment-cabinets", PropKind.Cabinet, new Vector3(34f, 0f, 30f), new Vector2(3.8f, 1.3f)),
                new FacilityProp("upward-elevator", PropKind.Elevator, new Vector3(45f, 0f, 30f), new Vector2(4.2f, 2.8f)),
            ],
            PrimaryObjective = new ObjectiveDefinition(
                "containment-incident",
                "Stabilize Level 68",
                "Restore the containment gate, survive the incident, and reach the upward service route."),
            EntryPrompt = "The service lift opens onto a floor that should be empty.",
            ExitPrompt = "Containment is stable enough to move upward.",
        };

    private static List<RaisedWall> OuterWalls(
        float width,
        float height,
        WallMaterial material,
        IEnumerable<RaisedWall> interior)
    {
        var walls = new List<RaisedWall>
        {
            WallFactory.Box(-1.4f, -1.4f, width + 1.4f, 0f, 2.8f, material),
            WallFactory.Box(-1.4f, height, width + 1.4f, height + 1.4f, 2.8f, material),
            WallFactory.Box(-1.4f, 0f, 0f, height, 2.8f, material),
            WallFactory.Box(width, 0f, width + 1.4f, height, 2.8f, material),
        };
        walls.AddRange(interior);
        return walls;
    }
}
