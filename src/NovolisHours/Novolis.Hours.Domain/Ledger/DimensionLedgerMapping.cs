namespace Novolis.Hours.Domain.Ledger;

/// <summary>Explicit mapping from a derived Dimension value to a flex movement.</summary>
public sealed record DimensionLedgerMapping
{
    /// <summary>Initializes a mapping.</summary>
    public DimensionLedgerMapping(
        string dimensionId,
        string valueId,
        decimal multiplier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueId);
        if (multiplier is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        DimensionId = dimensionId;
        ValueId = valueId;
        Multiplier = multiplier;
    }

    /// <summary>Dimension identity.</summary>
    public string DimensionId { get; }

    /// <summary>Derived Dimension value identity.</summary>
    public string ValueId { get; }

    /// <summary>Signed multiplier applied to the derived duration.</summary>
    public decimal Multiplier { get; }
}
