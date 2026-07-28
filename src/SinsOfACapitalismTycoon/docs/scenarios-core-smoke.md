# Core smoke scenarios

Retained for bounded-minimum regression. Not the product surface.

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --engine core --scenario logistics_bind --periods 300 --seed 42 --quiet
```

| Scenario | Bind |
|----------|------|
| `logistics_bind` | Narrow lane / mine logistics |
| `baseline` | Conservation / production most periods |
| `working_capital` | Thin cash + credit draw |
| `credit_cycle` | Facility + capacity expand |
| `fiscal_stress` | State treasury drain |
| `shock` | Mid-horizon loss + insurance |

Hauls require cash + logistics residual. Drama counts **factory** production gaps.
Reports include Core books (Flows, Credit, Obligations, Stress, projected Accounts).
