using System.Numerics;

namespace FrankMoat.Actors;

internal sealed class RangePickup
{
    public required PickupKind Kind { get; init; }

    public required Vector3 Position { get; init; }

    public bool Taken { get; set; }

    public string Label => Kind switch
    {
        PickupKind.Pump12 => "PUMP 12",
        PickupKind.Lever1894 => "WINCHESTER",
        PickupKind.Automatic => "AUTOMATIC",
        PickupKind.Explosive => "BUNKER LAUNCHER",
        PickupKind.Health => "MEDICAL KIT",
        PickupKind.Armor => "BODY ARMOR",
        _ => "AMMUNITION",
    };
}
