# Novolis Hours
## Acceptance Scenarios and Verification Regime

These scenarios define the expected behaviour of the rewritten Novolis Hours product.

They are not isolated domain-unit tests.

Each scenario must be verifiable through:

1. **automated feature tests** exercising the real application composition as far as practical;
2. **manual browser testing** against the real Blazor website running through the Aspire AppHost.

The purpose is to prove that the product works as a multi-user system, not merely that individual classes return expected values.

---

# 1. Test environment

The acceptance environment must run the actual Novolis Hours composition through Aspire.

At minimum:

```text
Aspire AppHost
├── hours-server (`Novolis.Hours.Server`)
├── Blazor website
├── configured persistence
└── supporting infrastructure required by Hours
```

Use existing authentication and infrastructure rather than inventing test-only replacements where avoidable.

Feature tests should continue the existing philosophy:

- real ASP.NET Core application;
- real endpoint mappings;
- real authentication/session behaviour;
- real domain services;
- real rule resolution;
- real projections;
- in-memory or local development persistence where appropriate;
- no mocks for internal Hours components.

External dependencies may use appropriate fakes where running the real dependency would make the test unreasonable.

---

# 2. Multi-user requirement

Scenarios must involve distinct authenticated users where roles or responsibilities differ.

Useful fixture identities include:

```text
ada          Employee
bob          Employee

alice        Manager, Team A, approval level 1
charlie      Manager, Division North, approval level 2

helen        HR
audrey       Auditor

system       System actor, organisation `hours-platform`, reads and stamps every customer
```

Where relevant, different employees should also have different:

- countries;
- calendar stacks;
- employment rules;
- Dimensions;
- workflow policies.

The tests must prove that one employee's rules, approvals, balances and reports do not bleed into another employee's data.

---

# 3. Normal Norwegian flex worker

## Configuration

Ada works in Norway.

Calendar stack:

```text
Norway
Corporate Calendar
Employment
Ada Routine
```

Routine:

```text
08:00 → 11:30
12:30 → 16:30
```

Expected work:

```text
7:30
```

Flex envelope:

```text
07:00 → 17:00
```

Core hours:

```text
09:00 → 15:00
```

## Action

Ada signs into the Blazor website.

The homepage shows today's resolved routine.

Ada selects:

```text
Worked as scheduled
```

## Expected result

One immutable registration is created with:

```text
Source = Employee
Intent = WorkedAsScheduled
```

The registration does not pretend to contain observed clock timestamps.

Resolution produces the configured routine intervals.

Expected:

```text
7:30
```

Actual:

```text
7:30
```

Flex:

```text
0
```

No compliance indicators exist.

Ada can drill into the WorkDay and see:

- her registration;
- resolved intervals;
- complete DayShape;
- applied calendar rules;
- source configuration.

## Manual verification

Perform the same action through the Blazor website.

Refresh the page.

Sign out and back in.

The WorkDay must remain correct.

---

# 4. Honest split day

## Configuration

Ada has the same Norwegian routine.

## Action

Instead of `WorkedAsScheduled`, Ada manually registers:

```text
08:00 → 12:00
16:00 → 19:30
```

She optionally writes:

```text
Dentist and family appointment.
```

## Expected result

The system records exactly two work intervals.

Actual work:

```text
7:30
```

The four-hour gap remains visible.

The UI may show routine differences such as:

```text
Worked in two intervals.
Worked later than normal routine.
```

But:

- no anomaly is created merely because of the gap;
- no manager workflow starts;
- no HR workflow starts;
- no compliance violation is inferred from honesty;
- the note is context, not permission.

If a genuine compliance rule independently applies, it may produce an indicator.

## Critical assertion

A large gap is not inherently suspicious.

The product must make truthful registration easier than falsifying a continuous working day.

---

# 5. Norwegian WorkDay crossing midnight

## Action

Bob manually registers:

