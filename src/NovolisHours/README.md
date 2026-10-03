# Novolis Hours

Novolis Hours is a worktime traceability product. It records when work happened relative to when it was expected, keeps a flex-time saldo, exposes legal and agreement notices, and carries approvals and disputes as append-only evidence.

It does not calculate payroll, pay rates, tax, payment, or leave.

## Product model

Each employment combines four independently versioned concepts:

1. **Profile** — the broad working-day envelope, core hours, lunch, weekly/day expectations, and unmarked-surplus classification.
2. **Template** — default weekday expected intervals, such as 08:00–16:00.
3. **Settings** — individual employment fraction and optional expected-interval override.
4. **Records** — immutable actual presence, actual breaks, financial-compensation marks, comments, policy snapshot, and legal notices.

The standard Norwegian profile is explicit:

- working-day envelope: 07:00–17:00
- core coverage: 09:00–15:00
- lunch: 11:30–12:00
- expected template: 08:00–16:00
- week/day expectation: 37.5 / 7.5 hours

For 09:30–21:45 with a 30-minute break, the recorded actual work is 11.75 hours, expected work is 7.5 hours, and the non-overtime flex movement is +4.25 hours. The record is retained even though it produces envelope, core-coverage, and daily-limit notices.

## Ledger and adjustments

There are only two duration accounts:

- employee flex saldo
- organisation control

Every flex movement creates equal and opposite duration postings. Classifications, comments, legal citations, and adjustment reasons explain a movement; they do not create additional accounts. Financial-compensation marks identify hours that need financial treatment elsewhere, without calculating any money and without becoming a separate flex account.

Manager adjustments are proposed facts. They only reach the duration ledger after employee acceptance. A dispute preserves the proposed adjustment, marks an anomaly, and routes it to HR or Higher. It never deletes records or prevents later registration.

Positive flex normalization is represented as a proposed adjustment with the `FlexNormalization` reason. It is expressly not a payment.

## Approval and legal notices

Monthly approval periods have independent business-day clocks for employee submission, manager review, and HR/Higher resolution. A missed date appends a visible anomaly; it never locks the period or prevents new worktime facts.

Legal presets provide source citations, explicit review states, and configurable overtime-agreement messages. They are informational boundaries, not hour-rejection gates:

- Norway private flex starter
- Norway government-handbook starter
- Belgium, England, France, Poland, and Finland starter presets requiring local review

All included presets start as `Draft`; a tenant must complete country-specific legal and collective-agreement review before production activation. The `Novolis.Time.Worktime.Legal` package is not a substitute for that advice.

## Hosts

- `Novolis.Hours.Server` — ASP.NET Core API, SignalR hub, JSON persistence, SPA delivery, workflow scheduling, and HTML report export.
- `Novolis.Hours.Cli` — prints the listening URL after starting the host.
- `Novolis.Hours.Avalonia` — shared-profile native client foundation.
- `Novolis.Hours.Maui` — shared-profile mobile and Windows client foundation.

The local demonstration host intentionally supports `admin/admin` so a fresh JSON-backed run can be tried in one command. Set `Hours__EnableDemoAdminCredentials=false` for any deployment and provision real accounts through Novolis Security.

## Dependencies

External:

- ASP.NET Core and SignalR
- Avalonia
- .NET MAUI
- PublicHoliday through `Novolis.Time.Calendar.PublicHoliday`

Internal:

- `Novolis.Time`, `Calendar`, `Calendar.PublicHoliday`, `Week`, `Worktime`, and `Worktime.Legal`
- `Novolis.Storage.Json` and `Novolis.Storage.InMemory`
- `Novolis.Security.Authentication` and its repository adapters
- `Novolis.WorkflowEngine`
- `Novolis.Avalonia.GraphicalProfile` and `Novolis.Maui.GraphicalProfile`

## Verification

The feature test project uses a real `WebApplication` and `TestServer`, real in-memory journal/storage providers, cookies, antiforgery, SignalR registration, and the actual endpoint mappings. It has no mocks.

Run the product locally:

```powershell
dotnet run --project src/NovolisHours/Novolis.Hours.Cli/Novolis.Hours.Cli.csproj -p:NovolisUseProjectReferences=true
```

Run the feature suite:

```powershell
dotnet test tests/Novolis.Hours.FeatureTests/Novolis.Hours.FeatureTests.csproj -p:NovolisUseProjectReferences=true
```

## Container

`Dockerfile` builds the server using an authenticated NuGet configuration mounted as a BuildKit secret; no GitHub Packages credential is copied into the image. The runtime stores JSON data at `/data` and disables the local demo account by default.

`compose.yaml` mounts that data directory as the `novolis-hours-data` volume.
