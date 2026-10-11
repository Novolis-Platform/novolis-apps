namespace FrankMoat.Weapons;

internal static class WeaponCatalog
{
    public static readonly WeaponSpec M1911 = new(
        WeaponId.M1911,
        "M1911",
        ".45 ACP",
        WeaponFireMode.Semi,
        MagazineSize: 7,
        ChamberPlusOne: true,
        TubeFeed: false,
        FireIntervalSeconds: 0.16f,
        ReloadSeconds: 1.15f,
        ShellInsertSeconds: 0f,
        SpinUpSeconds: 0f,
        PelletCount: 1,
        SpreadDegrees: 1.4f,
        ProjectileSpeed: 95f,
        Damage: 28,
        RecoilShake: 0.09f,
        ReserveStart: 56,
        InspectLine: "Emotional support. Seven in the stick, one in the pipe.");

    public static readonly WeaponSpec Pump12 = new(
        WeaponId.Pump12,
        "Pump 12",
        "12 gauge",
        WeaponFireMode.Pump,
        MagazineSize: 8,
        ChamberPlusOne: false,
        TubeFeed: true,
        FireIntervalSeconds: 0.62f,
        ReloadSeconds: 0f,
        ShellInsertSeconds: 0.38f,
        SpinUpSeconds: 0f,
        PelletCount: 8,
        SpreadDegrees: 11f,
        ProjectileSpeed: 70f,
        Damage: 11,
        RecoilShake: 0.22f,
        ReserveStart: 32,
        InspectLine: "Tube-fed. I can top this up while they are still walking.");

    public static readonly WeaponSpec Lever1894 = new(
        WeaponId.Lever1894,
        "Winchester 1894",
        ".30-30",
        WeaponFireMode.Semi,
        MagazineSize: 8,
        ChamberPlusOne: false,
        TubeFeed: true,
        FireIntervalSeconds: 0.48f,
        ReloadSeconds: 0f,
        ShellInsertSeconds: 0.42f,
        SpinUpSeconds: 0f,
        PelletCount: 1,
        SpreadDegrees: 0.8f,
        ProjectileSpeed: 120f,
        Damage: 72,
        RecoilShake: 0.18f,
        ReserveStart: 24,
        InspectLine: "A lever, a cartridge, and a workplace safety memo waiting to happen.");

    public static readonly WeaponSpec Automatic = new(
        WeaponId.Automatic,
        "Compact Automatic",
        "9 mm",
        WeaponFireMode.Automatic,
        MagazineSize: 30,
        ChamberPlusOne: false,
        TubeFeed: false,
        FireIntervalSeconds: 0.085f,
        ReloadSeconds: 1.6f,
        ShellInsertSeconds: 0f,
        SpinUpSeconds: 0f,
        PelletCount: 1,
        SpreadDegrees: 4f,
        ProjectileSpeed: 105f,
        Damage: 16,
        RecoilShake: 0.055f,
        ReserveStart: 120,
        InspectLine: "Small, loud, and apparently approved by three departments.");

    public static readonly WeaponSpec Explosive = new(
        WeaponId.Explosive,
        "Bunker Launcher",
        "40 mm",
        WeaponFireMode.Explosive,
        MagazineSize: 1,
        ChamberPlusOne: false,
        TubeFeed: false,
        FireIntervalSeconds: 0.95f,
        ReloadSeconds: 1.1f,
        ShellInsertSeconds: 0f,
        SpinUpSeconds: 0f,
        PelletCount: 1,
        SpreadDegrees: 1.2f,
        ProjectileSpeed: 45f,
        Damage: 95,
        RecoilShake: 0.34f,
        ReserveStart: 6,
        InspectLine: "A controlled demolition tool with an optimistic name.",
        Explosive: true,
        BlastRadius: 3.2f);

    public static IReadOnlyList<WeaponSpec> All { get; } =
        [M1911, Pump12, Lever1894, Automatic, Explosive];

    public static WeaponSpec FromId(WeaponId id) => id switch
    {
        WeaponId.Pump12 => Pump12,
        WeaponId.Lever1894 => Lever1894,
        WeaponId.Automatic => Automatic,
        WeaponId.Explosive => Explosive,
        _ => M1911,
    };
}
