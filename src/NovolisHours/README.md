# Novolis Hours

Novolis Hours is a worktime traceability product. It records when work happened relative to when it was expected, keeps a flex-time saldo, exposes legal and agreement notices, and carries approvals and disputes as append-only evidence.

It does not calculate payroll, pay rates, tax, payment, or leave.

## Product model

Each employment combines four independently versioned concepts:

1. **Profile** — the broad working-day envelope, core hours, lunch, weekly/day expectations, and unmarked-surplus classification.
2. **Template** — default weekday expected intervals, such as 08:00–16:00.
3. **Settings** — individual employment fraction and optional expected-interval override.
4. **Records** — immutable actual presence, actual breaks, financial-compensation marks, comments, policy snapshot, and legal notices.

The core rewrite keeps the same user-facing simplicity while separating fact
from meaning:

- `WorkRegistration` is an immutable assertion. `WorkedAsScheduled` has no
  invented clock observation; manual registrations and corrections carry
  absolute `DateTimeOffset` intervals.
- `WorkDay` is keyed by employee and nominal local date, so a split record may
  cross midnight without being silently split into unrelated facts.
- `DayShape` is the resolved expected date: working-day status, expected work,
  core hours, routine intervals, tags, timezone, and ordered applied-rule
  provenance.
- Dimensions such as `Project`, `Customer`, `TimeType`, and `Billability`
  describe resolved work after registration. Missing project allocation never
  invalidates actual work.
- The duration ledger has only `EmployeeFlex` and `OrganisationControl`.
  Explicitly mapped derived Dimension values create balanced postings; positive
  saldo normalization never means payment.
- Review actions, corrections, disputes, compliance indicators, and reports are
  projections over append-only facts. An overdue stage is an anomaly, not a
  lockout.

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

- `Novolis.Hours.Server` — ASP.NET Core API, SignalR hub, selectable JSON/Azure Table persistence, SPA delivery, workflow scheduling, and HTML report export.
- `Novolis.Hours.Cli` — prints the listening URL after starting the host.
- `Novolis.Hours.Client` — authenticated cookie and antiforgery API client shared by native hosts.
- `Novolis.Hours.Client.Avalonia` — shared-profile native client with real service sign-in, summary loading, and an opt-in UI agent hook.
- `Novolis.Hours.Client.Maui` — shared-profile mobile and Windows client with real service sign-in, summary loading, and an opt-in UI agent hook.
- `Novolis.Hours.Client.Blazor` — standalone Blazor WebAssembly client using the same protected API and browser credentials.
- `Novolis.Hours.AppHost` — Aspire process composition for the server, Azurite Table Storage, and all three clients. The clients are explicit-start resources and receive the server's managed HTTPS endpoint through an endpoint reference.

The secured v2 HTTP surface exposes `api/v2/work-registrations` and
`api/v2/employees/{employeeId}/work-registrations`. It uses the same authenticated
cookie, antiforgery, authorization, and rate-limited login boundaries as the
legacy compatibility surface. `api/v2/employees/{employeeId}/audit` exposes
event references for drill-down without placing domain entities on the wire.
SignalR invalidations retain the event identity and projection family so clients
can refresh a traceable view.

The local demonstration host supports `admin/admin` only when explicitly started with `serve --demo`, so a fresh JSON-backed run can be tried in one command. The default is secure: a non-demo host starts only when an administrator already exists or `Hours__InitialAdministratorPassword` is supplied through secure configuration. Production bootstrap checks passwords through the Novolis Security breach-checking adapter.

## Dependencies

External:

- ASP.NET Core and SignalR
- Avalonia
- .NET MAUI
- PublicHoliday through `Novolis.Time.Calendar.PublicHoliday`

Internal:

- `Novolis.Time`, `Calendar`, `Calendar.PublicHoliday`, `Week`, `Worktime`, and `Worktime.Legal`
- `Novolis.Storage.Json` and `Novolis.Storage.InMemory`
- `Novolis.Storage.AzureTables` and `Azure.Data.Tables` for Azure Table Storage/Azurite persistence
- `Novolis.Security.Authentication`, storage adapters, and `Novolis.Security.HaveIBeenPwned` for non-demo bootstrap checks
- `Novolis.WorkflowEngine`
- `Novolis.Markup.Html` for traceable, escaped HTML exports
- `Novolis.Avalonia.GraphicalProfile` and `Novolis.Maui.GraphicalProfile`
- `Novolis.Avalonia.Agent` and `Novolis.Maui.Agent` for controllable native UI surfaces

Related registration and ledger facts are committed through an atomic journal
batch envelope. JSON, in-memory, Azure Tables, and Azurite readers expand that
envelope back into the original append-only event stream, so replay and audit
do not expose storage implementation details.

## Verification

The feature test project uses a real `WebApplication` and `TestServer`, real in-memory journal/storage providers, cookies, antiforgery, SignalR channel projections, and the actual endpoint mappings. It also exercises the reusable native HTTP client against that same host. It has no mocks. The domain suite additionally covers calendars and DST, holiday-generation provenance, sparse Dimensions, append-only allocation corrections, balanced ledger replay, review deadlines/disputes, non-blocking compliance, health concerns, and temporal business-pressure attribution.

Run the product locally:

```powershell
dotnet run --project src/NovolisHours/Novolis.Hours.Cli/Novolis.Hours.Cli.csproj -p:NovolisUseProjectReferences=true -- serve --demo
```

Run the feature suite:

```powershell
dotnet test tests/Novolis.Hours.FeatureTests/Novolis.Hours.FeatureTests.csproj -p:NovolisUseProjectReferences=true
```

Run the Podman-backed Aspire smoke test explicitly:

```powershell
$env:NOVOLIS_HOURS_RUN_PODMAN_TESTS = "1"
dotnet test tests/Novolis.Hours.AspireTests/Novolis.Hours.AspireTests.csproj -p:NovolisUseProjectReferences=true
```

## Aspire host

The supported local composition is the project-based Aspire AppHost. It starts
exactly one `Novolis.Hours.Server` process and one persistent Azurite Table
Storage container under Podman. The server connects through the Azurite
development-storage connection string and exposes `/health/ready` only after
the Table service is reachable. The standalone Blazor client is available as a
separate HTTPS resource.

The AppHost forces `ASPIRE_CONTAINER_RUNTIME=podman`, keeps the bootstrap
administrator password as a secret parameter, disables demo credentials, and
wires `/health/ready` into resource readiness. The server exports structured
logs, traces, and metrics to the Aspire dashboard when
`OTEL_EXPORTER_OTLP_ENDPOINT` is supplied; direct CLI runs retain local
structured logging and in-process Activities/Meters without requiring a
collector. Set `NOVOLIS_AVALONIA_AGENT=1` or `NOVOLIS_MAUI_AGENT=1` to attach
the native UI agent surfaces; Aspire enables both for its client resources.
