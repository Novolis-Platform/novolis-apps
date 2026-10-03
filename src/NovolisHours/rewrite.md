# Novolis Hours
## Core Rewrite Implementation Specification

## 1. Purpose

Novolis Hours is an **explainable time-accounting system**.

Its core responsibility is to:

1. record working-time assertions without forcing them to match policy;
2. resolve each logical WorkDay through ordered, stackable rules;
3. classify and allocate time through customer-defined Dimensions;
4. maintain balance-affecting time through a double-entry duration ledger;
5. support mutual review, approval, correction and dispute without destroying history;
6. report compliance concerns without enforcing compliance by altering or rejecting reality;
7. turn accumulated time and compliance information into useful organisational health and workload reporting.

The central invariant is:

> **Reality is recorded. Meaning is derived. History is preserved.**

And the defining user-facing property is:

> **Every number is explainable.**

---

# 2. Scope

This rewrite specifies the Hours domain and the application logic immediately around it.

It includes:

- work registration;
- WorkDay resolution;
- calendar rules;
- configuration composition;
- Dimensions;
- ledger;
- notes and provenance;
- review and approval;
- disputes and corrections;
- compliance indicators;
- health-concern reporting;
- business-pressure reporting;
- audit drill-down;
- core reports and projections.

It deliberately does **not** redesign:

- authentication;
- storage technology;
- hosting;
- ASP.NET infrastructure;
- SignalR;
- native clients;
- telemetry;
- bootstrap;
- external identity.

Those existing facilities should adapt to the new domain contracts.

No compatibility layer for the old Hours domain model is required. Prefer deleting obsolete concepts over adapting them.

---

# 3. Architectural principles

## 3.1 Facts are small

Do not persist a giant “timesheet row” containing:

- actual work;
- expected work;
- flex;
- legal notices;
- policy metadata;
- approvals;
- classifications;
- financial treatment.

A work registration is an assertion about work.

Everything else is derived from it or recorded separately.

---

## 3.2 Rules never rewrite facts

Calendars, Dimension rules and compliance rules consume facts.

They produce derived results.

They do not change the registration that caused those results.

---

## 3.3 Configuration is sparse and ordered

“Configurable” does **not** mean every setting must have an administration UI.

Configuration is primarily an implementation mechanism.

Configuration is composed from ordered layers.

Each layer contributes only what it cares about.

Silence means:

> I have no opinion.

It does not mean:

> Reset this value to the default.

Later contributions take precedence where the relevant property is exclusive.

Additive concepts accumulate.

---

## 3.4 Simple rule types beat a rule language

Do not implement:

- scripting;
- expression languages;
- general-purpose workflow DSLs;
- visual rule engines;
- generic policy interpreters.

Use small strongly typed C# rule objects.

Repeated simple patterns are preferable to an abstraction capable of modelling the universe.

---

# 4. Core processing model

The conceptual pipeline is:

```text
Configuration
      ↓
DayShape
      ↓
WorkRegistration
      ↓
Resolved WorkDay
      ↓
Dimensions
      ↓
Ledger + Compliance
      ↓
Workflow / Review
      ↓
Reports
```

None of the lower stages rewrites the stages above it.

---

# 5. WorkDay

A `WorkDay` is a logical day belonging to an employee.

It is identified by:

```csharp
public readonly record struct WorkDayKey(
    string EmployeeId,
    DateOnly NominalDate);
```

There is no need to persist an empty WorkDay entity merely because a date exists.

A WorkDay projection is created from configuration and registrations for its key.

## 5.1 NominalDate

`NominalDate` identifies which logical working day the work belongs to.

It does not create a midnight boundary.

These are both valid single WorkDays:

```text
08:00 → 15:00
20:00 → 22:00
```

and:

```text
10:00 → 02:00 next calendar day
```

Calendar-day reports may split durations later.

The WorkDay itself remains intact.

---

# 6. Work intervals

Actual work uses real timestamps:

```csharp
public sealed record WorkInterval(
    DateTimeOffset Start,
    DateTimeOffset End);
```

Required invariant:

```text
End > Start
```

A registration may contain multiple intervals.

This makes separate “break” entities unnecessary for actual work.

Example:

```text
08:00 → 12:00
12:30 → 16:00
```

already states that the person did not register work from 12:00 to 12:30.

Similarly:

```text
08:00 → 15:00
20:00 → 22:00
```

truthfully represents a split WorkDay.

A gap is not an anomaly.

It is simply time the employee did not claim to have worked.

---

# 7. WorkRegistration

A `WorkRegistration` is an immutable assertion.

```csharp
public sealed record WorkRegistration(
    Guid Id,
    WorkDayKey WorkDay,
    WorkRecordSource Source,
    WorkRecordIntent Intent,
    ImmutableArray<WorkInterval> Intervals,
    Guid? CorrectsRegistrationId,
    string? Note,
    ActorRef RecordedBy,
    DateTimeOffset RecordedAt,
    ConfigurationSnapshotId ConfigurationSnapshotId);
```

## 7.1 Source

```csharp
public enum WorkRecordSource
{
    Employee,
    Employer,
    Integration,
    System
}
```

Source describes where the assertion came from.

It says nothing about whether it has been accepted.

---

## 7.2 Intent

```csharp
public enum WorkRecordIntent
{
    WorkedAsScheduled,
    ManualRegistration,
    Correction
}
```

### WorkedAsScheduled

The actor explicitly asserts:

> The configured routine is an acceptable representation of the work performed.

Its `Intervals` are empty.

Resolution obtains the intervals from the applicable DayShape.

This distinction must remain visible in audit history.

The system must never present schedule-derived intervals as clock observations.

### ManualRegistration

The actor supplies explicit intervals.

### Correction

The actor creates a new assertion and references the registration being corrected.

The original record remains immutable.

---

# 8. Registration UX

A normal WorkDay should require one action.

Example:

```text
Today

Routine
08:00 → 16:30
Lunch gap
11:30 → 12:30

Expected work
7:30

[ Worked as scheduled ]
```

That is sufficient.

For a deviation:

```text
08:17 → 12:00
12:45 → 17:02

[ Register ]
```

No project selection, legal classification, approval metadata or explanation is required to establish that the work occurred.

## 8.1 Notes

A note is context, not permission.

Registrations may carry an optional note.

Additional context may be appended separately:

```csharp
public sealed record WorkDayNote(
    Guid Id,
    WorkDayKey WorkDay,
    string Text,
    ActorRef RecordedBy,
    DateTimeOffset RecordedAt);
```

Do not add a taxonomy until one is demonstrably needed.

---

# 9. Routine differences

The WorkDay projection may compare actual work with the configured routine.

Examples:

```text
Started 22 minutes after routine.
Worked in two intervals.
Worked 01:15 outside routine.
```

Call these **routine differences**, not anomalies.

They are informational.

