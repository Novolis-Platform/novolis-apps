using FrankMoat.Levels;
using FrankMoat.Narrative;

namespace FrankMoat.Game;

internal sealed record WorldInteractionResult(
    string Message,
    FloorId? ElevatorDestination = null,
    FounderRecording? Recording = null);
