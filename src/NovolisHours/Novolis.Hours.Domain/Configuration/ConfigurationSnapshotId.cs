namespace Novolis.Hours.Domain.Configuration;

/// <summary>Stable identity for the effective configuration used to interpret a fact.</summary>
public readonly record struct ConfigurationSnapshotId(Guid Value)
{
    /// <summary>Creates a new snapshot identity.</summary>
    public static ConfigurationSnapshotId New() => new(Guid.CreateVersion7());

    /// <summary>Ensures the snapshot is usable as provenance.</summary>
    public void EnsureAssigned()
    {
        if (Value == Guid.Empty)
        {
            throw new InvalidOperationException("A configuration snapshot must have an identity.");
        }
    }
}
