namespace Novolis.Hours.Domain;

/// <summary>One person may hold several Hours roles. The journal still stamps a primary role.</summary>
public static class HoursRoleSet
{
    /// <summary>Primary role first, then extras, without duplicates.</summary>
    public static IReadOnlyList<HoursActorRole> Combine(
        HoursActorRole primary,
        IEnumerable<HoursActorRole>? extra)
    {
        var roles = new List<HoursActorRole> { primary };
        if (extra is null)
        {
            return roles;
        }

        foreach (var role in extra)
        {
            if (!roles.Contains(role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    /// <summary>Whether the assigned set includes <paramref name="role"/>.</summary>
    public static bool Has(IEnumerable<HoursActorRole>? roles, HoursActorRole role) =>
        roles is not null && roles.Contains(role);
}