They:

- do not create compliance indicators;
- do not require manager approval;
- do not require HR review;
- do not create escalation;
- do not imply misconduct.

A four-hour gap can be evidence of an employee registering time honestly.

The UI may offer:

```text
[ Add note ]
```

but should not demand an explanation merely because the shape is unusual.

---

# 10. Calendar model

Calendars determine the expected shape of a date.

The model consists of:

```text
ICalendarRule
        ↓
DayRule[]
        ↓
ordered Calendar stack
        ↓
DayShape
```

---

# 11. ICalendarRule

```csharp
public interface ICalendarRule
{
    IReadOnlyList<DayRule> GetRules(DateOnly date);
}
```

An `ICalendarRule` answers:

> What do I have to say about this date?

It may return zero rules.

Examples of selectors include:

- weekday;
- weekend;
- fixed date;
- date range;
- public holiday;
- Easter-relative date;
- temporary date override.

Selection and meaning must remain separate.

“Every Sunday” is selection.

“Non-working day” is meaning.

---

# 12. DayRule

`DayRule` is the semantic contribution.

Use simple explicit record types.

Examples:

```csharp
public abstract record DayRule;

public sealed record WorkingDayRule(bool Value) : DayRule;

public sealed record ExpectedWorkRule(TimeSpan Duration) : DayRule;

public sealed record PaidEntitlementRule(TimeSpan Duration) : DayRule;

public sealed record WorkEnvelopeRule(LocalTimeRange Range) : DayRule;

public sealed record CoreHoursRule(
    ImmutableArray<LocalTimeRange> Ranges) : DayRule;

public sealed record RoutineWorkRule(
    ImmutableArray<LocalTimeRange> Ranges) : DayRule;

public sealed record DayTagRule(
    string Key,
    string Value) : DayRule;
```

Do not create one enormous `DayRule` containing every possible nullable property.

Small rules make provenance clear.

---

# 13. Calendar stack

A worker may receive calendars from several sources:

```text
National
↓
Organisation
↓
Agreement
↓
Employment
↓
Employee
↓
Temporary override
```

Only applicable calendars participate.

A calendar is effective-dated and versioned.

Order matters.

## 13.1 Composition rules

Keep composition semantics deliberately small.

For scalar/replaceable properties:

```text
WorkingDay
ExpectedWork
PaidEntitlement
WorkEnvelope
CoreHours
RoutineWork
```

the last applicable rule wins.

For additive properties such as tags:

```text
PublicHoliday
SpecialDay
Organisation-defined labels
```

all applicable values remain.

Do not invent configurable merge strategies until required.

---

# 14. ICalendarRuleSet

The resolved stack exposes:

```csharp
public interface ICalendarRuleSet
{
    DayShape GetDayShape(DateOnly date);
}
```

It must preserve rule order.

It must not inherit from `ISet<T>` because order is semantically significant.

---

# 15. DayShape

```csharp
public sealed record DayShape(
    DateOnly Date,
    bool IsWorkingDay,
    TimeSpan ExpectedWork,
    TimeSpan PaidEntitlement,
    LocalTimeRange? WorkEnvelope,
    ImmutableArray<LocalTimeRange> CoreHours,
    ImmutableArray<LocalTimeRange> RoutineWork,
    ImmutableArray<DayTag> Tags,
    ImmutableArray<AppliedDayRule> Rules);
```

`Rules` is first-class product data.

It contains the complete ordered rule slice that produced the day.

Example:

```text
Sunday
    Norway → NonWorking

Christmas Day
    Norway → PublicHoliday
    Agreement → PaidEntitlement 7:30

Gas station
    Employer → WorkingDay

Employment
    ExpectedWork 7:30
```

Result:

```text
Working day       yes
Holiday           Christmas
Expected work     7:30
Paid entitlement  7:30
```

And the exact reason is available from `DayShape.Rules`.

---

# 16. AppliedDayRule

```csharp
public sealed record AppliedDayRule(
    string RuleId,
    string CalendarId,
    string CalendarVersion,
    int Order,
    DayRule Rule,
    RuleSource Source);
```

`RuleSource` should be sufficient to expose provenance such as:

- manually defined;
- built-in;
- generated;
- source package;
- package version.

---

# 17. Holiday generation

National public-holiday support should not call a holiday NuGet package dynamically at runtime.

Instead:

1. consume the holiday library during generation;
2. produce explicit Novolis calendar rules;
3. compile/store those rules as normal calendar input;
4. record generator provenance.

Generated rules should include:

```text
Jurisdiction
Date
Holiday identifier
Source package
Source package version
Generator version
```

A dependency update should therefore produce a visible rule/data diff rather than silently changing historical interpretation.

Runtime calendar resolution depends only on Novolis rules.

---

# 18. Effective configuration

All configurable domains follow the same broad philosophy:

> **ordered sparse contributions**

Do not create one giant generic configuration engine.

Calendar, Dimensions, compliance and workflow may each implement the pattern independently.

A resolved configuration is immutable and identified by:

```csharp
public readonly record struct ConfigurationSnapshotId(Guid Value);
```

A snapshot references the exact versions of:

- calendar stack;
- Dimension configuration;
- Dimension rules;
- compliance rules;
- workflow configuration;
- ledger configuration.

Registrations keep the snapshot active when they were created.

Historical interpretation must therefore remain reproducible.

---

# 19. WorkDay resolution

Resolution combines:

```text
Effective WorkRegistration
+
DayShape
=
ResolvedWorkDay
```

Conceptually:

```csharp
public sealed record ResolvedWorkDay(
    WorkDayKey Key,
    WorkRegistration EffectiveRegistration,
    DayShape Shape,
    ImmutableArray<WorkInterval> WorkedIntervals,
    TimeSpan ActualWorked,
    RoutineComparison RoutineComparison);
```

For `WorkedAsScheduled`:

```text
WorkedIntervals = DayShape.RoutineWork resolved into real timestamps
```

For manual registration:

```text
WorkedIntervals = WorkRegistration.Intervals
```

---

# 20. Time zones

Registrations store `DateTimeOffset`.

Calendar/routine definitions use local clock values.

Each employment/configuration snapshot therefore requires an effective time zone.

Conversion from local routine time to actual timestamps must be deterministic, including DST transitions.

Do not use `TimeOnly` as the authoritative representation of actual recorded work.

---

# 21. Dimensions

Dimensions are the product's general mechanism for giving worked time business meaning.

Examples include:

```text
Regular / Flex / Overtime
Billable / Non-billable
Project
Customer
Feature
Task
Cost centre
Incident
Product
Activity
```

The customer chooses which Dimensions matter.

They are configuration, not code branches.

---

# 22. DimensionDefinition

```csharp
public sealed record DimensionDefinition(
    string Id,
    string Name,
    DimensionAssignmentMode AssignmentMode,
    DimensionCardinality Cardinality,
    bool RequireFullCoverage,
    ImmutableArray<DimensionValue> Values);
```

