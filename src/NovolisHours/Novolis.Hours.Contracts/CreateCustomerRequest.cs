namespace Novolis.Hours.Contracts;

/// <summary>Administrator workplace-creation request.</summary>
public sealed record CreateCustomerRequest(
    string OrganisationId,
    string DisplayName,
    string TemplateId);
