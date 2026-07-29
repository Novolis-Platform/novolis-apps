# Places and stations

Calypso Cycle stations and habitats staged onto Sins geography. The campaign seed remains
the **Johnston 100-star** catalog ([universe.md](universe.md)); place names below are
**tier archetypes** and optional flavor overlays — not a claim that Duckville orbits
Proxima unless we explicitly bind them later.

Mesh rule: nodes + schedules + fees + risk. Corridors are **economic**, not FTL rails.

## Station tiers (playable reading)

| Tier | Cycle exemplar | Feel | Sins role overlay | Code |
|------|----------------|------|-------------------|------|
| **Shady-orderly** | Duckville (K21408) | Repair, cargo, work-for-credits | Mining | `PortTier.Shady` |
| **Fee-heavy capital** | SunSpear / Vega | Arbitration, insurance checks | Capital | `PortTier.Capital` |
| **Industrial** | Refinery 7 | Rollertrack unload | Industrial | `PortTier.Refinery` |
| **Customs trade** | Lucene 3 | Declared arrival | Transit approaches | `PortTier.Edge` |
| **Habitat destination** | Sotaris / Pacifica | Routes to settlements | Inhabited | `PortTier.Cert` |
| **Overwatch core** | Sol | Certification-dense | Capital (Sol) | `PortTier.Capital` |
| **Cert market** | Orunai docks | Ugly hulls OK if paperwork good | Industrial / Capital | `PortTier.Cert` / Capital |
| **Edge staging** | Y982116 yards | Fuel/crew before non-mesh | Transit / Waypoint | `PortTier.Edge` |
| **Fallen mesh** | Tortuga (concept) | Stolen cargo / fences | Drama / blacklist fiction | later |

Dwell and corridor tolls scale by tier at bridge seed; capital/refinery charge daily dock standing fees.

## Geographic color (for biography lines)

Early Cycle route texture (registry IDs as flavor, not sim keys):

`K21408 → F99210 (~10 ly) → V77302` · Lucene `L331904` · Sotaris `S881102` ·
later refinery / Pacifica staging `P7D19` / `7-Delta-19`.

When Spectre prints mega or tramp biographies, prefer **role + short name**
(`Sol`, `Wolf 424`, `Xi Bootis`) and optionally a **tier tag** (`capital`, `refinery`,
`edge`, `tortuga-risk`).

## Port day texture

From Cycle daily-life primer — use in flavor and milestone copy:

- Liquidity high in capitals, **thin two jumps out**; fees eat small balances
- Late fees, polite threats, forms — station weather
- Empty autonomous docks during unrest: *A hallway that expected feet and got none.*
- If the dock is empty, the **formal plan already failed**
- Economy class as camouflage: shared air, hard seats, no private compartment announcing who mattered
- Busy docks. Busy lies. None of it paid their fuel bill.

## Communications

People experience the Confederation as registry paperwork and **Mesh** traffic:
small pulse packets (fees, identity, warnings) vs bulk archives on cargo schedules.
Identity mail **pushes** when your mailbox is at the same system **node** that holds the packet;
public channels are **feeds** you pull (`News.General` ≠ `News.Prices`), except **`Emergency`**,
which is forced to every person, household, firm, ship, and thing mailbox at that node.
The Confederation does not deliver messages as a single courier; it ensures messages
become **visible** where the mesh reaches.

See [Mesh and communications](mesh-and-communications.md) for the BM sim (`Universe/Mesh`).

Wealth buys **distance and deniability** as much as guns.

## Binding policy (contributors)

1. Do not rename Johnston stars to Duckville without a `flavorBindings` table in code.
2. Do map **tiers → roles** freely in docs and Spectre tags.
3. Prefer one exemplar per tier in player-facing text so the cast stays memorable.

## Related

- [Universe](universe.md) — sim catalog truth
- [Commerce stack](commerce-stack.md) — fees and insurance at those ports
- [Calypso canon](calypso-canon.md)
- [Flavor and audio](flavor-and-audio.md)
