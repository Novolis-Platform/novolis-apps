namespace Novolis.Hours.Contracts;

/// <summary>Wire-level intent for an immutable work assertion.</summary>
public enum WorkRegistrationIntent
{
    WorkedAsScheduled,
    ManualRegistration,
    Correction,
}