Assignment mode:

```csharp
public enum DimensionAssignmentMode
{
    Derived,
    Manual,
    DerivedAndManual
}
```

Cardinality:

```csharp
public enum DimensionCardinality
{
    Exclusive,
    Additive
}
```

Examples:

```text
Project
    Manual
    Exclusive

Tags
    Manual
    Additive

TimeType
    Derived
    Exclusive

Billability
    DerivedAndManual
    Exclusive
```

---

# 23. Dimension measures

Dimensions must support both interval-based and duration-based meaning.

```csharp
public sealed record DimensionMeasure(
    string DimensionId,
    string ValueId,
    TimeSpan Duration,
    WorkInterval? Interval,
    DimensionMeasureSource Source,
    RuleRef? Rule);
```

Examples:

### Project

```text
Customer = ACME
Interval = 08:00 → 10:30
Duration = 02:30
```

### Overtime

```text
TimeType = Overtime
Interval = 17:00 → 18:15
Duration = 01:15
```

### Flex deficit

```text
TimeType = Flex
Interval = null
Duration = -00:30
```

This allows one Dimension model to represent both painted time and signed accounting quantities.

---

# 24. Dimension rules

Derived Dimensions use small rules:

```csharp
public interface IDimensionRule
{
    IReadOnlyList<DimensionMeasure> Evaluate(
        DimensionContext context);
}
```

The `DimensionContext` includes:

- ResolvedWorkDay;
- effective Dimension definitions;
- previously applied derived measures where ordering requires them.

Rules are evaluated in deterministic order.

For exclusive Dimensions, a later rule may replace an earlier rule over the overlapping portion.

For additive Dimensions, values accumulate.

Do not introduce a general expression language.

---

# 25. Dimension configuration stacking

Dimension configuration is sparse and stackable just like calendars.

Typical sources:

```text
Product
↓
Organisation
↓
Agreement
↓
Employment
↓
Employee override
```

A layer may:

- introduce a Dimension;
- replace a Dimension definition with the same stable ID;
- add values;
- contribute Dimension rules.

Order is explicit.

Later configuration wins for the same exclusive definition/value.

---

# 26. Manual Dimension assignments

Manual assignment happens **after** worked time has been established.

Example UI:

```text
08:00 ───────────────────── 16:30

08:00 → 10:30   ACME
10:30 → 12:00   Internal
13:00 → 16:30   ACME
```

Each assignment is append-only.

```csharp
public sealed record DimensionAssignment(
    Guid Id,
    WorkDayKey WorkDay,
    string DimensionId,
    string ValueId,
    ImmutableArray<WorkInterval> Intervals,
    Guid? CorrectsAssignmentId,
    ActorRef RecordedBy,
    DateTimeOffset RecordedAt);
```

A project allocation never creates working time.

It can only describe already-resolved work.

Missing project allocation therefore cannot invalidate a WorkRegistration.

---

# 27. Ledger

The duration ledger remains deliberately small.

Initial duration accounts:

```csharp
public enum DurationAccount
{
    EmployeeFlex,
    OrganisationControl
}
```

No separate account is created merely because something is:

- overtime;
- regular work;
- billable;
- project time;
- night work.

Those are Dimensions.

---

# 28. LedgerTransaction

```csharp
public sealed record LedgerTransaction
{
    public Guid Id { get; }
    public WorkDayKey WorkDay { get; }
    public Guid SourceRegistrationId { get; }
    public ImmutableArray<DurationPosting> Postings { get; }
    public string Reason { get; }
}
```

Construction must fail unless:

```text
Σ SignedDuration = 0
```

A transaction can therefore never exist unbalanced.

Example:

```text
EmployeeFlex          +00:45
OrganisationControl   -00:45
                     --------
                       00:00
```

---

# 29. Dimension-to-ledger mapping

A configured **derived** Dimension value may be designated as affecting the flex ledger.

For example:

```text
Dimension: TimeType

FlexCredit
    ledger multiplier +1

FlexDebit
    ledger multiplier -1
```

The ledger projector consumes derived measures and creates the corresponding balanced transaction.

Manual Project/Customer assignments must not accidentally affect balances.

Only explicitly configured derived Dimension values may have ledger effects.

---

# 30. Ledger corrections

Never edit a ledger transaction.

If a corrected registration changes:

```text
Flex +03:00
```

to:

```text
Flex +02:00
```

append:

```text
EmployeeFlex          -01:00
OrganisationControl   +01:00
```

The current saldo is:

```text
Σ EmployeeFlex postings
```

The saldo itself is never authoritative mutable state.

---

# 31. Review periods

Keep review separate from working-time truth.

A review period identifies:

```csharp
public sealed record ReviewPeriod(
    Guid Id,
    string EmployeeId,
    DateOnly From,
    DateOnly Through);
```

Monthly periods are a sensible normal default.

Do not hard-code “month” into the fundamental model.

---

# 32. Review actions

Review history is append-only.

```csharp
public sealed record ReviewAction(
    Guid Id,
    Guid PeriodId,
    ReviewActionKind Kind,
    ActorRef Actor,
    int? ApprovalLevel,
    string? Comment,
    DateTimeOffset RecordedAt);
```

Useful kinds:

```csharp
public enum ReviewActionKind
{
    Submit,
    Approve,
    Acknowledge,
    Dispute,
    Resolve,
    Comment
}
```

Current review state is a projection over actions.

Do not repeatedly persist mutated copies of a large “approval state” object.

---

# 33. Workflow policy

Workflow configuration says which actions are required.

Examples:

```text
Employee
    Submit

Manager level 1
    Approve
```

or:

```text
Employer registers
Employee
    Acknowledge
```

or:

```text
Manager level 1
    Approve

Manager level 2
    Approve

HR
    Final approve
```

No particular workflow is assumed to be morally or legally universal.

---

# 34. Corrections after approval

Approval does not freeze truth.

If a registration is corrected after approval:

- the previous approval remains in history;
- the new projection says the period changed after that approval;
- the applicable review steps may become outstanding again.

Do not delete the old approval.

---

# 35. Disputes

A dispute records disagreement.

It does not erase either side.

A dispute may target:

- a registration;
- a correction;
- a Dimension assignment where relevant;
- a ledger-affecting adjustment;
- a review period.

Resolution is another append-only action.

The full path remains visible:

```text
Employee assertion
→ Employer correction
→ Employee dispute
→ Manager/HR resolution
```

---

# 36. No automatic escalation from work shape

This is a product invariant.

The following must **not** automatically create:

- HR escalation;
- disciplinary workflow;
- manager investigation;
- dispute;
- dismissal workflow.

Examples:

```text
Started later than routine
Four-hour gap
Weekend work
Night work
Long workday
Compliance indicator
```

