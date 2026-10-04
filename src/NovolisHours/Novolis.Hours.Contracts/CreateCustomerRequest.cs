namespace Novolis.Hours.Contracts;

/// <summary>Platform request to open a customer. Prefer <see cref="LocationId"/>; <see cref="TemplateId"/> remains for tests.</summary>
public sealed record CreateCustomerRequest(
    string OrganisationId,
    string DisplayName,
    string? TemplateId,
    string? LocationId = null);
