namespace FrankMoat.Game;

internal sealed class SaveRecord
{
    public required CampaignState Campaign { get; init; }

    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;
}
