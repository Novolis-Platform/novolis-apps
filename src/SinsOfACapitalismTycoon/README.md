# Sins of a Capitalism Tycoon

Headless / Avalonia dogfood app for the bounded-minimum economy (`Novolis.Economy.Core`).

- **headless** — agent/CI entrypoint: run N periods, print the simulation report, exit
- **avalonia** — playtest shell: same report in a simple window

## Scenarios (`--scenario`)

| Name | What binds |
|------|------------|
| `logistics_bind` (default) | Narrow lane / mine logistics — ore piles at mine, factory stockouts |
| `baseline` | Generous lane — conservation / most periods produce |
| `working_capital` | Thin factory cash + expensive ore; credit draw when illiquid |
| `credit_cycle` | Committed facility + capacity expansion after draw |
| `fiscal_stress` | State treasury drains under high transfers / low tax |
| `shock` | Mid-horizon production loss + insurance |

Hauls require **cash + logistics residual** (no god-mode shipping).

## Run

```powershell
cd novolis-apps

# Default interesting showcase
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --periods 300 --seed 42

# Named scenarios
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --scenario logistics_bind --periods 300 --seed 42 --quiet
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --scenario working_capital --periods 200 --seed 1 --quiet
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --scenario baseline --periods 100 --seed 42 --quiet
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --scenario credit_cycle --periods 400 --seed 42 --quiet
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --scenario fiscal_stress --periods 200 --seed 42 --quiet
dotnet run --project src/SinsOfACapitalismTycoon -- --mode headless --scenario shock --periods 300 --seed 42 --quiet

# Playtest UI
dotnet run --project src/SinsOfACapitalismTycoon -- --mode avalonia --scenario logistics_bind --periods 200
```

### Expected drama signals

- **logistics_bind:** `periods without production > 0`, rising `peak mine ore stockpile`, factory stockouts
- **working_capital:** `credit draws > 0` and/or delinquent sightings; haul gaps when cash thin
- **baseline:** cash peak≈trough; most periods factory-produce (`periods without production` near 0)
- **fiscal_stress:** state cash trough (low aggregate cash); demand softens as transfers stall
- **shock:** `shocks injected 1`; claims/premiums or delinquency spike

Drama `periods without production` counts periods with **no factory widget runs** (mine ore alone does not count).

### Args

| Arg | Default | Meaning |
|-----|---------|---------|
| `--mode headless\|avalonia` | `headless` | Shell |
| `--scenario NAME` | `logistics_bind` | Scenario pack |
| `--periods N` | `100` | Core periods |
| `--seed U` | `42` | Deterministic seed |
| `--log-every N` | `0` (auto) | Period-log sample rate |
| `--quiet` / `-q` | off | Hide stderr progress |

## Local Core source iteration

```powershell
dotnet build src/SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true
```
