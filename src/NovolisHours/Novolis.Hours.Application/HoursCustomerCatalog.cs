using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Time.Week;

namespace Novolis.Hours.Application;

/// <summary>Named customer calendar stacks used by the Hours acceptance regime.</summary>
public static class HoursCustomerCatalog
{
    /// <summary>Norwegian private 37.5-hour office flex customer.</summary>
    public const string NordvikOffice = "nordvik-office";

    /// <summary>Norwegian 24/7 service customer using the state-handbook preset.</summary>
    public const string NordvikStation = "nordvik-station";

    /// <summary>French annualisation customer.</summary>
    public const string AtelierCurie = "atelier-curie";

    /// <summary>Polish monthly settlement customer.</summary>
    public const string WarsawSettlement = "warsaw-settlement";

    /// <summary>Finnish flexible-work customer.</summary>
    public const string HelsinkiFlex = "helsinki-flex";

    /// <summary>British high-street Game retail: rigid shop hours, attendance only.</summary>
    public const string GameRetail = "game-retail";

    /// <summary>California warehouse on FLSA weekly overtime and daily California overtime.</summary>
    public const string PacificYard = "pacific-yard";

    /// <summary>Ontario yard on the ESA 44-hour overtime threshold.</summary>
    public const string TorontoYard = "toronto-yard";

    /// <summary>Tokyo flextime shop that needs an Article 36 overtime agreement.</summary>
    public const string TokyoFlex = "tokyo-flex";

    /// <summary>Berlin office on the Arbeitszeitgesetz 8-hour day.</summary>
    public const string BerlinOffice = "berlin-office";

    /// <summary>Cross-customer platform identity used by the System actor.</summary>
    public const string Platform = "hours-platform";

    /// <summary>Ada's one-day envelope override used by stacking tests.</summary>
    public static DateOnly AdaTemporaryOverrideDate { get; } = new(2026, 11, 18);

    /// <summary>Built-in workplaces used by the acceptance seed and as setup templates.</summary>
    public static IReadOnlyList<HoursCustomer> All =>
    [
        PlatformCustomer,
        NordvikOfficeCustomer,
        NordvikStationCustomer,
        AtelierCurieCustomer,
        WarsawSettlementCustomer,
        HelsinkiFlexCustomer,
        GameRetailCustomer,
        PacificYardCustomer,
        TorontoYardCustomer,
        TokyoFlexCustomer,
        BerlinOfficeCustomer,
    ];

    /// <summary>Gets the customer system that owns an acceptance identity.</summary>
    public static HoursCustomer ForEmployee(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return employeeId.ToLowerInvariant() switch
        {
            "system" => PlatformCustomer,
            "bob" or "nina" => NordvikStationCustomer,
            "pierre" or "marc" => AtelierCurieCustomer,
            "anna" or "kasia" => WarsawSettlementCustomer,
            "liisa" => HelsinkiFlexCustomer,
            "jamie" or "priya" => GameRetailCustomer,
            "jordan" or "pat" => PacificYardCustomer,
            "casey" => TorontoYardCustomer,
            "yuki" => TokyoFlexCustomer,
            "lena" => BerlinOfficeCustomer,
            _ => NordvikOfficeCustomer,
        };
    }

