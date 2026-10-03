namespace Novolis.Hours.FeatureTests;

/// <summary>Deterministic concrete time source for full-product scheduler feature tests.</summary>
internal sealed class FrozenTimeProvider : TimeProvider
{
    private readonly DateTimeOffset utcNow;

    /// <summary>Initializes the time source at a fixed UTC instant.</summary>
    public FrozenTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;
}
