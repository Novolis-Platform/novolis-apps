namespace Novolis.Hours.Domain.Configuration;

/// <summary>Origin category for a rule retained in product provenance.</summary>
public enum RuleSource
{
    /// <summary>Tenant or user authored rule.</summary>
    Manual,

    /// <summary>Novolis built-in rule.</summary>
    BuiltIn,

    /// <summary>Rule generated from a source data package.</summary>
    Generated,

    /// <summary>Rule supplied by a versioned source package.</summary>
    SourcePackage,
}
