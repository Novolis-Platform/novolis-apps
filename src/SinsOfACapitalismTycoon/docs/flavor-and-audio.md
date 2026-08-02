# Flavor and audio

Headless does not mean silent. The product should *read* and later *sound* like a living
trade mesh: CCA glass offices, Meridian paragraphs, tramp galley heaters, Bulk River
schedule tones, empty autonomous docks, and the dry voice that refuses to sum Ops with Core.

## Reference listening / reading

**Galactic Confederation Review**

- [The Margin Was The Freedom](https://frankhaugen.github.io/galactic-confederation-review/articles/the-margin-was-the-freedom/)
- [FTL Transit Operational Tradeoffs](https://frankhaugen.github.io/galactic-confederation-review/articles/ftl-transit-operational-tradeoffs/)
- [Ship Law and Registry dossier](https://frankhaugen.github.io/galactic-confederation-review/dossiers/ship-law-and-registry/)

**Calypso Cycle** (author refs — see [calypso-canon.md](calypso-canon.md))

- Daily life & trade primer; CCA registration; cargo/HILS; jump doctrine
- Eska arc (Meridian options / completion crews)
- Chapters with dock weather: standby offer, empty dock, already late, what safety cost, branch office

## Flavor layers

| Layer | What it is | Voice |
|-------|------------|-------|
| **Institutional** | Standards-memo clarity | `vox.ledger` |
| **CCA glass** | Locked boards, escrow weather | `vox.cca` |
| **Dock memoir** | Varr / James complaint + gratitude | `vox.varr`, `vox.james` |
| **Liaison** | Underwriter-fluent, unsentimental | `vox.ixa` |
| **Actuarial / Meridian** | Recordable euphemism | `vox.meridian`, `vox.broker` |
| **Continuity** | Flat threshold talk | `vox.torrik` |
| **Traffic bridge** | Short urgency | `vox.drama` |
| **Schedule** | Tonne-plans | `vox.bulk` |

Flavor *annotates* causality; it does not replace tables.

## Transmission templates

Keep under ~160 characters when possible.

### CCA / registry / insurance

```text
[vox.cca] Job boards behind glass. Plenty of opportunity. All of it locked behind registration.
[vox.cca] Issuer five percent. Contractor ten plus risk. Escrowed until delivery.
[vox.dock] Record shows title, inspection, insurance, standing. Door is open.
[vox.broker] Premium revised: wear and Priority. That is what the number is for.
[vox.meridian] Odd vessel. Stable risk.
[vox.varr] Uninsured is not brave. Uninsured is begging without a form.
[vox.james] Outside the insurable range is not a job. It is a dare with invoices.
[vox.ledger] Standing: suspended. CanOperate=false. Ops cash unchanged by wishful thinking.
```

### Haul / profiles / HILS

```text
[vox.varr] Slow on Raw. Priority on Final. Cargo chooses the timetable.
[vox.ixa] If it fits the pallet, it fits the galaxy.
[vox.bulk] Bulk River Slow economic. We do not break schedule for four pallets.
[vox.drama] WaitingBerth at Capital. Dwell is not optional; berths are weather.
[vox.james] Ten light-years is routine. Twelve hurts. Beyond that needs a reason.
```

### Standby / ugly money / completion (Eska weather)

```text
[vox.ixa] Busy docks. Busy lies. None of it paid the fuel bill.
[vox.meridian] Freight is saturated. Everyone moves boxes. Nobody pays for ships that arrive.
[vox.ixa] Ugly money means the job is ugly or the person is expensive.
[vox.torrik] Soft pickup. Pattern break. Quiet money ends here.
[vox.meridian] Selection support only. No intervention specified, requested, or priced.
[vox.ixa] Listed operator becomes known responsive. That creates future work.
[vox.meridian] Every first contract is a test. This one is polite enough not to say so.
```

### Empty docks / late models / safety cost

```text
[vox.drama] Port ops autonomous. Human staffing withdrawn. Hallway expected feet; got none.
[vox.torrik] If the dock is empty, the formal plan already failed.
[vox.ixa] The model expected late and refused to call it late.
[vox.meridian] They built reality into the plan. Then pretended it was elegance.
[vox.varr] You were not managed. That is different. And that was the expensive part.
```

### Drama / loss / end

```text
[vox.drama] Fuel window closed on long-band approaches. Plan fails rising.
[vox.broker] Claim posted net of deductible. Loss quantity remains lost.
[vox.ledger] MILESTONE: claim — underwriter cash down; Industry still short.
[vox.ledger] Hash {hash}. Days {days}. Ops and Core separately. Never summed.
[vox.varr] We moved. Because we moved, other people could stay. That was the work.
```

## Audio direction (design now, wire later)

1. **Narrated report** — Review-style “listen to this selection” over Spectre acts
2. **Radio bed** — traffic-control ambience under `… 40%`, not identity orchestra
3. **Stingers** — milestone kinds only (grounding, famine, claim, upgrade, default, standby)
4. **Hull signatures** — tramp crew murmur vs Bulk River schedule chime vs CCA glass ping
5. **Silence** — allow quiet between days on `1000d` runs

| Do | Don’t |
|----|-------|
| Dry close-mic for `vox.ledger` | Trailer orchestra every tick |
| Meridian calm for ugly money | Shouting “mercenary” when Continuity is the point |
| Sparse stingers | Constant UI bleeps |
| Review accessibility tone | Meme voice packs |

Future: Manuscript/Voice packages already in Novolis dogfooding; Avalonia StarMap for
spatial voice later.

## Spectre as stage directions

1. Overture — Figlet + hash (`vox.ledger`)
2. Money — mood, not victory fanfare
3. Dual books — sacred separation
4. Logistics — geography pressure
5. Registry — cast health
6. Milestones — plot (stinger-ready; Calypso standby lines welcome)
7. Mega biography — Bulk River diary
8. Agents — one-line performances

Ask: *can a `vox.*` speak this row without a footnote?*

## Sample session script (VO)

> Ledger: Seed 1001, one hundred days, drama on.  
> CCA: Eight owner-masters and Bulk River on the board. Boards unlocked.  
> Ixa: Watch Priority — that is where premiums go to hunt.  
> Meridian: If standby ugly money appears, completion crews — not heroes.  
> Drama: Empty dock means the formal plan already failed.  
> Broker: Claims will pay. They will not restock the hold.  
> Ledger: Ops liquid down; Core still its own story. Never summed.  
> Varr: Which bill became less dangerous?

## Related

- [Characters](characters.md)
- [Gameplay](gameplay.md)
- [Commerce stack](commerce-stack.md)
- [Places and stations](places-and-stations.md)
- [Calypso canon](calypso-canon.md)
- [CLI and reports](cli-and-reports.md)