```text
Nominal date:
2026-10-04

18:00 → 02:00 next day
```

## Expected result

Exactly one logical WorkDay exists.

Actual work:

```text
8:00
```

The interval remains one interval.

Calendar-date reports may attribute portions to:

```text
2026-10-04
2026-10-05
```

without manufacturing a second WorkDay.

Night-work Dimensions and compliance indicators may be derived where configured.

The audit trail always leads back to the original single registration.

---

# 6. Norwegian gas station on Christmas Day

## Configuration

Bob works at a 24/7 gas station.

National calendar contributes:

```text
Christmas Day
    PublicHoliday

Sunday
    NonWorkingDay
```

Employer calendar contributes:

```text
Saturday
    WorkingDay

Sunday
    WorkingDay
```

Employment contributes:

```text
ExpectedWork
    7:30
```

Agreement contributes:

```text
Christmas Day
    PaidEntitlement
    7:30
```

## Expected DayShape

```text
WorkingDay       true
PublicHoliday    true
ExpectedWork     7:30
PaidEntitlement  7:30
```

## Action

Bob opens Christmas Day in the Blazor website.

## Expected result

The UI can display:

```text
Why does this day look like this?
```

and show the complete rule sequence.

The gas-station rule must not erase Christmas.

Christmas must not erase the gas-station working-day rule.

The exact effective result follows deterministic ordering and Dimension/accounting rules.

This is a flagship test of calendar composition.

---

# 7. Corporate Christmas Eve paid day

## Configuration

Ada's company adds:

```text
Christmas Eve
    NonWorkingDay
    ExpectedWork 0
    PaidEntitlement 7:30
```

## Case A: Ada does not work

No fake `WorkRegistration` is required.

The WorkDay shape explains:

```text
ExpectedWork     0
PaidEntitlement  7:30
```

The system must not require Ada to enter seven-and-a-half fictional worked hours merely to satisfy accounting.

## Case B: Ada actually works

Ada registers:

```text
09:00 → 12:00
```

The actual three hours are preserved.

They can receive relevant Dimensions such as:

```text
HolidayWork
Overtime
Flex
```

according to configuration.

Paid entitlement and actual work remain separate concepts.

---

# 8. Different national calendars for different employees

This scenario proves that international policy is configuration, not globally hard-coded behaviour.

## Users

```text
ada      Norway
pierre   France
anna     Poland
liisa    Finland
```

Each receives an appropriate national base calendar plus their organisation/employment layers.

The exact production rules require locally reviewed configuration. The test fixtures exist to prove composition and isolation rather than to declare the software itself authoritative on national labour law.

## Example dates

Choose fixture dates where national calendars deliberately differ.

For example:

```text
Date X
Norway      ordinary working day
France      public holiday

Date Y
Poland      public holiday
Finland     ordinary working day
```

## Expected result

Opening the same date for each user produces different DayShapes where their stacks differ.

Every DayShape explains its own applied rules.

Changing the French calendar configuration cannot change Ada's Norwegian result.

National rules are not selected from the browser locale.

They come from the employee's effective configuration.

---

# 9. Mid-year national or agreement rule change

## Configuration

Pierre has:

```text
Agreement v1
Effective through 2026-05-14
```

and:

```text
Agreement v2
Effective from 2026-05-15
```

The versions differ in one relevant WorkDay rule.

## Action

Pierre records equivalent work on:

```text
2026-05-14
2026-05-15
```

## Expected result

Each day uses the correct effective rule stack.

Changing or publishing v2 must not rewrite the historical explanation of May 14.

Both days expose the exact configuration/rules used.

A later software deployment must reproduce the historical results.

---

# 10. Customer and project Dimensions

## Configuration

Ada's organisation defines:

```text
Customer
Project
Billability
TimeType
```

Department configuration additionally contributes:

```text
CostCentre
```

Ada works:

```text
08:00 → 12:00
13:00 → 17:00
```