Compliance and routine-comparison engines return information.

They do not call workflow services.

If a human needs to review something, a human initiates that review.

---

# 37. Human roles

Every normal human user has the baseline employee relationship.

Additional product responsibilities are:

```text
Employee
Manager
Human Resources
Auditor
```

System/integrations are actors, not human roles.

Administration remains infrastructure and configuration access rather than a business approval role.

---

# 38. Manager hierarchy

Do not create:

```text
Manager
SeniorManager
RegionalManager
DirectorManager
```

Use responsibility and approval level:

```csharp
public sealed record Responsibility(
    string UserId,
    ResponsibilityRole Role,
    string ScopeId,
    int? ApprovalLevel);
```

Example:

```text
Manager
Scope = Team A
ApprovalLevel = 1
```

and:

```text
Manager
Scope = Division North
ApprovalLevel = 2
```

---

# 39. Report access

All human roles receive reports appropriate to their responsibility.

### Employee

Own:

- WorkDays;
- rules;
- registrations;
- Dimensions;
- ledger;
- balance;
- compliance indicators;
- review history.

### Manager

The same information for responsible scopes.

### HR

Cross-scope reports and final-review tools where configured.

### Auditor

Read-only access to responsible scopes with complete provenance.

Auditor is deliberately powerful for reading and powerless for mutation.

---

# 40. Compliance

Compliance is a reporting domain.

```csharp
public interface IComplianceRule
{
    IReadOnlyList<ComplianceIndicator> Evaluate(
        ComplianceContext context);
}
```

A compliance rule can inspect:

- worked intervals;
- DayShape;
- Dimension results;
- surrounding WorkDays where necessary.

It returns information only.

---

# 41. ComplianceIndicator

```csharp
public sealed record ComplianceIndicator(
    string Code,
    string Category,
    string Message,
    WorkDayKey WorkDay,
    WorkInterval? AffectedInterval,
    RuleRef Rule);
```

Avoid domain terminology such as:

```text
EmployeeViolation
Offender
BreachByEmployee
```

Prefer factual language:

```text
Daily rest below configured threshold.
Worked duration above configured daily threshold.
Night work detected.
```

Indicators describe what the rule observed.

They do not infer motive or misconduct.

---

# 42. Compliance cannot block registration

This must hold everywhere:

```text
Register work
      ↓
registration succeeds
      ↓
compliance evaluation
      ↓
zero or more indicators
```

Never:

```text
Compliance rule
      ↓
reject registration
```

A time-accounting product that cannot represent problematic working time cannot audit problematic working time.

---

# 43. Health Concerns report

`Health Concerns` is a first-class organisational report.

Individual indicators explain a WorkDay.

Aggregated indicators describe patterns in the organisation.

Useful aggregations include:

- long working days over time;
- insufficient-rest indicators;
- night work;
- weekend work;
- repeated out-of-hours work;
- break-related indicators;
- overtime volume;
- concentration by team;
- trends over periods.

Primary framing:

> **Where does the organisation appear to be creating unhealthy working patterns?**

Not:

> Which employees are behaving badly?

Employee-level drill-down remains available where the viewer's scope permits it, but employee leaderboards are not the primary report shape.

---

# 44. Business-pressure reporting

Dimensions allow the same information to answer business questions.

Examples:

```text
Which customers are associated with the most overtime?

Which projects generate the most evening work?

Which products are associated with recurring rest concerns?

Which customers consume disproportionate flex or overtime?

Where is unplanned work accumulating?
```

A report may aggregate:

```text
Customer
Project
Feature
Incident
Product
Department
```

against:

```text
Overtime duration
Extra-work duration
Out-of-hours duration
Night duration
Compliance indicators
```

Use neutral language such as:

> 31% of recorded overtime in this period was allocated to Project X.

Do not claim:

> Project X caused 31% of employee health problems.

Correlation is visible.

Causality remains a human conclusion.

---

# 45. Attribution rules

Business-pressure reports should use actual temporal overlap where possible.

Example:

```text
Overtime
17:00 → 19:00

Project ACME
16:00 → 18:30
```

ACME receives:

```text
01:30 overtime association
```

Do not invent attribution where the data cannot support one.

Day-level signed quantities without an interval may remain:

```text
Unattributed
```

unless there is an unambiguous configured allocation.

---

# 46. Audit trail

Auditability is domain data, not application logging.

Every meaningful report value should be able to expose a path such as:

```text
Report value
↓
Dimension measure / compliance indicator / ledger posting
↓
Resolved WorkDay
↓
Effective WorkRegistration
↓
DayShape
↓
Applied DayRules
↓
Configuration snapshot
↓
Original actors and timestamps
```

Review information adds:

```text
Submission
Approval
Dispute
Resolution
```

Nothing should terminate at:

> “The calculator produced 7.5.”

---

# 47. Application services

Do not replace one large service with twenty ceremonial services.

The application layer needs only a few orchestration boundaries.

Suggested responsibilities:

```text
WorkRegistrationService
    Register
    Correct
    Add note

WorkDayService
    Resolve/evaluate a WorkDay

DimensionService
    Add/correct manual allocations

ReviewService
    Open/submit/approve/dispute/resolve periods

ReportService
    Query employee, management, health and audit projections
```

Pure domain components underneath them:

```text
CalendarResolver
WorkRegistrationResolver
WorkDayEvaluator
DimensionEvaluator
ComplianceEvaluator
LedgerProjector
ReviewProjector
```

These should have very little dependency on one another.

---

# 48. Effective registration

A WorkDay may contain several historical registrations.

A pure resolver determines the currently operative assertion.

Conceptually:

```csharp
public interface IWorkRegistrationResolver
{
    WorkRegistration Resolve(
        IReadOnlyList<WorkRegistration> registrations,
        IReadOnlyList<ReviewAction> reviewActions);
}
```

The exact authority rules come from workflow configuration.

The resolver must never delete losing/older assertions.

They remain available for audit.

---

# 49. Journal/write model

Continue using an append-oriented journal model.

The important event families are conceptually:

```text
WorkRegistered
WorkDayNoteAdded
DimensionAssigned
ReviewActionRecorded
LedgerTransactionRecorded
```

Do not persist mutated copies of entire employee state.

Current state is projected.

A generic event-sourcing framework is unnecessary.

---

# 50. Atomic domain writes

Where one operation creates multiple authoritative facts, they must be committed together.

Example:

```text
Registration
+
corresponding ledger transaction
```

must not leave the system permanently half-written.

The persistence mechanism used to guarantee this is outside this specification.

The journal abstraction should expose an atomic commit/batch boundary if required.

---

# 51. Projections

Projections are disposable.

Examples:

```text
Employee period view
Current flex saldo
Manager dashboard
Approval state
Health Concerns report
Project workload report
Audit timeline
```

