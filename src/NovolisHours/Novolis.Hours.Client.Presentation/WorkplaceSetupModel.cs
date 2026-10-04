using System.Collections.Immutable;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Obsolete walkthrough retained so older tests compile against the commercial open form.</summary>
public sealed class WorkplaceSetupModel
{
    /// <summary>Builds from leftover templates. Prefer <see cref="CustomerOpenModel"/>.</summary>
    public WorkplaceSetupModel(IReadOnlyList<HoursWorkplaceTemplateResponse> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        Templates = templates.ToImmutableArray();
        SelectedTemplateId = Templates.Length > 0 ? Templates[0].Id : string.Empty;
    }

    /// <summary>Legacy template cards. Not shown on Setup.</summary>
    public ImmutableArray<HoursWorkplaceTemplateResponse> Templates { get; }

    /// <summary>Unused step counter.</summary>
    public int Step { get; set; } = 1;

    /// <summary>Legacy template id.</summary>
    public string SelectedTemplateId { get; set; }

    /// <summary>Customer slug.</summary>
    public string OrganisationId { get; set; } = string.Empty;

    /// <summary>Customer name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>People added during a leftover walkthrough.</summary>
    public List<HoursUserResponse> People { get; } = [];

    /// <summary>Created customer.</summary>
    public HoursCustomerResponse? Created { get; set; }

    /// <summary>Selected template, when known.</summary>
    public HoursWorkplaceTemplateResponse? SelectedTemplate =>
        Templates.FirstOrDefault(item =>
            item.Id.Equals(SelectedTemplateId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the leftover Game template is attendance-only.</summary>
    public bool AttendanceConfirmationOnly =>
        SelectedTemplate?.Prototype.AttendanceConfirmationOnly == true ||
        Created?.AttendanceConfirmationOnly == true;

    /// <summary>Commit verb employees see.</summary>
    public string CommitPreview => "Worked as planned";

    /// <summary>Who the customer administrator should hire next.</summary>
    public string PeopleHint =>
        "Add employees after the administrator sets the rules. The administrator may also be HR.";

    /// <summary>Whether a leftover name step can continue.</summary>
    public bool CanNameWorkplace =>
        !string.IsNullOrWhiteSpace(OrganisationId) &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        !string.IsNullOrWhiteSpace(SelectedTemplateId);
}
