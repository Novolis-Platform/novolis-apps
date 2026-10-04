using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Review;
using Novolis.Hours.Storage;

namespace Novolis.Hours.Application;

/// <summary>
/// Built-in workplaces plus administrator-created extras. Employee lookup prefers
/// the persisted organisation id when it names a known workplace.
/// </summary>
public sealed class HoursWorkplaceDirectory
{
    private readonly ConcurrentDictionary<string, HoursCustomer> extras =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HoursUserDirectory users;
    private readonly IHoursCustomerStore store;

    /// <summary>Loads persisted workplaces next to the built-in catalog.</summary>
    public HoursWorkplaceDirectory(HoursUserDirectory users, IHoursCustomerStore store)
    {
        this.users = users ?? throw new ArgumentNullException(nameof(users));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        foreach (var document in store.List())
        {
            extras[document.OrganisationId] = ToCustomer(document);
        }
    }

    /// <summary>Built-in and administrator-created workplaces.</summary>
    public IReadOnlyList<HoursCustomer> List()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<HoursCustomer>();
        foreach (var customer in extras.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(customer.Id))
            {
                list.Add(customer);
            }
        }

        foreach (var customer in HoursCustomerCatalog.All)
        {
            if (seen.Add(customer.Id))
            {
                list.Add(customer);
            }
        }

        return list;
    }

    /// <summary>Resolves the workplace for one employment.</summary>
    public HoursCustomer ForEmployee(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var user = users.FindByEmployeeId(employeeId);
        if (user is not null && TryGet(user.OrganisationId, out var byOrganisation))
        {
            return byOrganisation;
        }

        return HoursCustomerCatalog.ForEmployee(employeeId);
    }

    /// <summary>Looks up a built-in or persisted workplace.</summary>
    public bool TryGet(string organisationId, out HoursCustomer customer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationId);
        if (extras.TryGetValue(organisationId, out customer!))
        {
            return true;
        }

        return HoursCustomerCatalog.TryGet(organisationId, out customer);
    }

    /// <summary>Time zone for chrome and snapshots. Admin and system stay on UTC.</summary>
    public string TimeZoneId(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return employeeId.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            employeeId.Equals("system", StringComparison.OrdinalIgnoreCase)
            ? "UTC"
            : ForEmployee(employeeId).TimeZoneId;
    }

    /// <summary>Calendar stack for the resolved workplace.</summary>
    public WorkCalendarStack GetCalendar(
        string employeeId,
        DateOnly date,
        string? calendarVersionOverride = null) =>
        HoursCustomerCatalog.GetCalendar(
            ForEmployee(employeeId),
            employeeId,
            date,
            calendarVersionOverride,
            TimeZoneId(employeeId));

    /// <summary>Review policy stored on the employee's workplace.</summary>
    public ReviewPolicy GetReviewPolicy(string employeeId) =>
        HoursReviewWorkflowCatalog.ForCustomer(ForEmployee(employeeId));

    /// <summary>Registers a new workplace. Built-in ids are reserved.</summary>
    public async Task<HoursCustomer> RegisterAsync(
        HoursCustomer customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (HoursCustomerCatalog.TryGet(customer.Id, out _))
        {
            throw new InvalidOperationException(
                $"Workplace '{customer.Id}' is a built-in catalog identity and cannot be replaced.");
        }

        if (extras.ContainsKey(customer.Id))
        {
            throw new InvalidOperationException($"Workplace '{customer.Id}' already exists.");
        }

        await store.SaveAsync(FromCustomer(customer), cancellationToken);
        extras[customer.Id] = customer;
        return customer;
    }

    private static HoursCustomerDocument FromCustomer(HoursCustomer customer) =>
        new(
            IdFor(customer.Id),
            customer.Id,
            customer.DisplayName,
            customer.CountryCode,
            customer.TimeZoneId,
            customer.LegalPresetId,
            customer.ReviewPolicyId,
            customer.ExpectedWork.ToString("c", CultureInfo.InvariantCulture),
            FormatTime(customer.Envelope.Start),
            FormatTime(customer.Envelope.End),
            FormatTime(customer.CoreHours.Start),
            FormatTime(customer.CoreHours.End),
            FormatTime(customer.RoutineMorning.Start),
            FormatTime(customer.RoutineMorning.End),
            FormatTime(customer.RoutineAfternoon.Start),
            FormatTime(customer.RoutineAfternoon.End),
            customer.ObservesPublicHolidays,
            customer.SevenDayOperation,
            customer.SaturdayIsWorkingDay,
            customer.AllowsFlex,
            customer.AllowsDispute,
            customer.AttendanceConfirmationOnly);

    private static HoursCustomer ToCustomer(HoursCustomerDocument document) =>
        new(
            document.OrganisationId,
            document.CountryCode,
            document.TimeZoneId,
            document.LegalPresetId,
            TimeSpan.Parse(document.ExpectedWork, CultureInfo.InvariantCulture),
            new LocalTimeRange(ParseTime(document.EnvelopeStart), ParseTime(document.EnvelopeEnd)),
            new LocalTimeRange(ParseTime(document.CoreHoursStart), ParseTime(document.CoreHoursEnd)),
            new LocalTimeRange(ParseTime(document.RoutineMorningStart), ParseTime(document.RoutineMorningEnd)),
            new LocalTimeRange(ParseTime(document.RoutineAfternoonStart), ParseTime(document.RoutineAfternoonEnd)),
            document.ObservesPublicHolidays,
            document.SevenDayOperation,
            document.SaturdayIsWorkingDay,
            document.AllowsFlex,
            document.AllowsDispute,
            document.AttendanceConfirmationOnly,
            document.DisplayName,
            document.ReviewPolicyId);

    private static Guid IdFor(string organisationId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("hours-customer|" + organisationId.Trim().ToLowerInvariant()));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string FormatTime(TimeOnly value) => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static TimeOnly ParseTime(string value) => TimeOnly.Parse(value, CultureInfo.InvariantCulture);
}
