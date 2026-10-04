using System.Collections.Immutable;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Four-step administrator walkthrough: pattern, name, people, done.</summary>
public sealed class WorkplaceSetupModel
{
    /// <summary>Builds the walkthrough from host templates.</summary>
    public WorkplaceSetupModel(IReadOnlyList<HoursWorkplaceTemplateResponse> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        Templates = templates.ToImmutableArray();
        SelectedTemplateId = Templates.Length > 0 ? Templates[0].Id : string.Empty;
    }

    /// <summary>Nordvik office and Game shop cards.</summary>
    public ImmutableArray<HoursWorkplaceTemplateResponse> Templates { get; }

    /// <summary>1 pattern, 2 workplace, 3 people, 4 done.</summary>
    public int Step { get; set; } = 1;

    /// <summary>Selected template id.</summary>
    public string SelectedTemplateId { get; set; }

    /// <summary>New workplace slug.</summary>
    public string OrganisationId { get; set; } = string.Empty;

    /// <summary>New workplace display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>People added during this walkthrough.</summary>
    public List<HoursUserResponse> People { get; } = [];

    /// <summary>Created workplace after step 2 succeeds.</summary>
    public HoursCustomerResponse? Created { get; set; }

    /// <summary>Selected template, when known.</summary>
    public HoursWorkplaceTemplateResponse? SelectedTemplate =>
        Templates.FirstOrDefault(item =>
            item.Id.Equals(SelectedTemplateId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the pattern is attendance-only Game shop hours.</summary>
    public bool AttendanceConfirmationOnly =>
        SelectedTemplate?.Prototype.AttendanceConfirmationOnly == true ||
        Created?.AttendanceConfirmationOnly == true;

    /// <summary>Commit verb the new people will see.</summary>
    public string CommitPreview =>
        AttendanceConfirmationOnly ? "I was here" : "Worked as scheduled";

    /// <summary>Who to add next.</summary>
    public string PeopleHint =>
        AttendanceConfirmationOnly
            ? "Add a shop clerk and HR. There is no store-manager approve step."
            : "Add an employee and a manager. Flex and paint stay on.";

    /// <summary>Whether step 2 can continue.</summary>
    public bool CanNameWorkplace =>
        !string.IsNullOrWhiteSpace(OrganisationId) &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        !string.IsNullOrWhiteSpace(SelectedTemplateId);
}