A projection may be cached.

It must always be reconstructible from authoritative records and configuration snapshots.

---

# 52. Reports

Initial useful report set:

### Employee period

- expected work;
- actual work;
- Dimension totals;
- flex balance;
- approval state;
- compliance indicators.

### Ledger

- transactions;
- postings;
- running saldo.

### Management

- worked time by team;
- Dimension breakdown;
- overtime/flex;
- review status.

### Health Concerns

- compliance trends;
- extra-work trends;
- organisational concentrations;
- drill-down.

### Business pressure

- customer/project/product versus extra-work measures;
- overtime;
- night/weekend work;
- compliance associations.

### Audit

- complete causal chain for selected period/value.

---

# 53. Report drill-down requirement

All aggregated rows should retain references to their contributing records.

Example:

```text
Project ACME
Overtime 47:30
```

must be drillable to:

```text
employees/days
→ overtime measures
→ work intervals
→ registrations
→ rules
```

Do not build aggregate reports that discard provenance and then attempt to reconstruct it later.

---

# 54. What not to implement

The rewrite should explicitly avoid:

- absence management;
- payroll calculation;
- tax;
- monetary rates;
- project management;
- generic IAM;
- employee productivity scoring;
- employee compliance rankings;
- automatic disciplinary escalation;
- mutable flex saldo fields;
- a general rule DSL;
- arbitrary scripting;
- mandatory reason codes for normal schedule deviations;
- one giant `HoursConfiguration`;
- Controller → Service → Repository layering for its own sake.

---

# 55. Critical invariants

The following invariants should have direct tests.

## Work

- registrations are immutable;
- corrections append;
- a WorkDay may cross midnight;
- a WorkDay may contain multiple intervals;
- registration is allowed regardless of compliance result.

## Calendars

- rule order is deterministic;
- sparse layers do not reset lower layers;
- later exclusive rules win;
- additive rules accumulate;
- DayShape always retains its applied rule slice.

## Dimensions

- Dimensions cannot create actual working time;
- manual allocation cannot extend beyond actual work where interval-based;
- exclusive Dimensions cannot have competing final values over the same interval;
- additive Dimensions may overlap;
- derived measures retain rule provenance.

## Ledger

- every transaction sums to zero;
- transactions are immutable;
- corrections append transactions;
- saldo is derived from postings.

## Workflow

- review actions are immutable;
- approval never deletes registrations;
- correction after approval remains possible;
- dispute preserves all competing assertions;
- compliance indicators cannot create workflow actions.

## Compliance

- indicators never reject registrations;
- indicators never modify intervals;
- indicators retain the rule that produced them.

## Audit

- every ledger movement identifies its source;
- every derived Dimension identifies its rule;
- every DayShape identifies its rules;
- every human action identifies its actor and timestamp.

---

# 56. Required behavioural tests

The rewrite is not complete until these scenarios pass through real domain composition.

### Normal day

Employee selects `WorkedAsScheduled`.

Routine intervals resolve correctly.

Expected and actual time match.

No manual clock input is required.

### Split WorkDay

```text
08:00 → 15:00
20:00 → 22:00
```

is stored as one WorkDay with two intervals.

No anomaly exists merely because it is split.

### Midnight

```text
10:00 → 02:00
```

remains one logical WorkDay.

### Gas station Christmas

Base national calendar contributes:

```text
Sunday
NonWorking
Christmas
PublicHoliday
```

Gas-station calendar contributes:

```text
WorkingDay
```

Agreement contributes:

```text
PaidEntitlement
```

The resulting DayShape contains all applicable semantics and the complete rule chain.

### Corporate paid day

Christmas Eve corporate rule overrides expected work appropriately while preserving paid entitlement.

### Mid-year policy change

A rule beginning 15 May changes days from 15 May onward without changing earlier evaluations.

### Correction

Original registration remains.

Correction becomes operative according to workflow.

Ledger difference is appended rather than rewritten.

### Four-hour gap

Employee records two honest work intervals separated by four hours.

The WorkDay is accepted.

Routine comparison may describe the difference.

No automatic HR/manager workflow is created.

### Compliance concern

A long WorkDay is registered successfully.

A compliance indicator is produced afterwards.

### Dimension stacking

Organisation defines Project and Billability.

Department adds CostCentre.

Employment contributes TimeType rules.

All coexist deterministically.

### Project painting

Project allocations describe only actual worked intervals.

They do not affect ActualWorked.

### Flex ledger

A derived +45 minute flex measure produces:

```text
EmployeeFlex +00:45
OrganisationControl -00:45
```

### Flex correction

A correction reducing +45 to +30 produces a new -15 minute correcting transaction.

### Multi-manager approval

Manager levels 1 and 2 both review according to configured responsibility.

No new role types are required.

### Dispute

Employee dispute preserves employer assertion, employee assertion and resolution.

### Post-approval correction

Period previously approved receives a correction.

Old approval remains visible.

Projection indicates new review is required.

### Health report

Repeated rest/long-day indicators aggregate at organisational level.

No employee “worst offender” ranking is generated.

### Customer pressure

Overtime intervals overlapping Customer ACME allocations aggregate into ACME's business-pressure report with drill-down to source WorkDays.

---

# 57. Testing approach

Keep the current philosophy of testing the system as realistically as practical.

Use:

- xUnit;
- FluentAssertions;
- real domain objects;
- real rule stacks;
- real projectors;
- real application services;
- in-memory journal/storage implementations for feature tests;
- real ASP.NET host tests for externally visible workflows.

Mock only actual external boundaries where a real or in-memory implementation is inappropriate.

Calendar, Dimension, compliance and ledger tests should generally require no mocks.

---

# 58. Suggested domain structure

Keep physical structure unsurprising:

```text
Novolis.Hours.Domain
│
├── Work
│   ├── WorkDayKey
│   ├── WorkInterval
│   ├── WorkRegistration
│   ├── WorkDayNote
│   └── WorkRegistrationResolver
│
├── Calendars
│   ├── ICalendarRule
│   ├── DayRule
│   ├── Calendar
│   ├── CalendarRuleSet
│   ├── DayShape
│   └── CalendarResolver
│
├── Dimensions
│   ├── DimensionDefinition
│   ├── DimensionValue
│   ├── DimensionAssignment
│   ├── DimensionMeasure
│   ├── IDimensionRule
│   └── DimensionEvaluator
│
├── Ledger
│   ├── DurationAccount
│   ├── DurationPosting
│   ├── LedgerTransaction
│   └── LedgerProjector
│
├── Review
│   ├── ReviewPeriod
│   ├── ReviewAction
│   ├── ReviewPolicy
│   └── ReviewProjector
│
├── Compliance
│   ├── IComplianceRule
│   ├── ComplianceIndicator
│   └── ComplianceEvaluator
│
└── Reporting
    ├── EmployeeReport
    ├── HealthConcernsReport
    ├── BusinessPressureReport
    └── AuditProjection
```