She then paints:

```text
08:00 → 10:30
Customer = ACME
Project = Phoenix

10:30 → 12:00
Customer = ACME
Project = Support

13:00 → 17:00
Project = Internal Platform
```

Derived rules classify part of the day as flex/overtime.

## Expected result

Actual working time remains independent of project allocation.

Dimension configuration stacks correctly.

Customer/project allocations cannot create additional work.

Missing allocation cannot erase work.

Reports can aggregate by:

```text
Customer
Project
Billability
TimeType
CostCentre
```

and drill back to the underlying intervals.

---

# 11. Flex ledger and later correction

## Initial state

Ada has:

```text
Expected 7:30
Actual   8:15
```

The evaluation derives:

```text
FlexCredit +00:45
```

## Expected ledger

```text
EmployeeFlex          +00:45
OrganisationControl   -00:45
```

The transaction balances to zero.

## Correction

Ada later discovers she accidentally included 15 minutes that she did not work.

She creates a correction.

The correct flex result is:

```text
+00:30
```

## Expected result

The original registration remains.

The original ledger transaction remains.

A new correction transaction records:

```text
EmployeeFlex          -00:15
OrganisationControl   +00:15
```

Current saldo contribution:

```text
+00:30
```

The browser audit view exposes both registrations and both transactions.

No update-in-place is permitted.

---

# 12. Employee → manager → manager → HR workflow

This deliberately exercises a less trivial hierarchy.

## Responsibilities

```text
Ada
    Employee

Alice
    Manager
    Team A
    Level 1

Charlie
    Manager
    Division North
    Level 2

Helen
    HR
```

Workflow:

```text
Employee submits
→ Manager level 1 approves
→ Manager level 2 approves
→ HR final approval
```

## Action

Ada submits October.

Alice approves.

Charlie approves.

Helen gives final approval.

## Expected result

Each action is separately recorded with:

- actor;
- role/responsibility;
- approval level;
- timestamp;
- optional comment.

There is no `ManagerLevel2` role.

Current status is a projection of review actions.

All four users see only the reports and actions appropriate to their scope.

---

# 13. Employer-entered time with employee acknowledgement

This tests a very different workplace model.

## Workflow

```text
Employer/Manager enters time
→ Employee acknowledges
```

Employees are not permitted to create their own initial registration.

## Action

Alice enters Bob's work:

```text
08:00 → 16:00
```

Bob signs in.

He can inspect:

- registration;
- source = Employer;
- actual intervals;
- DayShape;
- Dimensions;
- relevant rules.

Bob acknowledges it.

## Expected result

Employer entry is valid.

Employee acknowledgement is a separate review action.

The domain must not falsely attribute the registration to Bob.

---

# 14. Employer-entered time disputed by employee

Using the previous workflow:

Alice records:

```text
08:00 → 15:30
```

Bob says:

```text
I worked until 16:30.
```

Bob disputes the registration and provides his assertion.

## Expected result

The system retains:

```text
Employer assertion
08:00 → 15:30

Employee assertion
08:00 → 16:30

Employee dispute
```

No assertion disappears.

Helen later resolves according to configured workflow.

Resolution is another recorded action.

The final effective state can be projected while preserving the entire disagreement.

This scenario must be tested using separate browser sessions/users.

---

# 15. Stamping-machine workflow

## Configuration

Work registration comes primarily from an integration.

```text
Source = Integration
```

Employee may:

```text
view
request/correct according to workflow
```

## Input

Stamping integration produces:

```text
07:58 → 12:01
12:29 → 16:07
```

## Expected result

The system records the exact integration assertion.

The UI identifies its source.

The employee must not be shown as its author.

If Ada creates a correction:

```text
08:00 → 12:00
12:30 → 16:00
```

the correction is separately attributed to Ada.

The integration record remains.

The audit timeline clearly shows both.

---

# 16. Correction after final approval

## Given

October has already been approved by:

```text
Ada
Alice
Charlie
Helen
```

## Action

Ada discovers a genuine mistake and appends a correction to October.

## Expected result

The system accepts the correction.

The previously approved period is not locked against truth.

Existing approvals remain in history.

The current review projection says, in effect:

```text
Previously approved
Changed after approval
Review required according to workflow
```

Required review steps become outstanding again.

No old review action is deleted.

---

# 17. Compliance concern without punishment workflow

## Action

Ada truthfully registers an unusually long day:

```text
07:00 → 22:00
```

with appropriate real gaps if any.

## Expected result

Registration succeeds.

Compliance rules may produce indicators such as:

```text
Daily working duration above configured threshold.
Daily rest may be below configured threshold.
```

They include rule provenance.

They do not:

- reject the registration;
- shorten the registration;
- create a disciplinary case;
- automatically notify HR as a misconduct escalation;
- label Ada as violating company rules.

The auditor and authorised management reports can surface the concern.

---

# 18. Organisation Health Concerns report

## Data

Create several employees across at least two teams.

Over several weeks:

```text
Team A
    repeated late-evening work
    several short-rest indicators

Team B
    ordinary working pattern
```

## Expected report

An authorised manager, HR user or auditor can see that Team A has a concentration of health-related indicators.

Useful result:

```text
Daily-rest concerns have increased in Team A over the selected period.
```

The report supports drill-down to:

```text
indicator
→ WorkDay
→ worked intervals
→ registration
→ applicable compliance rule
```

The default report must not produce:

```text
Top employees violating working-time rules
```

or similar employee-shaming rankings.

The purpose is organisational understanding.

---

# 19. Customer associated with excessive extra work

## Data

Across multiple employees and days:

```text
Customer ACME
    substantial evening/overtime work

Customer BETA
    mostly routine-hours work
```

Projects and customers are assigned through Dimensions.

Compliance and TimeType Dimensions identify overtime/out-of-hours work.

## Expected report

Management can see, for example:

```text
ACME
47:30 overtime-associated work

BETA
06:15 overtime-associated work
```

or:

```text
42% of recorded overtime in the selected period overlaps work allocated to ACME.
```

The report must calculate overlap from actual intervals where possible.

It must not claim:

```text
ACME caused employee health problems.
```

The result is correlation and workload attribution, not automatic causality.

Every aggregate must be drillable to contributing WorkDays.

---

# 20. Auditor cross-user investigation

## User

Audrey has:

```text
Role = Auditor
Scope = Norwegian organisation
```

## Action

Audrey signs into the Blazor website.

She selects:

```text
Team A
October
Flex +24:15
```

then drills down to:

```text
employee
→ WorkDay
→ registration
→ applied calendar rules
→ Dimensions
→ ledger posting
→ review actions
→ compliance indicators
```

## Expected result

Audrey can see everything required by her scope.

She cannot:

- register work;
- correct work;
- approve;
- resolve disputes;
- modify configuration.

Audit access must be read-only.

---

# 21. Scope isolation

## Configuration

Alice manages Team A.

Another manager manages Team B.

## Expected result

Alice may access reports and workflows for Team A.

She cannot gain access to Team B merely by changing:

```text
employee ID
team ID
URL
API request
```

This must be tested both:

- through UI navigation;
- directly against the relevant HTTP endpoint.

The browser hiding a button is not authorization.

---

# 22. Concurrent multi-user actions

The product is inherently multi-user.

## Scenario

Ada has submitted a period.

At roughly the same time:

- Alice opens it for review;
- Ada adds a legitimate correction;
- Alice attempts approval using the version she opened earlier.

## Expected result

The system must not silently approve a state different from what Alice reviewed.

Use the simplest concurrency mechanism supported by the existing architecture.

Acceptable outcomes include requiring Alice to reload/review the changed state.

The important invariant is:

> A review action must be attributable to the state being reviewed.

