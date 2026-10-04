namespace Novolis.Hours.Application;

/// <summary>Administrator starting pattern for a new workplace.</summary>
public sealed record HoursWorkplaceTemplate(
    string Id,
    string Title,
    string Summary,
    HoursCustomer Prototype);