Do not create a project per folder.

The existing Domain/Application/Contracts split remains sufficient.

---

# 59. Rewrite sequence

Implement vertically rather than constructing an enormous framework first.

### Phase 1: Work + calendars

Implement:

- WorkDayKey;
- WorkInterval;
- WorkRegistration;
- source/intent;
- calendar stacking;
- DayRule;
- DayShape with rule provenance;
- WorkedAsScheduled resolution.

This establishes the fundamental model.

### Phase 2: Dimensions + evaluation

Implement:

- Dimension definitions;
- sparse Dimension configuration;
- derived rules;
- manual assignments;
- WorkDay evaluation.

### Phase 3: ledger

Implement:

- ledger transactions;
- two accounts;
- Dimension-to-ledger mapping;
- correcting transactions;
- saldo projection.

### Phase 4: review

Implement:

- review periods;
- append-only actions;
- manager levels;
- HR final review;
- disputes.

### Phase 5: compliance

Implement:

- compliance rule interface;
- indicators;
- rule provenance;
- strict separation from workflow.

### Phase 6: reporting

Implement:

- employee report;
- audit drill-down;
- Health Concerns;
- business-pressure reporting.

### Phase 7: clients

Adapt existing clients to the new model.

The primary registration screen must be built around:

```text
Worked as scheduled
```

and explicit deviations, not around filling a multi-column timesheet.

---

# 60. Definition of done

The core rewrite is successful when a user can inspect a value such as:

```text
October flex
+12:45
```

and follow it through:

```text
+12:45
↓
ledger postings
↓
derived Flex Dimension measures
↓
individual WorkDays
↓
actual registrations
↓
WorkedAsScheduled or explicit intervals
↓
DayShapes
↓
national / corporate / employment / routine rules
↓
configuration versions
↓
actors, corrections and approvals
```

Likewise, management must be able to move upward:

```text
individual honest registrations
↓
compliance indicators
↓
organisation-level Health Concerns
↓
customer/project Dimension correlation
```

without changing the meaning of the underlying work.

The implementation should therefore optimise for four things:

> **Truth. Composition. Traceability. Simplicity.**

Everything else is secondary.

| Term | Meaning | Allegory |
|---|---|---|
| **WorkDay** | The bounded logical unit for one day’s work. It may contain multiple intervals and may continue past midnight until that workday actually ends. | **Git:** Pull request / change set |
| **NominalDate** | The calendar date the WorkDay belongs to. It identifies the day conceptually, but does not constrain all work to 00:00–24:00. | |
| **WorkInterval** | A concrete span during which work occurred. A WorkDay may contain one or many intervals. | **Git:** Individual changed hunk within the PR |
| **WorkRegistration** | An immutable assertion that work happened, or that a previous assertion should be corrected. It records provenance, not legal interpretation. | **Git:** Commit |
| **WorkRecordSource** | Who or what originated the registration: `Employee`, `Employer`, `Integration`, `System`. | **Git:** Commit author / automation identity |
| **WorkRecordIntent** | Why the registration was made, for example `WorkedAsScheduled`, `ManualRegistration`, `Correction`. | **Git:** Commit intent/message category |
| **WorkedAsScheduled** | Positive confirmation that the configured routine/schedule is an acceptable representation of the actual work performed. | **Git:** Commit accepting the existing expected shape |
| **ManualRegistration** | Explicit registration of actual work rather than accepting the configured routine. | **Git:** Explicit commit |
| **Correction** | A new assertion that supersedes or modifies the effective interpretation of an earlier registration without deleting it. | **Git:** Follow-up commit fixing an earlier commit |
| **Rules** | Contextual facts and policies that affect interpretation, such as holidays, special paid days, statutory rules, collective agreements, local rules. | **Accounting:** Accounting policy / regulatory rules |
| **Schedule** | The organisation-defined working structure, such as office hours, core hours, expected daily or weekly patterns. | **Accounting:** Standard posting framework / expected structure |
| **Routine** | The employee’s normal expected pattern within the schedule, used to resolve `WorkedAsScheduled`. | **Git:** Baseline branch state |
| **WorkDayDisposition** | A modifier describing the expected workload for the day, such as normal, half-day, or non-working, without tracking absence reasons. | |
| **Configuration** | The composable set of rules, schedules, routines, workflow policies, classifiers, settlement rules, and other behaviour controlling interpretation. | **Accounting:** Chart/policy configuration |
| **ConfigurationSnapshot** | Immutable identity of the exact configuration used when evaluating a WorkDay, so historical results remain reproducible. | **Git:** Commit SHA / immutable repository state |
| **WorkDayResolution** | The process of converting registrations such as `WorkedAsScheduled` into effective concrete intervals for the logical WorkDay. | **Git:** Materializing the working tree from commits/configuration |
| **WorkDayEvaluation** | Deterministic interpretation of a WorkDay using its registrations and configuration snapshot. Produces worked time, expected time, deductions, classifications, notices, etc. | **Accounting:** Journal processing / posting preparation |
| **ActualWorkedTime** | The time determined to have actually been worked after resolving the registrations and applicable break semantics. | |
| **ExpectedTime** | The expected amount or pattern of work derived from schedule, routine, disposition, and rules. | **Accounting:** Budget / expected position |
| **Break** | A period deducted or excluded from worked time. It may be explicit or rule-derived, and that provenance should remain visible. | |
| **Classifier** | A deterministic rule that assigns meaning to resolved work, such as regular time, flex, overtime, night work, holiday work, break deduction, etc. | **Accounting:** Account coding / transaction classification |
| **Classification** | A derived interpretation attached to some part of worked time. Multiple classifications may coexist on the same interval. | **Accounting:** Account/category coding |
| **RegularTime** | Worked time classified as ordinary contractual working time. | |
| **FlexTime** | Derived positive or negative time affecting the employee’s flex balance. | **Accounting:** Balance-affecting posting |
| **Overtime** | Worked time classified as overtime under the applicable rules/agreement. | **Accounting:** Special transaction classification |
| **LegalNotice / Notice** | A derived warning or informational result indicating a rule firing or possible compliance issue. It does not alter the underlying registration. | **Git:** CI/check result |
| **Ledger** | The balance system containing time movements whose current saldo matters. It is downstream of work determination and classification. | **Accounting:** General ledger |
| **LedgerTransaction** | One atomic balance-changing event containing balanced postings. | **Accounting:** Journal entry |
| **Posting** | One side of a ledger transaction, such as `EmployeeFlex +00:30`. | **Accounting:** Debit/credit posting |
| **EmployeeFlex** | The employee-side balance account for flex time. | **Accounting:** Ledger account |
| **OrganisationControl** | The equal-and-opposite control account balancing employee flex movements. | **Accounting:** Control / contra account |
| **Saldo / Balance** | The current sum of ledger postings. It is derived, not authoritative source data. | **Accounting:** Account balance / trial balance |
| **Adjustment** | A proposed or resolved balance-affecting change, represented as a new fact rather than mutation of history. | **Accounting:** Adjusting journal entry; **Git:** corrective commit |
| **Workflow** | Configurable process governing who may register, correct, acknowledge, approve, dispute, or resolve work records. | **Git:** Review/approval process |
| **Approval** | Workflow state indicating review or acceptance according to configured policy. It does not change whether the work happened. | **Git:** PR approval |
| **Dispute** | Workflow state indicating disagreement with a registration, correction, or resulting interpretation while preserving all assertions. | **Git:** Review requesting changes / unresolved review thread |
| **Resolution** | A workflow decision determining which assertion or interpretation becomes effective without deleting the competing history. | **Git:** Review resolution / accepted follow-up commit |
| **EffectiveState** | The current authoritative interpretation of a WorkDay after applying registrations, corrections, workflow, and configuration. It is derived from history. | **Git:** Current branch contents |
| **Projection** | Disposable/read-optimized representation derived from source history, evaluation and ledger data. | **Git:** Checked-out tree / generated view; **Accounting:** report |
| **Report** | Human-readable projection across WorkDays, periods, classifications and ledger balances. It must be drillable back to provenance. | **Git:** `main` viewed at a point in time; **Accounting:** financial statement/report |
| **Allocation** | Post-registration description of what determined worked time was spent on. It does not establish whether work occurred. | **Accounting:** Cost allocation |
| **WorkAllocation** | A slice or annotation assigning worked time to dimensions such as project, customer, feature, task or category. | **Accounting:** Cost-centre/project allocation |
| **Dimension** | A configurable categorisation axis such as project, customer, feature, cost centre, task or activity. | **Accounting:** Analytical dimension / cost centre |
| **Tag** | A non-exclusive annotation on worked time. Multiple tags may overlap. | **Git:** Label/tag, loosely |
| **Project** | A classification/allocation dimension applied after worked time has been determined. | **Accounting:** Project/cost object |
| **Customer** | A classification/allocation dimension describing which customer the worked time relates to. | **Accounting:** Customer/cost object |
| **Feature / Task / Category** | Additional configurable allocation dimensions applied to determined worked time. | **Accounting:** Analytical dimensions |
| **AuditTrail** | The preserved chain from registration through configuration, evaluation, classification, ledger consequence, workflow and reporting. | **Git:** Full commit/review history; **Accounting:** audit trail |
| **Provenance** | Metadata explaining who created a record, why, when, under which configuration, and how later derived values came from it. | **Git:** Author, commit history, parentage; **Accounting:** source-document trail |
| **CorrectionChain** | The sequence linking an original registration to later corrections without removing any prior record. | **Git:** Series of corrective commits |
| **EffectiveRegistration** | The registration currently selected by workflow/rules as operative for evaluation. Competing historical registrations remain preserved. | **Git:** Version currently represented in the branch |
| **SettlementPeriod** | A reporting/accounting period over which balances may be reviewed, approved, carried or normalised. | **Accounting:** Accounting period |
| **Normalization** | A configured balance adjustment, such as flex carry-cap handling, performed explicitly through ledger movement rather than rewriting history. | **Accounting:** Period-end adjusting entry |
| **FinancialCompensationMarker** | A classification indicating that some worked time requires financial treatment elsewhere, without turning Hours into payroll. | **Accounting:** Posting flag / subledger handoff |
| **Integration** | External source of registrations, such as a stamping machine or another timekeeping system. | **Git:** Bot/automation author |
| **System** | Internal actor for deterministic derived activity where a non-human source is needed. It should not impersonate employee/employer provenance. | **Git:** CI/bot account |
| **HistoricalReplay** | Re-running historical registrations through the exact configuration snapshot that originally applied. | **Git:** Checkout/rebuild from an old commit; **Accounting:** Reconstructing the ledger from journals |

