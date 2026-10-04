using System.Collections.Immutable;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Customer administrator form for week, flex, dispute, and month close.</summary>
public sealed class CustomerRulesModel
{
    /// <summary>Loads rules from the signed-in administrator's customer.</summary>
    public CustomerRulesModel(
        HoursCustomerResponse customer,
        IReadOnlyList<ReviewPolicyCatalogItem> policies)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(policies);
        Customer = customer;
        Policies = policies.ToImmutableArray();
        SaturdayIsWorkingDay = customer.SaturdayIsWorkingDay;
        AllowsFlex = customer.AllowsFlex;
        AllowsDispute = customer.AllowsDispute;
        ReviewPolicyId = string.IsNullOrWhiteSpace(customer.ReviewPolicyId)
            ? Policies.Length > 0 ? Policies[0].Id : string.Empty
            : customer.ReviewPolicyId;
    }

    /// <summary>Customer these rules belong to.</summary>
    public HoursCustomerResponse Customer { get; }

    /// <summary>Host review policies.</summary>
    public ImmutableArray<ReviewPolicyCatalogItem> Policies { get; }

    /// <summary>Saturday is a working day.</summary>
    public bool SaturdayIsWorkingDay { get; set; }

    /// <summary>Employees may keep flex.</summary>
    public bool AllowsFlex { get; set; }

    /// <summary>Employees may dispute a month.</summary>
    public bool AllowsDispute { get; set; }

    /// <summary>How a month is closed.</summary>
    public string ReviewPolicyId { get; set; }

    /// <summary>Whether the form can be saved.</summary>
    public bool CanSave => !string.IsNullOrWhiteSpace(ReviewPolicyId);

    /// <summary>Wire request for the current form.</summary>
    public UpdateCustomerRulesRequest ToRequest() =>
        new(SaturdayIsWorkingDay, AllowsFlex, AllowsDispute, ReviewPolicyId.Trim());
}