    /// <summary>Looks up a built-in workplace by organisation id.</summary>
    /// <param name="organisationId">Stable workplace id.</param>
    /// <param name="customer">The matching workplace when found.</param>
    /// <returns><see langword="true"/> when the id is a built-in workplace.</returns>
    public static bool TryGet(string organisationId, out HoursCustomer customer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationId);
        customer = All.FirstOrDefault(item =>
            item.Id.Equals(organisationId, StringComparison.OrdinalIgnoreCase))!;
        return customer is not null;
    }

    /// <summary>Gets the organisation identifier used by an acceptance identity.</summary>
    public static string OrganisationId(string employeeId) => ForEmployee(employeeId).Id;

    /// <summary>Gets the local time-zone identifier used by an acceptance identity.</summary>
    public static string TimeZoneId(string employeeId) =>
        employeeId.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
        employeeId.Equals("system", StringComparison.OrdinalIgnoreCase)
            ? "UTC"
            : ForEmployee(employeeId).TimeZoneId;

    /// <summary>Gets the ordered calendar stack for one employee and local date.</summary>
    public static WorkCalendarStack GetCalendar(
        string employeeId,
        DateOnly date,
        string? calendarVersionOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return GetCalendar(
            ForEmployee(employeeId),
            employeeId,
            date,
            calendarVersionOverride,
            TimeZoneId(employeeId));
    }

    /// <summary>Builds the calendar stack for an already resolved workplace.</summary>
    public static WorkCalendarStack GetCalendar(
        HoursCustomer customer,
        string employeeId,
        DateOnly date,
        string? calendarVersionOverride = null,
        string? timeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var version = calendarVersionOverride ?? GetCalendarVersion(employeeId, date);
        var zone = timeZoneId ?? customer.TimeZoneId;
        if (customer.Id == Platform)
        {
            return new WorkCalendarStack(
                [
                    new WorkCalendarLayer(
                        "hours-platform.system",
                        version,
                        CalendarLayerOrders.Organisation,
                        [
                            new EveryDateCalendarRule(
                                "hours-platform.no-expected-work",
                                [
                                    new WorkingDayRule(false),
                                    new ExpectedWorkRule(TimeSpan.Zero),
                                ]),
                        ],
                        RuleSource.Manual,
                        kind: CalendarLayerKind.Organisation),
                ],
                zone);
        }

        var layers = CreateLayers(customer, employeeId, version);
        return new WorkCalendarStack(layers, zone);
    }

    /// <summary>Gets the effective calendar version label for one employee and date.</summary>
    public static string GetCalendarVersion(string employeeId, DateOnly date) =>
        employeeId.Equals("pierre", StringComparison.OrdinalIgnoreCase) &&
        date >= new DateOnly(2026, 5, 15)
            ? "agreement-v2"
            : employeeId.Equals("pierre", StringComparison.OrdinalIgnoreCase)
                ? "agreement-v1"
                : "acceptance-calendar-v1";

    private static IReadOnlyList<WorkCalendarLayer> CreateLayers(
        HoursCustomer customer,
        string employeeId,
        string version)
    {
        var layers = new List<WorkCalendarLayer>
        {
            NationalCalendarFactory.CreateNationalLayer(customer.CountryCode, version),
        };

        if (customer.ObservesPublicHolidays)
        {
            layers.Add(CreateOfficeOrganisation(customer, version));
        }
        else
        {
            layers.Add(CreateStationOrganisation(customer, version));
        }

        layers.AddRange(CreateAgreementLayers(customer, version));
        layers.AddRange(CreateEmployeeScheduleLayers(customer, employeeId, version));

        if (employeeId.Equals("ada", StringComparison.OrdinalIgnoreCase))
        {
            layers.Add(CreateAdaTemporaryOverride(version));
        }

        return layers;
    }

    private static WorkCalendarLayer CreateOfficeOrganisation(HoursCustomer customer, string version) =>
        new(
            $"{customer.Id}.organisation",
            version,
            CalendarLayerOrders.Organisation,
            [
                new PublicHolidayObservanceCalendarRule(
                    $"{customer.Id}.observe-public-holidays",
                    NationalHolidayCatalog.For(customer.CountryCode),
                    customer.ExpectedWork),
                new EveryDateCalendarRule(
                    $"{customer.Id}.organisation.shape",
                    [
                        new WorkEnvelopeRule(customer.Envelope),
                        new CoreHoursRule([customer.CoreHours]),
                    ]),
                .. CreateCorporatePaidDays(customer),
            ],
            RuleSource.Manual,
            kind: CalendarLayerKind.Organisation);

    private static WorkCalendarLayer CreateStationOrganisation(HoursCustomer customer, string version) =>
        new(
            $"{customer.Id}.organisation",
            version,
            CalendarLayerOrders.Organisation,
            [
                new EveryDateCalendarRule(
                    $"{customer.Id}.always-open",
                    [
                        new WorkingDayRule(true),
                        new WorkEnvelopeRule(customer.Envelope),
                        new CoreHoursRule([customer.CoreHours]),
                    ]),
            ],
            RuleSource.Manual,
            kind: CalendarLayerKind.Organisation);

    private static IEnumerable<IWorkCalendarRule> CreateCorporatePaidDays(HoursCustomer customer)
    {
        if (customer.Id != NordvikOffice)
        {
            return [];
        }

        return
        [
            new FixedDateCalendarRule(
                "corporate-christmas-eve",
                12,
                24,
                [
                    new WorkingDayRule(false),
                    new ExpectedWorkRule(TimeSpan.Zero),
                    new PaidEntitlementRule(TimeSpan.FromHours(7.5)),
                    new DayTagRule("PaidEntitlement", "Corporate Christmas Eve"),
                ]),
        ];
    }

    private static IEnumerable<WorkCalendarLayer> CreateAgreementLayers(HoursCustomer customer, string version)
    {
        if (customer.Id == NordvikStation)
        {
            yield return new WorkCalendarLayer(
                $"{customer.Id}.agreement",
                version,
                CalendarLayerOrders.Agreement,
                [
                    new FixedDateCalendarRule(
                        "christmas-entitlement",
                        12,
                        25,
                        [new PaidEntitlementRule(TimeSpan.FromHours(7.5))]),
                ],
                RuleSource.Manual,
                kind: CalendarLayerKind.Agreement);
            yield break;
        }

        yield return new WorkCalendarLayer(
            $"{customer.Id}.agreement",
            version,
            CalendarLayerOrders.Agreement,
            [
                new EveryDateCalendarRule(
                    $"{customer.Id}.agreement.presence",
                    [new DayTagRule("Agreement", customer.LegalPresetId)]),
            ],
            RuleSource.Manual,
            kind: CalendarLayerKind.Agreement);
    }

    private static IEnumerable<WorkCalendarLayer> CreateEmployeeScheduleLayers(
        HoursCustomer customer,
        string employeeId,
        string version)
    {
        if (customer.Id == AtelierCurie)
        {
            yield return CreateEmployeeScheduleLayer(
                customer,
                employeeId,
                version,
                CreateWeekday(
                    TimeSpan.FromHours(7),
                    new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(17, 0)),
                    new LocalTimeRange(new TimeOnly(10, 0), new TimeOnly(16, 0)),
                    new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(12, 0)),
                    new LocalTimeRange(new TimeOnly(13, 0), new TimeOnly(17, 0))),
                effectiveTo: new DateOnly(2026, 5, 14));
            yield return CreateEmployeeScheduleLayer(
                customer,
                employeeId,
                version,
                CreateWeekday(
                    TimeSpan.FromHours(7),
                    new LocalTimeRange(new TimeOnly(8, 30), new TimeOnly(16, 30)),
                    new LocalTimeRange(new TimeOnly(9, 30), new TimeOnly(15, 30)),
                    new LocalTimeRange(new TimeOnly(8, 30), new TimeOnly(12, 0)),
                    new LocalTimeRange(new TimeOnly(13, 0), new TimeOnly(16, 30))),
                effectiveFrom: new DateOnly(2026, 5, 15));
            yield break;
        }

        yield return CreateEmployeeScheduleLayer(
            customer,
            employeeId,
            version,
            CreateWeekday(
                customer.ExpectedWork,
                customer.Envelope,
                customer.CoreHours,
                customer.RoutineMorning,
                customer.RoutineAfternoon));
    }

    private static WorkCalendarLayer CreateEmployeeScheduleLayer(
        HoursCustomer customer,
        string employeeId,
        string version,
        HoursWeekday weekday,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null)
    {
        var schedule = new EmployeeSchedule(
            employeeId,
            customer.TimeZoneId,
            new WeekBasedCalendar<HoursWeekday>(
                WeekModel.Iso,
                CreateWeekPattern(customer, weekday),
                effectiveFrom: effectiveFrom,
                effectiveTo: effectiveTo));
        IWorkCalendarRule rule = new WeekBasedScheduleRule($"{employeeId}.week", schedule.Calendar);
        if (customer.ObservesPublicHolidays)
        {
            var excluded = NationalHolidayCatalog.Dates(customer.CountryCode).ToList();
            if (customer.Id == NordvikOffice)
            {
                excluded.Add(new DateOnly(2025, 12, 24));
                excluded.Add(new DateOnly(2026, 12, 24));
                excluded.Add(new DateOnly(2027, 12, 24));
            }

            rule = new ExcludingDatesCalendarRule(
                $"{employeeId}.week",
                rule,
                excluded);
        }

        return new WorkCalendarLayer(
            $"{employeeId}.employee",
            version,
            CalendarLayerOrders.Employee,
            [rule],
            RuleSource.Manual,
            effectiveFrom,
            effectiveTo,
            CalendarLayerKind.Employee);
    }

    private static WeeklyPattern<HoursWeekday> CreateWeekPattern(HoursCustomer customer, HoursWeekday weekday)
    {
        var weekend = new HoursWeekday(
            false,
            TimeSpan.Zero,
            weekday.WorkEnvelope,
            weekday.CoreHours,
            weekday.RoutineWork);
        return new WeeklyPattern<HoursWeekday>(
            Enum.GetValues<DayOfWeek>().Select(day =>
            {
                var working = day switch
                {
                    DayOfWeek.Sunday => customer.SevenDayOperation,
                    DayOfWeek.Saturday => customer.SevenDayOperation || customer.SaturdayIsWorkingDay,
                    _ => true,
                };
                return new KeyValuePair<DayOfWeek, HoursWeekday>(day, working ? weekday : weekend);
            }));
    }

    private static HoursWeekday CreateWeekday(
        TimeSpan expectedWork,
        LocalTimeRange envelope,
        LocalTimeRange coreHours,
        LocalTimeRange morning,
        LocalTimeRange afternoon) =>
        new(
            true,
            expectedWork,
            envelope,
            [coreHours],
            morning.Start == afternoon.Start && morning.End == afternoon.End
                ? [morning]
                : [morning, afternoon]);

    private static WorkCalendarLayer CreateAdaTemporaryOverride(string version) =>
        new(
            "ada.temporary-override",
            version,
            CalendarLayerOrders.TemporaryOverride,
            [
                new DateRangeCalendarRule(
                    "ada-temporary-envelope",
                    AdaTemporaryOverrideDate,
                    AdaTemporaryOverrideDate,
                    [new WorkEnvelopeRule(new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(18, 0)))]),
            ],
            RuleSource.Manual,
            kind: CalendarLayerKind.TemporaryOverride);

    private static HoursCustomer PlatformCustomer { get; } = new(
        Platform,
        "XX",
        "UTC",
        "hours.platform",
        TimeSpan.Zero,
        new LocalTimeRange(new TimeOnly(0, 0), new TimeOnly(23, 59)),
        new LocalTimeRange(new TimeOnly(0, 0), new TimeOnly(23, 59)),
        new LocalTimeRange(new TimeOnly(0, 0), new TimeOnly(0, 1)),
        new LocalTimeRange(new TimeOnly(0, 0), new TimeOnly(0, 1)),
        ObservesPublicHolidays: false,
        SevenDayOperation: false,
        AllowsFlex: false,
        AllowsDispute: false,
        DisplayName: "Hours",
        ReviewPolicyId: HoursReviewWorkflowCatalog.CascadingApproval);

    private static HoursCustomer NordvikOfficeCustomer { get; } = new(
        NordvikOffice,
        "NO",
        "Europe/Oslo",
        "norway.private.flex",
        TimeSpan.FromHours(7.5),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
        new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        DisplayName: "Nordvik",
        ReviewPolicyId: HoursReviewWorkflowCatalog.CascadingApproval);

    private static HoursCustomer NordvikStationCustomer { get; } = new(
        NordvikStation,
        "NO",
        "Europe/Oslo",
        "norway.state.flex",
        TimeSpan.FromHours(7.5),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
        new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
        ObservesPublicHolidays: false,
        SevenDayOperation: true,
        DisplayName: "Nordvik Station",
        ReviewPolicyId: HoursReviewWorkflowCatalog.EmployerAcknowledged);

    private static HoursCustomer AtelierCurieCustomer { get; } = new(
        AtelierCurie,
        "FR",
        "Europe/Paris",
        "france.annualisation",
        TimeSpan.FromHours(7),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(10, 0), new TimeOnly(16, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(12, 0)),
        new LocalTimeRange(new TimeOnly(13, 0), new TimeOnly(17, 0)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        DisplayName: "Atelier Curie",
        ReviewPolicyId: HoursReviewWorkflowCatalog.SingleApprover);

    private static HoursCustomer WarsawSettlementCustomer { get; } = new(
        WarsawSettlement,
        "PL",
        "Europe/Warsaw",
        "poland.okres-rozliczeniowy",
        TimeSpan.FromHours(8),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(12, 0)),
        new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        DisplayName: "Warsaw Settlement",
        ReviewPolicyId: HoursReviewWorkflowCatalog.EmployerOnly);

    private static HoursCustomer HelsinkiFlexCustomer { get; } = new(
        HelsinkiFlex,
        "FI",
        "Europe/Helsinki",
        "finland.liukuva-tyoaika",
        TimeSpan.FromHours(7.5),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
        new LocalTimeRange(new TimeOnly(12, 0), new TimeOnly(16, 0)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        DisplayName: "Helsinki Flex",
        ReviewPolicyId: HoursReviewWorkflowCatalog.EmployeeHr);

    private static HoursCustomer GameRetailCustomer { get; } = new(
        GameRetail,
        "GB",
        "Europe/London",
        "england.retail.rigid",
        TimeSpan.FromHours(8),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(18, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(18, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(13, 0)),
        new LocalTimeRange(new TimeOnly(14, 0), new TimeOnly(18, 0)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        SaturdayIsWorkingDay: true,
        AllowsFlex: false,
        AllowsDispute: false,
        AttendanceConfirmationOnly: true,
        DisplayName: "Game",
        ReviewPolicyId: HoursReviewWorkflowCatalog.AttendanceHr);

    private static HoursCustomer PacificYardCustomer { get; } = new(
        PacificYard,
        "US",
        "America/Los_Angeles",
        "usa.flsa.weekly",
        TimeSpan.FromHours(8),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(12, 0)),
        new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        SaturdayIsWorkingDay: false,
        AllowsFlex: false,
        AllowsDispute: false,
        AttendanceConfirmationOnly: true,
        DisplayName: "Pacific Yard",
        ReviewPolicyId: HoursReviewWorkflowCatalog.AttendanceHr);

    private static HoursCustomer TorontoYardCustomer { get; } = new(
        TorontoYard,
        "CA",
        "America/Toronto",
        "canada.ontario.esa",
        TimeSpan.FromHours(8),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(12, 0)),
        new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        AllowsFlex: false,
        AllowsDispute: true,
        DisplayName: "Toronto Yard",
        ReviewPolicyId: HoursReviewWorkflowCatalog.EmployeeHr);

    private static HoursCustomer TokyoFlexCustomer { get; } = new(
        TokyoFlex,
        "JP",
        "Asia/Tokyo",
        "japan.lsa.36",
        TimeSpan.FromHours(8),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(19, 0)),
        new LocalTimeRange(new TimeOnly(10, 0), new TimeOnly(15, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(12, 0)),
        new LocalTimeRange(new TimeOnly(13, 0), new TimeOnly(18, 0)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        AllowsFlex: true,
        AllowsDispute: true,
        DisplayName: "Tokyo Flex",
        ReviewPolicyId: HoursReviewWorkflowCatalog.EmployeeHr);

    private static HoursCustomer BerlinOfficeCustomer { get; } = new(
        BerlinOffice,
        "DE",
        "Europe/Berlin",
        "germany.arbzg",
        TimeSpan.FromHours(8),
        new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(18, 0)),
        new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0)),
        new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(12, 0)),
        new LocalTimeRange(new TimeOnly(12, 30), new TimeOnly(16, 30)),
        ObservesPublicHolidays: true,
        SevenDayOperation: false,
        AllowsFlex: false,
        AllowsDispute: true,
        DisplayName: "Berlin Office",
        ReviewPolicyId: HoursReviewWorkflowCatalog.EmployeeHr);
}
