using Novolis.Hours.Domain.Review;

namespace Novolis.Hours.Application;

/// <summary>Per-customer review policies. One host, six workplaces.</summary>
public static class HoursReviewWorkflowCatalog
{
    /// <summary>Ada's office: submit, two manager levels, then HR.</summary>
    public const string CascadingApproval = "cascading-approval";

    /// <summary>Bob's station: employer records time, employee acknowledges.</summary>
    public const string EmployerAcknowledged = "employer-acknowledged";

    /// <summary>Pierre's atelier: employee submits, one manager approves.</summary>
    public const string SingleApprover = "single-approver";

    /// <summary>Anna's settlement: employer records and closes the period.</summary>
    public const string EmployerOnly = "employer-only";

    /// <summary>Liisa's flex shop: employee submits, HR closes.</summary>
    public const string EmployeeHr = "employee-hr";

    /// <summary>Game retail: employee confirms attendance, HR closes. No dispute.</summary>
    public const string AttendanceHr = "attendance-hr";

    /// <summary>Every review policy an administrator can assign to a workplace.</summary>
    public static IReadOnlyList<ReviewPolicy> All =>
    [
        CascadingApprovalPolicy,
        EmployerAcknowledgedPolicy,
        SingleApproverPolicy,
        EmployerOnlyPolicy,
        EmployeeHrPolicy,
        AttendanceHrPolicy,
    ];

    /// <summary>Resolves the review policy that applies to an acceptance identity.</summary>
    public static ReviewPolicy ForEmployee(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return ForOrganisation(HoursCustomerCatalog.OrganisationId(employeeId));
    }

    /// <summary>Resolves the review policy stored on a workplace, or the built-in organisation default.</summary>
    public static ReviewPolicy ForCustomer(HoursCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return TryGet(customer.ReviewPolicyId, out var policy)
            ? policy
            : ForOrganisation(customer.Id);
    }

    /// <summary>Looks up a review policy by id.</summary>
    public static bool TryGet(string? policyId, out ReviewPolicy policy)
    {
        policy = All.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(policyId) &&
            item.Id.Equals(policyId, StringComparison.OrdinalIgnoreCase))!;
        return policy is not null;
    }

    /// <summary>Snapshot workflow version captured with a WorkDay.</summary>
    public static string WorkflowVersion(string employeeId) =>
        WorkflowVersion(ForEmployee(employeeId));

    /// <summary>Snapshot workflow version for an already resolved policy.</summary>
    public static string WorkflowVersion(ReviewPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return $"{policy.Id}.{policy.Version}";
    }

    private static ReviewPolicy ForOrganisation(string organisationId) =>
        organisationId switch
        {
            HoursCustomerCatalog.NordvikStation => EmployerAcknowledgedPolicy,
            HoursCustomerCatalog.AtelierCurie => SingleApproverPolicy,
            HoursCustomerCatalog.WarsawSettlement => EmployerOnlyPolicy,
            HoursCustomerCatalog.HelsinkiFlex => EmployeeHrPolicy,
            HoursCustomerCatalog.GameRetail => AttendanceHrPolicy,
            HoursCustomerCatalog.PacificYard => AttendanceHrPolicy,
            HoursCustomerCatalog.TorontoYard => EmployeeHrPolicy,
            HoursCustomerCatalog.TokyoFlex => EmployeeHrPolicy,
            HoursCustomerCatalog.BerlinOffice => EmployeeHrPolicy,
            _ => CascadingApprovalPolicy,
        };

    private static ReviewPolicy CascadingApprovalPolicy { get; } = new(
        CascadingApproval,
        "1",
        [
            new ReviewStage("employee-submit", ReviewActionKind.Submit, ResponsibilityRole.Employee, null, 2),
            new ReviewStage("manager-level-1", ReviewActionKind.Approve, ResponsibilityRole.Manager, 1, 2),
            new ReviewStage("manager-level-2", ReviewActionKind.Approve, ResponsibilityRole.Manager, 2, 2),
            new ReviewStage("hr-final", ReviewActionKind.Approve, ResponsibilityRole.HumanResources, null, 2),
        ]);

    private static ReviewPolicy EmployerAcknowledgedPolicy { get; } = new(
        EmployerAcknowledged,
        "1",
        [
            new ReviewStage("employee-acknowledge", ReviewActionKind.Acknowledge, ResponsibilityRole.Employee, null, 2),
        ]);

    private static ReviewPolicy SingleApproverPolicy { get; } = new(
        SingleApprover,
        "1",
        [
            new ReviewStage("employee-submit", ReviewActionKind.Submit, ResponsibilityRole.Employee, null, 2),
            new ReviewStage("manager-approve", ReviewActionKind.Approve, ResponsibilityRole.Manager, null, 2),
        ]);

    private static ReviewPolicy EmployerOnlyPolicy { get; } = new(
        EmployerOnly,
        "1",
        [
            new ReviewStage("employer-approve", ReviewActionKind.Approve, ResponsibilityRole.Manager, null, 2),
        ]);

    private static ReviewPolicy EmployeeHrPolicy { get; } = new(
        EmployeeHr,
        "1",
        [
            new ReviewStage("employee-submit", ReviewActionKind.Submit, ResponsibilityRole.Employee, null, 2),
            new ReviewStage("hr-close", ReviewActionKind.Approve, ResponsibilityRole.HumanResources, null, 2),
        ]);

    private static ReviewPolicy AttendanceHrPolicy { get; } = new(
        AttendanceHr,
        "1",
        [
            new ReviewStage("employee-confirm", ReviewActionKind.Acknowledge, ResponsibilityRole.Employee, null, 2),
            new ReviewStage("hr-attendance", ReviewActionKind.Approve, ResponsibilityRole.HumanResources, null, 2),
        ],
        allowsDispute: false);
}