The core vocabulary is shaping into three distinct families:

**Reality:** `WorkDay`, `WorkRegistration`, `WorkInterval`, `Source`, `Intent`.

**Interpretation:** `Configuration`, `Resolution`, `Evaluation`, `Classifier`, `Classification`, `Workflow`.

**Consequences and views:** `LedgerTransaction`, `Posting`, `Balance`, `Allocation`, `Projection`, `Report`.

That separation is probably worth preserving explicitly in the codebase too.

Yes. I think that is the better abstraction.

`ICalendarRule` should not itself mutate a `DayShape`. It should answer:

> **For this date, what `DayRule`s do I contribute?**

And critically, calendars should be **sparse**. No rule means “I have nothing to say about this day”, not “normal workday”.

Something like:

```csharp
public interface ICalendarRule
{
    IEnumerable<DayRule> GetRules(DateOnly date);
}
```

Or, since this should normally be tiny:

```csharp
public interface ICalendarRule
{
    ImmutableArray<DayRule> GetRules(DateOnly date);
}
```

Then `DayRule` is the actual semantic contribution:

```csharp
public abstract record DayRule;
```

with concepts such as:

```csharp
public sealed record WorkingDayRule(bool IsWorkingDay) : DayRule;

public sealed record ExpectedWorkRule(TimeSpan Duration) : DayRule;

public sealed record PaidTimeOffRule(TimeSpan Duration) : DayRule;

public sealed record CoreHoursRule(TimeRange Range) : DayRule;

public sealed record FlexEnvelopeRule(TimeRange Range) : DayRule;

public sealed record RoutineIntervalRule(TimeRange Range) : DayRule;

public sealed record BreakRule(TimeRange Range) : DayRule;
```

Now your **Norway calendar** can contribute only what Norway actually defines.

Conceptually:

```text
Saturday
    WorkingDay(false)

Sunday
    WorkingDay(false)

17 May
    WorkingDay(false)
    PaidTimeOff(7:30)

25 December
    WorkingDay(false)
    PaidTimeOff(7:30)
```

A gas station calendar layered above it can simply contribute:

```text
Saturday
    WorkingDay(true)

Sunday
    WorkingDay(true)
```

It doesn't need to reproduce the entire Norwegian calendar.

And Christmas Day can still remain:

```text
Norway:
    WorkingDay(false)
    PaidTimeOff(7:30)

GasStation:
    WorkingDay(true)
```

Now the final shape can legitimately become:

```text
WorkingDay:      true
ExpectedWork:    according to employment schedule
PaidTimeOff:     maybe still relevant according to applicable policy
Holiday:         true
```

Which is exactly the interesting case for a 24/7 business: **the public holiday didn't cease to exist just because this employee works on it**.

That means `DayRule`s should generally target **individual dimensions**, rather than having:

