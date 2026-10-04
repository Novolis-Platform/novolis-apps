namespace Novolis.Hours.Contracts;

/// <summary>Client-side mix of Hours roles for one signed-in person.</summary>
public static class HoursClientRoleSet
{
    /// <summary>Primary role first, then extras, without duplicates.</summary>
    public static IReadOnlyList<HoursClientRole> Combine(
        HoursClientRole primary,
        IEnumerable<HoursClientRole>? extra)
    {
        var roles = new List<HoursClientRole> { primary };
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
    public static bool Has(IEnumerable<HoursClientRole>? roles, HoursClientRole role) =>
        roles is not null && roles.Contains(role);

    /// <summary>Short labels a clerk can read, joined with a middle dot.</summary>
    public static string Label(IEnumerable<HoursClientRole> roles) =>
        string.Join(" · ", roles.Select(Label));

    /// <summary>Short label for one role.</summary>
    public static string Label(HoursClientRole role) =>
        role switch
        {
            HoursClientRole.HumanResources => "HR",
            HoursClientRole.Administrator => "Admin",
            _ => role.ToString(),
        };
}
