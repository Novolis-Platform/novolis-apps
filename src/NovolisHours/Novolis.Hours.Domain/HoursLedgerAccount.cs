namespace Novolis.Hours.Domain;

/// <summary>Two universal duration accounts used to prove every worktime movement balances.</summary>
public enum HoursLedgerAccount
{
    /// <summary>The employee's recorded flex-time saldo.</summary>
    EmployeeFlexSaldo,

    /// <summary>The matching organisational control side; it has no payroll meaning.</summary>
    OrganisationControl,
}