Do not solve this by preventing Ada from recording truth while Alice has the page open.

---

# 23. Configuration change while users are active

## Scenario

Ada has already registered Monday.

An administrator publishes a new employment/calendar configuration effective Tuesday.

Ada then registers Tuesday.

## Expected result

Monday retains its original configuration snapshot.

Tuesday uses the new effective stack.

The browser correctly explains each day using its own applicable rules.

No bulk rewrite of Monday occurs.

---

# 24. Browser refresh and replay

For representative scenarios above:

1. perform the action;
2. close the relevant page;
3. refresh;
4. sign out;
5. sign in again;
6. restart the Aspire composition where practical;
7. query the same data again.

The result must be reconstructed from authoritative state.

No correctness may depend on:

- browser memory;
- current SignalR connection;
- singleton process state;
- cached UI objects.

---

# 25. Automated feature-test regime

Every acceptance scenario should have automated coverage where technically practical.

Feature tests should test behaviours such as:

```text
start application
authenticate user A
perform action
authenticate user B
perform review
query resulting projection
assert domain outcome
```

Where HTTP/browser-level behaviour matters, exercise HTTP rather than directly calling the domain service.

Use direct domain tests additionally for combinatorial rule behaviour, but do not consider those a substitute for feature tests.

Particularly important feature-test targets:

- authentication boundaries;
- role/scope access;
- multi-user workflows;
- append-only corrections;
- cross-user approvals;
- dispute resolution;
- configuration effective dates;
- report drill-down;
- ledger results;
- international calendar isolation.

---

# 26. Manual browser verification regime

The rewrite is not accepted merely because automated tests pass.

The Blazor website must be run through Aspire and exercised manually.

At minimum manually verify:

```text
Employee registration
WorkedAsScheduled
Manual split day
Cross-midnight day
Calendar explanation
Dimension painting
Flex balance
Manager approval
Multi-level approval
Employee dispute
HR resolution
Auditor drill-down
Health Concerns report
Business-pressure report
```

Use multiple browser identities.

Prefer separate browser profiles/incognito sessions where necessary so that several actors can remain signed in simultaneously.

The tester should deliberately move between:

```text
employee
manager
HR
auditor
```

to ensure the same underlying facts appear appropriately from each perspective.

---

# 27. Fix-and-confirm regime

Finding a problem is part of the acceptance process.

A defect is not considered fixed when:

> The developer changed the code and the original test now looks plausible.

The required loop is:

```text
Reproduce
↓
Understand
↓
Add or extend automated test
↓
Fix
↓
Run focused test
↓
Run relevant feature suite
↓
Run complete Hours feature suite
↓
Run Aspire
↓
Reproduce manually in browser
↓
Confirm from every affected user role
```

If a browser-discovered defect had no automated coverage, the fix must normally add regression coverage.

---

# 28. Confirmation after fixes

Confirmation must check both the originally failing path and neighbouring behaviour.

Example:

A fix for:

```text
Employee correction after manager approval
```

must confirm at least:

```text
correction succeeds
old approval remains
new review becomes outstanding
ledger projection is correct
manager sees changed state
employee sees correction
auditor sees both histories
```

Do not stop at:

```text
HTTP 200
```

The feature's semantic outcome is what matters.

---

# 29. Regression rule

Every meaningful defect discovered during rewrite or manual acceptance should become one of:

- an existing scenario strengthened with an assertion;
- a new feature test;
- a focused domain test where the defect was purely deterministic rule logic.

This should gradually turn the acceptance suite into an executable product specification.

---

# 30. Final acceptance bar

The rewrite is ready when the team can start the Aspire AppHost, open the Blazor website, use several real test identities, and demonstrate the complete story:

