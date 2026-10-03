namespace Novolis.Hours.Client;

/// <summary>Small read projection suitable for native-client summaries.</summary>
public sealed record HoursEmployeeSummary(
    string EmployeeId,
    TimeSpan FlexSaldo,
    int PresenceRecordCount,
    int AdjustmentCount,
    int AnomalyCount);