```csharp
new DayRule
{
    IsWorkingDay = true,
    ExpectedHours = ...,
    IsHoliday = ...
}
```

because whole-object replacement would destroy information contributed by lower calendars.

---

## Then the hierarchy becomes very clean

I'd see:

```text
ICalendarRule
    projects zero-or-more DayRules for a date

ICalendar
    contains ordered ICalendarRules

CalendarStack
    contains ordered ICalendars

DayShapeResolver
    folds all resulting DayRules into DayShape
```

Perhaps:

```csharp
public interface ICalendar
{
    IEnumerable<DayRule> GetRules(DateOnly date);
}
```

and:

```csharp
public sealed class Calendar(
    ImmutableArray<ICalendarRule> rules) : ICalendar
{
    public IEnumerable<DayRule> GetRules(DateOnly date) =>
        rules.SelectMany(x => x.GetRules(date));
}
```

Then:

```csharp
public sealed class CalendarStack(
    ImmutableArray<ICalendar> calendars)
{
    public DayShape GetDayShape(DateOnly date) =>
        DayShape.Resolve(
            date,
            calendars.SelectMany(x => x.GetRules(date)));
}
```

Although I'd eventually return a resolution containing provenance rather than naked `DayShape`.

---

## Recurrence rules become natural

This avoids having a rule per date.

You can have:

```csharp
public sealed record DaysOfWeekRule(
    ImmutableHashSet<DayOfWeek> Days,
    DayRule Rule) : ICalendarRule
{
    public ImmutableArray<DayRule> GetRules(DateOnly date) =>
        Days.Contains(date.DayOfWeek)
            ? [Rule]
            : [];
}
```

So Norway's baseline can contain:

```text
Sat/Sun → NonWorkingDay
```

while the gas-station overlay contains:

```text
Sat/Sun → WorkingDay
```

Similarly:

```text
FixedDateRule
RecurringDateRule
DateRangeRule
NthWeekdayRule
PublicHolidayRule
EasterRelativeRule
```

but those are **selection mechanisms**, not business meaning.

That's an important distinction:

```text
"Every Sunday"
```

is an `ICalendarRule` selector.

```text
"Non-working day"
```

is a `DayRule`.

That separation feels right.

---

## And your corporate three-days example becomes trivial

Norwegian baseline:

```text
Norway Calendar
├─ weekends
├─ statutory holidays
└─ whatever national baseline applies
```

Company:

```text
Corporate Calendar
├─ Christmas Eve     → PaidTimeOff + NonWorking
├─ New Year's Eve    → PaidTimeOff + NonWorking
└─ Wednesday before Easter → PaidTimeOff + NonWorking
```

Employment:

```text
Employment Calendar
├─ Normal working day → ExpectedWork 7:30
├─ Flex envelope      → 07:00–17:00
└─ Core hours         → 09:00–15:00
```

Frank:

```text
Routine Calendar
├─ Expected presence → 08:00–16:30
└─ Lunch             → 11:30–12:30
```

Each calendar is mostly **silent**.

That's the property I particularly like.

A calendar should say:

> “Here are the things I know that matter today.”

not:

> “Here is my complete opinion about today's entire shape.”

Then stacking remains compositional rather than replacement-based.

And yes, that gives you the exact useful behavior you identified: a 24/7 employer can override **weekends as working days** without accidentally deleting **Christmas being Christmas**.

Yes. **That turns the calendar stack from “configuration convenience” into an auditable rule engine.** The killer part is not merely knowing Christmas Day exists. It’s being able to answer *why this particular employee/day resolved the way it did*.

I’d make the day resolution carry both the final shape and the complete applied rule slice:

```csharp
public sealed record DayShapeResolution(
    DayShape Shape,
    ImmutableArray<AppliedDayRule> Rules);
```

Where each applied rule preserves provenance:

```csharp
public sealed record AppliedDayRule(
    string RuleId,
    string Source,
    string SourceVersion,
    string Calendar,
    int Precedence,
    DayRule Rule);
```

Then your gas-station Christmas example becomes wonderfully explicit:

```text
2026-12-25

Norway:
  PublicHoliday(ChristmasDay)
  WorkingDay(false)
  PaidTimeOff(7:30)

GasStationCalendar:
  WorkingDay(true)

EmploymentCalendar:
  ExpectedWork(7:30)

Resolved:
  IsHoliday       = true
  IsWorkingDay    = true
  ExpectedWork    = 7:30
  PaidTimeOff     = 7:30   // depending on agreement semantics

Rules:
  [all four contributing rules, in order]
```

So the UI can literally expose:

> **Why does this day look like this?**

and show the exact deterministic chain.

That’s excellent for:

- auditors
- employees disputing a result
- managers debugging configuration
- support
- regression tests
- explaining why two employees on the same date got different results

## Code-genning holidays is better than hiding a runtime dependency

I like your approach of consuming a holiday library/tool **during build or generation**, then emitting your own explicit rules.

Rather than runtime:

```text
Hours
→ HolidayLibrary
→ magic answer for 2026-12-25
```

you get generated source/config such as:

```text
Rule:
Id: norway.public-holiday.2026.christmas-day
Date: 2026-12-25
Kind: PublicHoliday
GeneratedFrom:
    Package: ...
    Version: ...
```

That gives you several advantages.

First, **reproducibility**. Updating the holiday package doesn’t silently reinterpret historical days.

Second, **diffability**. A package update that changes a holiday produces a source/generated-data diff you can review.

Third, **observability**. The final day can say exactly which generated rule fired.

Fourth, **zero runtime dependence** on whatever semantics the holiday package happens to expose.

The generated output effectively becomes a curated input to your domain.

## I’d preserve generator provenance too

Something like:

```text
Generated rule source
  Provider: PublicHoliday
  PackageVersion: 3.8.1
  GeneratedAtBuild: ...
  Jurisdiction: NO
  Year: 2027
```

Possibly also content hash / generator version.

Then if someone asks in 2029:

> Why was 17 May 2027 classified this way?

you can answer from persisted/generated rule identity rather than hoping the current NuGet package still produces the same result.

## And `Rules` belongs directly on the resolution

I agree strongly with this.

Not just:

```csharp
DayShape Shape
```

but:

```csharp
DayShape Shape
ImmutableArray<AppliedDayRule> Rules
```

because the rules are not debug metadata. They are part of the **explanation of the derived state**.

That means reporting can drill:

```text
Monthly report
→ Day
→ Resolved shape
→ Applied rules
→ Source calendar
→ Generated/source provenance
```

and then onward into:

```text
Registration
→ Evaluation
→ Classifications
→ Ledger postings
```

At that point the entire system has a consistent property:

> **Every derived number has a causal path back to explicit inputs and explicit rules.**

That is much more valuable than merely having “audit logging”.

You’re essentially building **explainable time accounting**. That phrase may actually be worth keeping around, because it captures the product property quite well.