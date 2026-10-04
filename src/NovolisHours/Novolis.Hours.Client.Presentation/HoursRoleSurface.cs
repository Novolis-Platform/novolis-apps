using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Maps the assigned role set to chrome and home. Mixed roles are a union, not a single door.</summary>
public static class HoursRoleSurface
{
    /// <summary>Home after sign-in for this role set. A timesheet wins when the person also records hours.</summary>
    public static HoursSurfaceKind Home(HoursClientRole role) =>
        Home(HoursClientRoleSet.Combine(role, null));

    /// <summary>Home after sign-in for every assigned role.</summary>
    public static HoursSurfaceKind Home(IEnumerable<HoursClientRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var assigned = roles.ToArray();
        if (HoursClientRoleSet.Has(assigned, HoursClientRole.System))
        {
            return HoursSurfaceKind.Backoffice;
        }

        if (ShowsTimesheet(assigned))
        {
            return HoursSurfaceKind.Week;
        }

        if (HoursClientRoleSet.Has(assigned, HoursClientRole.Administrator))
        {
            return HoursSurfaceKind.Workplace;
        }

        if (HoursClientRoleSet.Has(assigned, HoursClientRole.HumanResources) ||
            HoursClientRoleSet.Has(assigned, HoursClientRole.Manager) ||
            HoursClientRoleSet.Has(assigned, HoursClientRole.Higher))
        {
            return HoursSurfaceKind.Reviews;
        }

        if (HoursClientRoleSet.Has(assigned, HoursClientRole.Auditor))
        {
            return HoursSurfaceKind.Audit;
        }

        return HoursSurfaceKind.Week;
    }

    /// <summary>Whether this role has a personal week and WorkDay.</summary>
    public static bool ShowsTimesheet(HoursClientRole role) =>
        ShowsTimesheet(HoursClientRoleSet.Combine(role, null));

    /// <summary>Whether any assigned role records hours. Managers who are also employees keep a week.</summary>
    public static bool ShowsTimesheet(IEnumerable<HoursClientRole> roles) =>
        HoursClientRoleSet.Has(roles, HoursClientRole.Employee);

    /// <summary>Whether Workplace, People, and Rules belong in the chrome.</summary>
    public static bool ShowsWorkplace(IEnumerable<HoursClientRole> roles) =>
        HoursClientRoleSet.Has(roles, HoursClientRole.Administrator);

    /// <summary>Whether Reviews belong in the chrome.</summary>
    public static bool ShowsReviews(IEnumerable<HoursClientRole> roles) =>
        HoursClientRoleSet.Has(roles, HoursClientRole.HumanResources) ||
        HoursClientRoleSet.Has(roles, HoursClientRole.Manager) ||
        HoursClientRoleSet.Has(roles, HoursClientRole.Higher) ||
        HoursClientRoleSet.Has(roles, HoursClientRole.Administrator);

    /// <summary>Whether Audit belongs in the chrome.</summary>
    public static bool ShowsAudit(IEnumerable<HoursClientRole> roles) =>
        HoursClientRoleSet.Has(roles, HoursClientRole.Auditor);

    /// <summary>Route the door should send this role to after sign-in.</summary>
    public static string HomePath(HoursClientRole role) =>
        HomePath(HoursClientRoleSet.Combine(role, null));

    /// <summary>Route the door should send this role set to after sign-in.</summary>
    public static string HomePath(IEnumerable<HoursClientRole> roles) =>
        Home(roles) switch
        {
            HoursSurfaceKind.Reviews => "/reviews",
            HoursSurfaceKind.Audit => "/audit",
            HoursSurfaceKind.Workplace => "/workplace",
            _ => "/",
        };
}
