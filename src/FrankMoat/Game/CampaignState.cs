using System.Numerics;
using System.Text.Json.Serialization;
using FrankMoat.Levels;
using FrankMoat.Weapons;

namespace FrankMoat.Game;

internal sealed class CampaignState
{
    public FloorId CurrentFloor { get; set; } = FloorId.Level0;

    public bool DirectiveActive { get; set; }

    public bool ExpressReturnLocked { get; set; }

    public bool ServiceRouteUnlocked { get; set; }

    public bool Level0Briefed { get; set; }

    public bool Level69LoadoutChosen { get; set; }

    public bool Level68IncidentResolved { get; set; }

    public string CurrentObjectiveId { get; set; } = "morning";

    public int Health { get; set; } = 100;

    public int Armor { get; set; } = 25;

    public float PlayerX { get; set; } = 6f;

    public float PlayerZ { get; set; } = 6f;

    public WeaponId CurrentWeapon { get; set; } = WeaponId.M1911;

    public int CurrentReserve { get; set; } = WeaponCatalog.M1911.ReserveStart;

    public int CurrentMagazine { get; set; } = WeaponCatalog.M1911.MagazineSize;

    public bool CurrentChambered { get; set; } = WeaponCatalog.M1911.ChamberPlusOne;

    public int WorldSeed { get; set; } = 4761;

    public List<WeaponId> OwnedWeapons { get; set; } = [WeaponId.M1911];

    public List<string> Flags { get; set; } = [];

    public List<string> CompletedObjectives { get; set; } = [];

    [JsonIgnore]
    public Vector3 PlayerPosition
    {
        get => new(PlayerX, 0f, PlayerZ);
        set
        {
            PlayerX = value.X;
            PlayerZ = value.Z;
        }
    }

    public void Reset()
    {
        CurrentFloor = FloorId.Level0;
        DirectiveActive = false;
        ExpressReturnLocked = false;
        ServiceRouteUnlocked = false;
        Level0Briefed = false;
        Level69LoadoutChosen = false;
        Level68IncidentResolved = false;
        CurrentObjectiveId = "morning";
        Health = 100;
        Armor = 25;
        PlayerX = 6f;
        PlayerZ = 6f;
        CurrentWeapon = WeaponId.M1911;
        CurrentReserve = WeaponCatalog.M1911.ReserveStart;
        CurrentMagazine = WeaponCatalog.M1911.MagazineSize;
        CurrentChambered = WeaponCatalog.M1911.ChamberPlusOne;
        WorldSeed = 4761;
        OwnedWeapons = [WeaponId.M1911];
        Flags.Clear();
        CompletedObjectives.Clear();
    }

    public bool HasFlag(string id) =>
        Flags.Contains(id, StringComparer.OrdinalIgnoreCase);

    public void SetFlag(string id)
    {
        if (!HasFlag(id))
        {
            Flags.Add(id);
        }
    }

    public void CompleteObjective(string id)
    {
        CurrentObjectiveId = id;
        if (!CompletedObjectives.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            CompletedObjectives.Add(id);
        }
    }

    public bool Owns(WeaponId weapon) =>
        OwnedWeapons.Contains(weapon);

    public void Grant(WeaponId weapon)
    {
        if (!Owns(weapon))
        {
            OwnedWeapons.Add(weapon);
        }
    }
}
