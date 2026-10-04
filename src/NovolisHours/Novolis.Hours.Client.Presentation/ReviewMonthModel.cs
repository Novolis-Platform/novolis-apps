using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Month review numbers derived from already-explained days.</summary>
public sealed class ReviewMonthModel
{
    /// <summary>Creates a month summary from a review projection and the days in range.</summary>
    public ReviewMonthModel(ReviewProjectionResponse review, IReadOnlyList<WorkDayResponse> days)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(days);
        Review = review;
        Days = days;
        ExpectedWork = days.Aggregate(TimeSpan.Zero, (total, day) => total + day.ExpectedWork);
        ActualWork = days.Aggregate(TimeSpan.Zero, (total, day) => total + day.ActualWorked);
        AllowsDispute = review.AllowsDispute;
        AttendanceConfirmationOnly = review.AttendanceConfirmationOnly;
        HasSubmitStage = review.Stages.Any(stage =>
            string.Equals(stage.RequiredAction, "Submit", StringComparison.Ordinal));
        HasAcknowledgeStage = review.Stages.Any(stage =>
            string.Equals(stage.RequiredAction, "Acknowledge", StringComparison.Ordinal));
        HasApproveStage = review.Stages.Any(stage =>
            string.Equals(stage.RequiredAction, "Approve", StringComparison.Ordinal));
        EmployeePrimaryAction = AttendanceConfirmationOnly || !HasSubmitStage
            ? "Acknowledge"
            : "Submit";
        EmployeePrimaryLabel = AttendanceConfirmationOnly
            ? "Confirm attendance"
            : HasSubmitStage
                ? "Submit"
                : "Acknowledge";
        WorkplaceName = days.Count > 0 && !string.IsNullOrWhiteSpace(days[0].OrganisationName)
            ? days[0].OrganisationName
            : review.EmployeeId;
    }

    /// <summary>Server review projection.</summary>
    public ReviewProjectionResponse Review { get; }

    /// <summary>WorkDays covering the period.</summary>
    public IReadOnlyList<WorkDayResponse> Days { get; }

    /// <summary>Sum of expected work.</summary>
    public TimeSpan ExpectedWork { get; }

    /// <summary>Sum of recorded actual work.</summary>
    public TimeSpan ActualWork { get; }

    /// <summary>Whether this workplace offers a dispute action.</summary>
    public bool AllowsDispute { get; }

    /// <summary>Whether the employee confirms attendance instead of submitting a timesheet.</summary>
    public bool AttendanceConfirmationOnly { get; }

    /// <summary>Whether the policy includes an employee submit stage.</summary>
    public bool HasSubmitStage { get; }

    /// <summary>Whether the policy includes an employee acknowledge stage.</summary>
    public bool HasAcknowledgeStage { get; }

    /// <summary>Whether a reviewer Approve stage exists.</summary>
    public bool HasApproveStage { get; }

    /// <summary>API action kind for the employee primary button.</summary>
    public string EmployeePrimaryAction { get; }

    /// <summary>Visible label for the employee primary button.</summary>
    public string EmployeePrimaryLabel { get; }

    /// <summary>Workplace name shown above the period.</summary>
    public string WorkplaceName { get; }
}
