namespace Novolis.Hours.Contracts;

/// <summary>Customer administrator changes to workplace rules. Location and calendar stay as provisioned.</summary>
public sealed record UpdateCustomerRulesRequest(
    bool SaturdayIsWorkingDay,
    bool AllowsFlex,
    bool AllowsDispute,
    string ReviewPolicyId);
