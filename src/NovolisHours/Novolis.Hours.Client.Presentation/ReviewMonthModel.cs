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
        EmployeePrimaryAction = HasSubmitStage ? "Submit" : "Acknowledge";
        EmployeePrimaryLabel = "Hand month to HR";
        WorkplaceName = days.Count > 0 && !string.IsNullOrWhiteSpace(days[0].OrganisationName)
            ? days[0].OrganisationName
            : review.EmployeeId;
        UnrecordedWorkingDays = days.Count(day => day.IsWorkingDay && day.Registration is null);
        ChangedDays = days.Count(day =>
            day.Registration?.Intent == WorkRegistrationIntent.ManualRegistration);
        GapLabel = WeekStudioModel.GapSentence(UnrecordedWorkingDays);
        ChangedLabel = WeekStudioModel.ChangedSentence(ChangedDays);
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

    /// <summary>Working days with no registration.</summary>
    public int UnrecordedWorkingDays { get; }

    /// <summary>Days recorded with typed times instead of the usual clock.</summary>
    public int ChangedDays { get; }

    /// <summary>One sentence HR and the clerk can read about gaps.</summary>
    public string GapLabel { get; }

    /// <summary>One sentence about days that were not the usual clock.</summary>
    public string ChangedLabel { get; }

    /// <summary>Who this stage is waiting on, without a policy id.</summary>
    public static string StageActorLabel(ReviewStageResponse stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return stage.RequiredAction switch
        {
            "Submit" or "Acknowledge" => "You",
            "Approve" when stage.Role is "Manager" or "Higher" => "Manager",
            "Approve" => "HR",
            _ => string.IsNullOrWhiteSpace(stage.Role) ? "Review" : stage.Role,
        };
    }

    /// <summary>Whether the stage is finished.</summary>
    public static string StageStateLabel(ReviewStageResponse stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return stage.IsComplete ? "Done" : "Waiting";
    }
}
