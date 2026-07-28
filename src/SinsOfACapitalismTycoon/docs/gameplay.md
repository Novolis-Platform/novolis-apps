# Gameplay

Sins is a **headless tycoon watch**: you seed a universe, let it run for days, then
read the Spectre report like a CCA briefing that learned to swear politely. You do not
click starships. You *judge* them.

Tone models:

- Captain Varr — freedom is access, not comfort
  ([Margin Was Freedom](https://frankhaugen.github.io/galactic-confederation-review/articles/the-margin-was-the-freedom/))
- Calypso Cycle commerce weather — escrow, registry glass, Meridian boredom, ugly standby money
  ([calypso-canon.md](calypso-canon.md), [commerce-stack.md](commerce-stack.md))

## The loop (one sentence)

Firms dig, make, and sell; ST tramps and a mega-hauler move cargo under registry and
insurance; households eat **Final**; drama and wear try to kill the margin; you read
who stayed legible.

## What you are playing

| Layer | Your job |
|-------|----------|
| Seed | Pick `--seed` / `--days` / `--drama` — deterministic theater |
| Watch | Progress (unless `--quiet`); greppable `MILESTONE:` after |
| Judge | Spectre: money, registry, mega biography, agent last decisions |
| Compare | Same seed → same hash sins; new seed → new sins |

There is no high score. Better questions:

> **Which bill became less dangerous?**  
> **Was the freedom real — or only expensive?**

## Core play loops

### 1. The Final sink (civilization breathes)

```text
Wages → household budgets → buy Final at Station → stock destroyed
         ↑________________ Industry / haul ________________|
```

Empty Final shelves while Ops liquid “looks fine” is a failed read. Nella-shaped
station stalls are the color; the sink is the law.

### 2. The haul residual (where tramps live)

Tramps earn the gap between **efficient** (Bulk River Slow) and **necessary**
(Priority Capital/Final to a flickering dock). Cycle line: *Freight is saturated.
Everyone moves boxes. Nobody wants to pay for the ship that actually arrives.*

Watch: MinMargin vs plan fails · P/S/Std legs · WaitingBerth / Loading · Capital choke.

### 3. The CCA / insurance door

Registry standing is the hatch. Escrow and premiums are the price of staying on the board.

- Registration unlocks work (fiction); `CanOperate` unlocks haul (sim)
- Miss premium → uninsured → idle hull → plant starvation
- Insurable range: hold can fit cargo the policy will not

### 4. The wear clock (mileage → overhaul or burnout)

Speed, distance, and cargo mass burn **rated drive life**. At ~72% life, elective overhaul
is cheap(er). Past rated life, burnout is guaranteed until a forced overhaul. Premiums track
life fraction (capped); maintenance cash is separate from insurance.

### 5. Drama weather (`--drama on`)

Fuel famine, ore shock, fiscal bleed — plus Calypso-shaped beats when staged:
empty autonomous berths, standby ugly money, degraded handoffs, “model expected late.”

### 6. Completeness vs heroics (Meridian weather)

Ugly standby money selects **completion crews**, not momentum chasers.
Selection language stays boring on purpose. Reputation after a finished ugly job is
future work (*listed → known responsive*).

## How to run a session

```powershell
dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 10d --seed 1001

dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 30d --seed 1001 --story

dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 30d --seed 1001 --mode avalonia

dotnet run --project novolis-apps/src/SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --engine campaign --days 100d --seed 1001 --quiet
```

### Life moments to hunt

| Moment | What “winning the scene” looks like |
|--------|-------------------------------------|
| Late-pay spiral | Premium missed → registry hold → plant ore floor stress |
| Mega biography | Bulk River Slow Sol↔mine↔plant with wear and lots |
| Grounding cascade | ≥2 hulls uninsured/suspended; Final shelves thin |
| Fuel famine | Plan fails spike; hulls stuck Loading |
| Insured loss | Claim paid; underwriter cash down; shortage lasts days |
| Empty berth | Formal plan failed; autonomous dock; no late-fee chatter |
| Ugly standby | Completion premium; reputation bump; recurring Opportunities pool |
| Escrow | Open / release / clawback; issuer 5% + contractor skim |
| Jump refuse | Dense Priority band refused (unless rep/escrow) |
| Lien | Venture debt follows hull when uninsured |

```text
MILESTONE:
```

## Reading Spectre like a player

1. Header hash — reproducibility badge
2. Ops money — mood, not victory
3. Ops vs Core — never add them
4. Logistics — geography biting
5. Registry — cast health bar
6. Milestones — the plot
7. Mega biography — big-carrier diary
8. Agent last decisions — one-line performances

Cast and voices: [characters.md](characters.md). Speakable lines: [flavor-and-audio.md](flavor-and-audio.md).
Port tiers: [places-and-stations.md](places-and-stations.md).

## Difficulty without a slider

| Knob | Softer | Meaner |
|------|--------|--------|
| `--days` | `10d` | `100d` / `1000d` |
| `--drama` | `off` | `on` (default) |
| `--story` | off | on (live radio) |
| `--mode` | `headless` | `avalonia` (briefing room) |
| `--seed` | `1001` known theater | other seeds |
| Quiet | off | on (autopsy only) |

## What “fun” means here

Causal surprise you can explain. A grounding is fun if Priority → premium → miss → idle
→ empty Final is visible. A Bulk River Slow delivery is fun if you feel why bulk never
raced the courier. Meridian boredom is fun when ugly money still finishes.

If the report only shows “cash went up,” the session failed — even if numbers are green.

## Related

- [Characters](characters.md)
- [Flavor and audio](flavor-and-audio.md)
- [Commerce stack](commerce-stack.md)
- [Places and stations](places-and-stations.md)
- [Calypso canon](calypso-canon.md)
- [CLI and reports](cli-and-reports.md)
