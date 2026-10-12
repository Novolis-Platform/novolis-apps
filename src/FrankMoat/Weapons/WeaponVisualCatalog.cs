namespace FrankMoat.Weapons;

internal static class WeaponVisualCatalog
{
    public static WeaponVisualDefinition For(WeaponId id) => id switch
    {
        WeaponId.Pump12 => new(0.3f, 1.35f, new(42, 46, 52), new(190, 148, 76), false),
        WeaponId.Lever1894 => new(0.26f, 1.48f, new(92, 54, 34), new(196, 164, 106), false),
        WeaponId.Automatic => new(0.28f, 1.25f, new(48, 55, 66), new(82, 180, 194), false),
        WeaponId.Explosive => new(0.42f, 1.32f, new(54, 58, 64), new(224, 92, 48), true),
        _ => new(0.24f, 1.08f, new(40, 44, 52), new(206, 178, 96), false),
    };
}
