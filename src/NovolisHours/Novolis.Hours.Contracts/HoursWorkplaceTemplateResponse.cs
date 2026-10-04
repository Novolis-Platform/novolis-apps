namespace Novolis.Hours.Contracts;

/// <summary>Setup pattern shown on the administrator walkthrough.</summary>
public sealed record HoursWorkplaceTemplateResponse(
    string Id,
    string Title,
    string Summary,
    HoursCustomerResponse Prototype);