```text
Different employees
with different national and employment calendars
register real working time
↓
calendar stacks explain expected days
↓
Dimensions classify and allocate real work
↓
double-entry postings explain balances
↓
managers and employees review it
↓
disagreements remain auditable
↓
HR resolves only where the configured workflow requires it
↓
auditors inspect provenance
↓
compliance indicators reveal working patterns
↓
Health Concerns reveals organisational patterns
↓
Customer/Project reports reveal where extra work accumulates
```

And throughout that demonstration:

> **No rule needs reality to lie in order for the system to make sense.**

---

# 31. Five-customer calendar stacking walkthrough

Use the Aspire AppHost (or the development server with `Hours__EnableAcceptanceSeed=true`) and the Blazor client. Sign in with the acceptance seed password for that run.

For each customer, open WorkDay and confirm the explanation list shows layer kind, rule id, and generated holiday provenance where a national holiday applies.

## nordvik-office (`ada`, `Europe/Oslo`)

| Date | Expected |
| --- | --- |
| 2026-10-01 | Working day, expected 7:30, envelope 07:00–17:00, routine 08:00–11:30 / 12:30–16:30 |
| 2026-10-04 | Weekend non-working, expected 0 |
| 2026-05-01 | PublicHoliday `Første mai`, not working, not French Labour Day |
| 2026-12-24 | Corporate paid day, expected 0, paid 7:30, Organisation `corporate-christmas-eve` |
| 2026-11-18 | Temporary override envelope 08:00–18:00, still expected 7:30 |

## nordvik-station (`bob`, `Europe/Oslo`)

| Date | Expected |
| --- | --- |
| 2026-10-04 | Sunday working, Organisation `always-open`, National weekend rule still listed |
| 2026-12-25 | Working + PublicHoliday `Første juledag` + paid 7:30 + expected 7:30. National provenance remains. |

## atelier-curie (`pierre`, `Europe/Paris`)

| Date | Expected |
| --- | --- |
| 2026-05-01 | PublicHoliday `Fête du Travail`, calendar version `agreement-v1` |
| 2026-05-14 | calendar version `agreement-v1` |
| 2026-05-15 | calendar version `agreement-v2`, envelope 08:30–16:30 |

## warsaw-settlement (`anna`, `Europe/Warsaw`)

| Date | Expected |
| --- | --- |
| 2026-05-03 | PublicHoliday Constitution Day, not working |

## helsinki-flex (`liisa`, `Europe/Helsinki`)

| Date | Expected |
| --- | --- |
| 2026-12-06 | PublicHoliday Itsenäisyyspäivä, not working |

Publishing a French configuration must not change ada's 2026-05-01 applied-rule sequence.

## Product chrome (week → day strip → layer chip → paint)

Use the Blazor client, not the retired server landing page.

1. Sign in with login and password only (service URL stays in Settings). Home is this week, defaulting to today.
2. Open a DayShape tile. The 06:00–20:00 strip shows envelope, core, routine, actual, and paint. Drag actual blocks to move or resize; paint mode writes only on actual work.
3. While the pointer is down, the layer rail stays live. Silent layers stay empty. Overflow past the envelope lights Organisation.
4. One commit action: *Worked as scheduled*. Evidence (snapshot ids, AppliedRules) stays folded.

# 32. Diverging review workflows

Each customer carries its own review policy. Opening a review shows that policy id and only those stages.

| Customer | People | Policy | Required stages |
| --- | --- | --- | --- |
| nordvik-office | ada, alice, charlie, helen | `cascading-approval` | Submit → Manager 1 → Manager 2 → HR |
| nordvik-station | bob, nina | `employer-acknowledged` | Employee Acknowledge |
| atelier-curie | pierre, marc | `single-approver` | Submit → one Manager |
| warsaw-settlement | anna, kasia | `employer-only` | Manager Approve (no employee submit) |
| helsinki-flex | liisa, helen | `employee-hr` | Submit → HR |

`system` is not a customer employee. It belongs to `hours-platform`, may read every WorkDay and report, and may stamp Integration assertions onto any customer. It does not approve human review stages.