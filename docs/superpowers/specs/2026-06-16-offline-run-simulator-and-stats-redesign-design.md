# Offline Run Simulator + Run Stats / Welcome-Back Redesign

**Date:** 2026-06-16
**Branch:** `feat/run-ender-economy`
**Status:** Design approved — ready for implementation plan

## Problem

Three connected issues, all surfaced by the new run-ender economy (money = seed-bag fuel,
bankruptcy ends the run, score = Farm Time survived):

1. **Run Stats popup is flat and unclear.** Plain two-column TMP text. "Total Run Time" vs
   "Real Time" are confusing labels, and both render as `MM:SS` so `513:09` reads as nonsense
   (it's 8h 33m). The user wants a polished, readable layout in `h m s`.

2. **The Welcome-Back popup says nothing about what the farm actually did.** On reopen it shows
   cow compost + research catch-up, then "here's some coins for being away." There is no
   breakdown, so the player cannot reason about efficiency or plan.

3. **Welcome-Back and run-loss conflict.** On reopen, `ResumeRun` always restarts the run, tactical
   state resets (nothing growing), and `BankruptcyWatcher` silently ends it ~8s later if the player
   can't afford a seed bag. So you can "come back" AND "lose the run" with no explanation.

## Solution Overview

Build a **deterministic, cause-attributed offline run simulator** that replays the away-period using
the game's real formulas, then drives an **affordability-gated welcome-back flow** and a **redesigned,
itemized Run Stats screen**. Three deliverables:

1. **`OfflineRunSimulator`** — pure, unit-testable C#. Replays the away-window in farm-time, attributes
   harvests and losses by cause, finds the bankruptcy point (if any), and returns a full result.
2. **Affordability-gated reopen flow** — if the simulated run stays solvent → resume + "Continue"
   welcome-back modal. If it goes bankrupt → end the run + "Run ended" modal → full Run Stats recap.
3. **UI redesign** — Run Stats popup rebuilt in UI Toolkit ("Ledger" direction); two welcome-back
   outcome modals in the same style; `h m s` time formatting; relabeled time fields; per-crop
   itemization.

The simulator carries a deliberate **30% offline-inefficiency tax** on payout so being away is viable
but never optimal.

---

## Decisions (locked during brainstorming)

| Topic | Decision |
|---|---|
| Reopen flow | **Affordability gate.** Simulate away-period; solvent → resume + Continue modal; bankrupt → end run + Run-ended modal → full stats. |
| Time labels | Rename **"Total Run Time" → "Farm Time"** (drives score), keep **"Real Time" → "Real time played"** (wall clock). Show both, with a hint. |
| Time format | **Adaptive `h m s`**, drop empty leading units: `8h 33m 9s`, `33m 9s`, `45s`. |
| Sim fidelity | **Cause-attributed cycle simulator** — explicit loss channels (deer/crows/lightning/dried/rotted), not one fuzzy "efficiency" number. |
| Threats/weather | Pulled from existing **deterministic wave formulas** (`ThreatWaveManager`, `ThunderstormManager`). No randomness needed. |
| Offline tax | Flat **30%** (down from old 50%). Applied **only to payout**: final Coins banked + resume-Money. **Compost untaxed.** Sim runs untaxed, so survival/bankruptcy timing matches the live game. |
| Stats visual | **Direction A — "Ledger"** (refined rows grouped by section, hero score, icons). |
| Itemization | **Harvested itemized per crop** (sprite + name + count + total). **Losses itemized per cause.** |
| CTA layout | Main CTA always on **bottom**, sub-action above it. **Money listed before Coins** (money is transient fuel; coins/compost are the kept bottom line). |
| Implementation surface | Run Stats **migrates from TMP scene popup to UI Toolkit** to unify with the welcome-back modals. |

---

## Component 1 — `OfflineRunSimulator` (pure C#)

Lives in the `IdleFarm.EconomyCore` asmdef (alongside `SeedEconomy`) so the EditMode test assembly
can reference it. No singletons, no `MonoBehaviour`, no scene access — all state comes in via an input
struct, all results go out via a result struct. This is the same isolation pattern `SeedEconomy` uses
and is what makes it testable.

### Inputs (`OfflineSimContext`)

Gathered by the caller (`OfflineProgressManager`) from live managers/save:

- `awaySeconds` — real wall-clock gap.
- `startFarmSeconds` — saved `runTotalSeconds` (Farm Time at save).
- `startMoney` — saved run Money balance.
- `maxGameSpeed` — `1 + GameSpeed` research bonus (offline assumed at max speed, matching `ResumeRun`).
- `zones` — list of `{ zoneId, crop, tileCount }` from the saved seed selection + `FarmGrid` config.
- Mitigation state — fence level, scarecrow owned, farm dog owned, sprinkler owned, plus relevant
  research bonuses (threat reduction, watering, seed-bag discount, bag-size, etc.).
- Tuning — `SeedEconomy` constants + a new `OfflineSimTuning` (see below).

### Time model

Farm Time advances at max speed while away: `totalFarmToSimulate = awaySeconds * maxGameSpeed`.
The sim steps in fixed **farm-time ticks** `Δ` (default 10s; a tunable — smaller = more accurate,
slower). It runs from `startFarmSeconds` until `startFarmSeconds + totalFarmToSimulate` **or** until
bankruptcy, whichever comes first.

### Per-tick loop

For each tick at farm-time `t`:

1. **Wave context** (deterministic, from existing formulas):
   `wave = floor(t / waveIntervalSeconds) + 1`; deer/crow counts + hunger from `ThreatWaveManager`'s
   formulas; storm active iff `t` falls inside a storm window derived from `stormWaveInterval`.
   These formulas are pure functions of `t` — the sim re-implements/them shares them, it does not
   need the live managers running.
2. **Growth** — advance each tracked tile's growth progress by `Δ`.
3. **Planting** — fill empty tiles up to a per-tick **helper throughput cap** (`OfflineSimTuning`),
   consuming seeds and auto-buying bags via `SeedEconomy.BagCost(baseCost, t/60, discount)`. If a bag
   is unaffordable and the bag is empty, the tile stays empty.
4. **Losses (attributed by cause)** — subtract from growing/harvestable tiles:
   - **Deer / crows** — eat-pressure = `count * hunger * Δ * eatRate`, reduced by fence/dog
     (deer) and scarecrow (crows) mitigation + threat research. Increment `eatenByDeer` / `eatenByCrows`.
   - **Lightning** — if storm-lightning active, `strikes = lightningRate * Δ`. Increment `struckByLightning`.
   - **Dried / rotted** — tiles not covered by sprinkler/helper watering dry at `dryRate`; harvestable
     tiles past their window rot at `rotRate`. Increment `driedUp` / `rotted`.
5. **Harvest** — mature tiles within their harvest window are harvested: `money += harvestValue`,
   `coins += coinValue`, `harvestedByCrop[crop]++`.
6. **Bankruptcy check** — if no tile is growing/harvestable, no seeds remain, and no bag is affordable
   for any zone → record `bankrupt = true`, `bankruptAtFarmSeconds = t`, stop.

> Loss-channel rates (`eatRate`, `lightningRate`, `dryRate`, `rotRate`, helper throughput) are **dials**
> in `OfflineSimTuning`, seeded to plausible values and refined by playtest. The architecture is fixed;
> the constants are expected to be tuned. This is explicitly an approximation tuned to *feel* accurate,
> not a bit-exact replay of the live loop.

### Outputs (`OfflineRunResult`)

- `bankrupt` (bool), `bankruptAtFarmSeconds`, `finalFarmSeconds`.
- `harvestedByCrop` — `Dictionary<CropData,int>` (drives itemized Harvested list).
- Loss counts by cause: `eatenByDeer`, `eatenByCrows`, `struckByLightning`, `driedUp`, `rotted`.
- `moneyEarned`, `moneySpentOnBags`, `coinsBanked`, `compostGained`.
- Defense counts (deer/crows repelled), if cheaply derivable from mitigation; else omitted v1.
- `seedsPlanted`, total harvested (derived).

### Tax application (outside the sim)

The sim returns **untaxed** numbers. The caller applies the 30% haircut to **payout only**:

```
grantedCoins   = floor(result.coinsBanked  * 0.70)
resumeMoney    = floor(finalMoneyBalance   * 0.70)   // only matters on the survived path
compostGranted = result.compostGained                // untaxed
```

Survival/bankruptcy is decided by the untaxed sim, so the tax never causes an "extra" offline
bankruptcy — it only reduces what you walk away with.

---

## Component 2 — Reopen flow (`OfflineProgressManager` + `RunManager`)

Today `SaveManager.LoadGame` calls `RunManager.ResumeRun` directly when `runActive`, and
`OfflineProgressManager` separately shows cow/research catch-up. These are merged into one gated flow:

1. On load with an active run + meaningful gap (≥ `MinGapMinutes`), `OfflineProgressManager` builds an
   `OfflineSimContext` and runs `OfflineRunSimulator`.
2. **Survived path:** apply tax → `RunManager.ResumeRun(...)` with the advanced Farm Time and the
   **taxed** resume-Money → open **Continue** welcome-back modal populated from the result. Cow compost
   + research catch-up fold into the same modal.
3. **Bankrupt path:** do **not** resume. Finalize the run as ended at `bankruptAtFarmSeconds`
   (set `LastRunSurvivedSeconds`, push the per-crop/per-cause result into `RunStats`), grant taxed
   coins/compost, clear the saved active-run snapshot → open **Run-ended** welcome-back modal whose CTA
   opens the full Run Stats popup.
4. Gaps below `MinGapMinutes` keep today's behavior (plain resume, no modal).

`BankruptcyWatcher` is unchanged for live play; the offline path now pre-empts the silent ~8s
bankruptcy by deciding outcome up front.

---

## Component 3 — `RunStats` per-crop tracking

`RunStats` currently keeps only a total `CropsHarvested`. To itemize Harvested by crop (live runs **and**
the offline result), add:

- `Dictionary<CropData,int> HarvestedByCrop` with `AddCropHarvested(CropData crop)` (keep the existing
  parameterless increment as a fallback / update callers).
- A setter/merge so `OfflineProgressManager` can push the simulator's `harvestedByCrop` and per-cause
  losses into `RunStats` for the bankrupt path, so the full Run Stats popup renders identically whether
  the run ended live or offline.
- `GetDisplayStats()` is replaced by a richer accessor the new UITK popup consumes (sections + itemized
  rows + crop sprite refs), rather than pre-formatted label/value strings.

Loss counters already exist per cause (`PlantsEatenByDeer`, `PlantsEatenByCrows`, `PlantsDehydrated`,
`CropsDecayed`); add `PlantsStruckByLightning` (new cause surfaced by the redesign).

---

## Component 4 — UI (UI Toolkit)

All three surfaces share the "Ledger" style: dark farm panel, hero block, section headers with color +
icon, right-aligned tabular values, kept-currency highlight row.

### Time formatting helper

A shared `TimeFormat.Hms(float seconds)` → adaptive `8h 33m 9s` / `33m 9s` / `45s` (drop empty leading
units, always show the smallest present unit). Used everywhere run time is shown.

### Run Stats popup (rebuilt in UITK)

- **Hero:** `Farm Time · Score` (big `h m s`) + `Real time played · …` sub-line.
- **Bankruptcy banner** when the run ended broke.
- **Economy:** Money earned, Spent on seed bags (negative), **Coins banked** (highlighted bottom-line
  row), Compost gained, dim "after 30% offline tax" note (offline runs only).
- **Harvested:** one row per crop — `cropSprite` + name + count — then a "Total harvested" row.
- **Losses:** one row per cause — Eaten by deer / by crows / Struck by lightning / Dried up / Rotted.
- **Defense:** deer/crows repelled.
- **CTA:** Close (bottom).
- Reachable from: normal run end, "Prev. Run Stats" button, and the Run-ended modal CTA.

### Welcome-back modals (UITK, outcome-selected)

**Survived → Continue:**
- Title "Welcome back!", "You were away for …".
- Green hero: "Your run is still going — Farm Time +Δ (now …) · ran at max speed while away".
- "While you were away": Money now, **Coins banked** (highlight), Compost gained, dim tax note.
- Harvested (itemized, can be abbreviated to top crops + total).
- Losses (itemized by cause).
- Sub-action "See full breakdown" (above) → main CTA **"Continue the Run"** (bottom).

**Bankrupt → Run ended:**
- Title "Welcome back", "You were away for …".
- Red hero: "Your run ended while away — Bankrupt at … · final score …".
- "You banked": **Coins kept** (highlight), Compost gained, dim tax note.
- Harvested + Losses itemized.
- Sub-action "Start a new run" (above) → main CTA **"View Full Run Stats"** (bottom).

---

## Testing

EditMode unit tests on `OfflineRunSimulator` (pure, deterministic):

- **No-away / tiny gap** → empty result, not bankrupt.
- **Solvent steady state** → harvests > 0, money stays ≥ 0, not bankrupt over a long window.
- **Forced bankruptcy** → high bag cost / zero income → `bankrupt`, with `bankruptAtFarmSeconds`
  inside the window.
- **Cause attribution** → with deer-only waves, `eatenByDeer > 0` and other causes 0; storms produce
  `struckByLightning`; unwatered config produces `driedUp`.
- **Tax** → payout helper yields exactly `floor(x*0.70)` for coins + resume-money, compost unchanged.
- **Determinism** → identical inputs yield identical outputs (no RNG).
- **Mitigation** → adding fence/scarecrow strictly reduces the corresponding loss count.

UI verified by the existing manual/MCP smoke-test workflow (no automated UITK tests in this project).

## Out of scope / future

- Compost tax/balance (left untaxed for now; revisit).
- Per-crop **loss** itemization (v1 itemizes harvests by crop, losses by cause only).
- Bit-exact parity with the live loop — intentionally an approximation + flat tax.
- Defense (repelled) counts offline if not cheaply derivable from mitigation may be omitted in v1.

## Risks

- **Tuning drift:** offline numbers can feel off until loss-channel rates are tuned. Mitigated by
  isolating all rates in `OfflineSimTuning` and unit-testing structure (not magnitudes).
- **Second code path:** the simulator approximates the live loop and can diverge as live mechanics
  change. Mitigated by sharing the wave/storm/`SeedEconomy` formulas rather than re-deriving them, and
  keeping the sim deliberately coarse.
