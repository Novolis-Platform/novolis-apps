using FrankMoat.Game;

namespace FrankMoat.Levels;

internal sealed class ElevatorSystem
{
    public ElevatorState State { get; private set; } = ElevatorState.Idle;

    public FloorId? Destination { get; private set; }

    public float Timer { get; private set; }

    public string StatusText { get; private set; } = "Elevator idle";

    public bool TryStart(CampaignState campaign, FloorId destination)
    {
        if (State != ElevatorState.Idle || !CanTravel(campaign, destination))
        {
            return false;
        }

        Destination = destination;
        State = ElevatorState.DoorsOpening;
        Timer = 0.7f;
        StatusText = $"Doors opening for {DisplayFloor(destination)}";
        return true;
    }

    public FloorId? Tick(float dt)
    {
        if (State == ElevatorState.Idle || Destination is null)
        {
            return null;
        }

        Timer -= dt;
        if (Timer > 0f)
        {
            return null;
        }

        switch (State)
        {
            case ElevatorState.DoorsOpening:
                State = ElevatorState.Boarding;
                Timer = 1.1f;
                StatusText = "Boarding. FAITH recommends remaining inside the elevator.";
                break;
            case ElevatorState.Boarding:
                State = ElevatorState.InTransit;
                Timer = 2.4f;
                StatusText = $"In transit to {DisplayFloor(Destination.Value)}";
                break;
            case ElevatorState.InTransit:
                State = ElevatorState.DoorsOpeningAtDestination;
                Timer = 0.8f;
                StatusText = "Arrival sequence complete.";
                break;
            case ElevatorState.DoorsOpeningAtDestination:
                var arrived = Destination;
                Destination = null;
                State = ElevatorState.Idle;
                StatusText = "Elevator idle";
                return arrived;
        }

        return null;
    }

    public static bool CanTravel(CampaignState campaign, FloorId destination)
    {
        return campaign.CurrentFloor switch
        {
            FloorId.Level0 => destination == FloorId.Level69 && campaign.DirectiveActive,
            FloorId.Level69 => destination == FloorId.Level68 && campaign.ServiceRouteUnlocked,
            FloorId.Level68 => destination == FloorId.Level69 && campaign.Level68IncidentResolved,
            _ => false,
        };
    }

    public static string DisplayFloor(FloorId floor) => floor switch
    {
        FloorId.Level0 => "LEVEL 0",
        FloorId.Level69 => "LEVEL 69",
        FloorId.Level68 => "LEVEL 68",
        _ => floor.ToString(),
    };
}
