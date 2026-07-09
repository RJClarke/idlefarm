# Pantry Economy (Wood Sinks v1) — Smokehouse, Cannery & Lake — Design

**Date:** 2026-07-07
**Branch context:** brainstormed on `feat/run-ender-economy`
**Status:** Phase 1 (Pantry core + Cannery) BUILT 2026-07-07 on `feat/run-ender-economy` — plan
`docs/superpowers/plans/2026-07-07-pantry-economy-phase1-cannery.md`, 135/135 EditMode tests,
play-mode smoke test passed (build → intake → cook → toast → sell → save/reload). Placeholder
art (Barn_Small sprite + Cloud smoke); not yet visually playtested by the user. Phases 2
(Lake/Smokehouse) + 3 (QOL ladder, research gates) not started.
**Predecessor:** `2026-06-30-woodcutting-design.md` §9 deferred "Preserves/Crafting" — this spec is that system, expanded into two buildings.

## Summary

Two wood-fired **processing buildings** turn perishable goods into higher-value goods over
real time, burning Wood as fuel. The **Smokehouse** smokes fish caught at a new **Lake**;
the **Cannery** preserves crops diverted from run harvests. Both share one processing core
(slots + firebox + timers). Together they make Wood an *infinite recurring sink*: the fire
must keep burning, so the player keeps returning to chop.

```
INPUT (perishable good) + FUEL (wood, burned/hour) → wait 4/8/12 real hours → OUTPUT (2.5×+ value)
```

## Goals

1. **Infinite wood sink** — wood demand never ends; chopping stays a per-session habit.
2. **Burning beats selling** — the fire is wood's best exit; rack-selling is the fallback.
3. **Every path stays alive** — raw-sell vs process, short vs long batches, smokehouse vs
   cannery: no option dominates; choices are driven by schedule, supply, and risk appetite.
4. **Three return rhythms** — chop now (active), collect a bite next visit (semi-active),
   open a finished batch hours later (idle bet).

## Non-goals (deferred, documented in §Future)

- Auto-chop / helper woodcutting.
- Achievements-gated tool upgrades (no achievements system yet; v1 = coins+materials).
- General drag-and-drop inventory. The Pantry is counted stacks only.
- Market/stall screen unifying all selling (v1 sells at each building).
- Wood Hauler helper, Damper research, helper-fisher (all specced in §Future, not v1).

## The One Rule (economy anchor)

**Wood burned always beats wood sold.** Rack price stays 1g/wood. Net premiums from
processing return ~2.5g+ per wood burned (commons) up to ~11g (rare fish). Every tuning
number derives from holding this band. If a change makes burning worse than selling, it's
wrong.

## 1. The Pantry (resource ledger)

Counted stacks in the existing `CurrencyManager`/Compost pattern — integers + change
events, **no item objects**:

- **Fish** ×3 tiers (Perch / Bass / Northern Pike)
- **Smoked Fish** ×3 tiers
- **Jars** (preserved goods, tracked per source crop for flavor: "Strawberry Jam ×4")
- (Wood already exists.)

Persisted in `GameData` JSON via SaveManager. Quests can check "make N" / "sell N".
**Selling (v1):** raw + smoked fish sell at the Smokehouse UI; jars at the Cannery UI;
wood at the existing rack. Processed goods and raw fish sell for **Gold only** (Cash stays
a wood-rack-only, in-run-capped exit per the woodcutting spec).

## 2. The Firebox (shared processing core — build once, skin twice)

- Each building has a **fuel store** (max capacity, a knob) and **slots** holding items.
- **Burn rate = perSlotBurn (20 wood/h) × active slots.** Flat rate — no per-tier burn.
  Duration alone scales total fuel: 4h = 80, 8h = 160, 12h = 240 wood per slot.
- The fire **burns while fuel remains, even with nothing cooking** — waste is possible and
  intentional (planning matters). Managed by two buttons:
  - **"Stoke to finish"** — adds exactly enough wood for everything currently loaded.
  - **"Fill furnace"** — top up to max.
- **Fuel out → cooking pauses. Nothing is ever ruined.** Resumes on re-stoke. Part-fills
  are fine (50 wood now, 50 later); total fuel-hours is what matters.
- Adding an item mid-burn raises the burn rate immediately — the "fuel lasts: Xh Ym at
  current load" readout visibly drops, prompting a re-stoke.
- Finished items move to a **ready shelf** in the building UI (slots free immediately);
  claim to Pantry, sell from the same panel.
- **All decision math is pure** — `ProcessingMath` in the EconomyCore assembly, mirroring
  `WoodcuttingMath`: burn simulation, pause/resume, offline catch-up (UtcNow-based,
  piecewise: fuel-hours remaining vs cook-hours remaining), stack/claim/sell math.
  Fully EditMode-unit-tested.

## 3. Smokehouse — the jackpot ladder (rarity-gated, steep premiums)

| Fish | Base odds | Raw sell | Time | Wood | Smoked | Net premium | Premium/wood |
|---|---|---|---|---|---|---|---|
| Perch | 98% | 100g | 4h | 80 | 300g | 120g | 2.5g |
| Bass | 1.9% | 400g | 8h | 160 | 1,400g | 840g | 5.3g |
| Northern Pike | 0.1% | 2,000g | 12h | 240 | 5,000g | 2,760g | 11.5g |

(All values are tuning knobs; the *shape* — steep premium curve justified by RNG-gated
inputs — is the design.)

- **Slots 1 → 8**, purchased **inside the Smokehouse menu** (coins + wood, front-loaded
  curve — see §5a); the **last 2 slots are research-gated** instead (alternate path).
- Rare fish are *events*; pole upgrades raise the odds and thus the income ceiling.
- **Saturation note:** at mostly-perch odds each running slot consumes ~6 fish/day, and a
  manual fisher catches ~10–20. Slots 1–3 are all a human can feed; slots 6–8 only pay
  off once the helper-fisher (Future) exists — which is exactly why the last slots are
  research-gated.

### 3a. Fishing (the Lake)

Sits **between passive (Research) and active (Woodcutting)** — semi-active:

- **Fishing pole mirrors the axe exactly:** no pole → tap the Lake → same world-hint
  pattern ("You need to buy a fishing pole first") → first pole is a **coins-only**
  purchase in the Carpenter **Tools** section → thereafter upgrade-only (coins +
  materials; achievement gates deferred).
- **Pole stats:** bite rate + rarity odds. Upgrades shift the rarity curve; rare fish stay
  *possible* at entry level (98 / 1.9 / 0.1 base).
- **Casting:** press/hold → distance meter → cast (distance is purely aesthetic for now;
  castable pools are a future idea). Bobber sits on the water.
- **Bite:** average ~20 min (UtcNow-based, works offline). A **speech-bubble indicator
  with a small fish icon (no words)** appears at screen edge when a fish is on.
- **No minigame, no loss:** the fish waits indefinitely — collect it next visit, even
  hours later. Manual collect, then recast. **One line in the water at a time** —
  throughput is throttled by visit cadence, not grinding.
- Expected rhythm: cast → go chop wood / manage the run → collect on the way back.

## 4. Cannery — the volume engine (quantity-gated, gentle premiums)

Crop inputs aren't rare, so the gate is **sacrifice**: every diverted crop is run income
forfeited while Money = survival (run-ender economy). The ladder:

- **Input units = 1 per cook-hour:** 4h jam = 4 units, 8h compote = 8, 12h sauce = 12.
- **Jar value = forgone harvest income × multiplier:** 2.5 (4h) / 2.65 (8h) / 2.8 (12h) —
  flat base plus a small **patience bonus**.
- Consequence by design: three 4h batches ≈ one 12h batch per slot. The choice is purely
  **schedule** — active players run short flexible batches; overnight players lock a slot
  for one check-in and a small bonus. No tier dominates; sauce can never pay Pike money.
- Single crop type per jar (readable output: "Strawberry Jam"). Crops bucket into the
  4h/8h/12h tiers by crop value (data-driven mapping).
- **Slots 4 → 24**, purchased in the Cannery menu (coins + wood, front-loaded curve —
  see §5a), last few research-gated.

### 4a. Intake QOL ladder (mid-run crop diversion)

- **Tier 1 (base):** first-come-first-served — harvested crops (player or helper) fill
  open slots in harvest order, skipping their coin payout. One master **Intake ON/OFF**
  lever on the building so it never silently taxes a desperate run.
- **Tier 2 (upgrade):** **intake bin** — harvests sit in a temporary holding area
  mid-run; the player opens it and chooses what enters slots.
- **Tier 3 (upgrade):** **per-slot pre-assignment** — designate each slot a crop type;
  auto-fills only from matching harvests; unmatched slots stay empty.

UX principle: **easy drop points in the menu** — no inventory-management screen.

## 5. Progression & gating

1. **Research kicks off each building** — an Equipment-branch research entry gates
   availability (Compost Bay pattern): "Smoking" and "Preserving".
2. **Carpenter Construction builds the shell** — coins + a **chunky wood cost** (the
   burst sink; roughly a day or two of chopping). Sits beside Greenhouse in the existing
   Construction section.
3. **Slots + QOL tiers bought inside each building's own menu** (coins + wood), with the
   final slots gated behind research entries instead — alternate paths, not a pure ladder.
4. Pole (and axe) upgrade paths stay in Carpenter Tools.

### 5a. Slot pricing curve (front-loaded)

This game is meant to stretch **years**, so slot costs are deliberately lopsided:

- **The first ~25–40% of each building's slots are cheap and quick** — early game feels
  generous and fast-moving (Smokehouse slots 2–3, Cannery slots ~5–10).
- **After that, costs ratchet hard**: mid slots take days of normal play; late slots take
  a week+ of grinding each. Anchor for tuning: a late slot costs *multiple days of the
  marginal premium that slot will generate* — nonstop players earn it roughly twice as
  fast as casuals, never instantly (income is time-capped, see §7).
- **The final slots are research-gated instead of purchasable** — the alternate route.

## 6. Daily wood budget (the "keep chopping" dial)

Target: **~3–5 Woods visits per day, held constant across progression.**

- One clear of the current 3 trees ≈ 210 wood ≈ ~90 seconds.
- Early game (1–2 slots running): ~300–500 wood/day ≈ 2 visits.
- Late game demand grows ~10× (more slots, longer batches), so **supply must scale to
  hold visits constant**: axe levels gain a **yield multiplier** (new axe effect), the
  Woods gains more/bigger trees, and research adds burn efficiency (−% wood/slot-hour)
  and chop yield. Auto-chop remains future.
- All numbers are inspector/SO knobs, consistent with existing tuning patterns.

## 7. Stress test & anti-exploit (nonstop-player audit, 2026-07-07)

Audited: what does a player who fishes and chops **nonstop, every refresh** earn, at every
tool level? Findings and rulings:

- **Fishing cannot break the economy.** One line + ~20min bite = max ~3 fish/hour
  regardless of effort. Base pole ≈ ~108g/bite raw (~244g smoked); a max pole with fat
  rare odds ≈ ~290g premium/bite. A hyperactive 20-bite day ≈ 2–8k gold. Bounded by
  real time by construction — no changes needed.
- **Sapling exploit (FIXED in code 2026-07-07):** `StageYield` used to pay 20% of full
  yield at stage 0 for ~1 tap, and felling replants at stage 0 instantly → tap-spam
  minted unbounded wood (~70k/hour). Fix: `StageYield = fullYield × stage/(stageCount−1)`
  (sapling = 0), and `TreeNode` makes no chop progress on a zero-yield stage. Regrow
  timers are now genuinely the supply governor.
- **Grove camping (tuning change required at implementation):** 40s/80s regrows were
  tuned as downtime filler, not audited as a faucet — camping 3 trees ≈ 14,000 wood/hour
  ≈ 14,000g/hour at the rack, which trivializes every sink. **Stretch regrows into the
  minutes range** (target: softwood ~3min, hardwood ~6min → camping ≈ ~3k wood/hour,
  and a ~90s visit still clears ~210 wood, so the intended visit experience is
  unchanged). Values live on `WoodTreeData` SOs; final numbers are a playtest knob.
- **Why the system is otherwise camp-proof:** processed income is capped by
  **slot-hours and bite timers — real time — not by wood owned**. A hoard of 50k wood
  still earns exactly `slots × hours × premium`; surplus wood's only exit is the rack at
  1g. Nonstop players accelerate *progression* (~2× a casual), never their *income
  rate*. Watch-item for playtest: if rack-selling still overshadows premiums, lower the
  rack gold price rather than touching the premium tables.
- **"Perfect engine" endgame is intended and gated:** full-tilt demand (24 cannery slots
  + fed smokehouse) ≈ 8–12k wood/day vs ~210/visit early — the gap closes only by
  stacking axe yield multiplier + Woods expansion + burn-efficiency research + Wood
  Hauler + helper-fisher, with a true zero-touch engine reserved for an auto-chop
  research at the very end of the tree.

## 8. World layout & presentation

- **Lake = new camera location directly RIGHT of the farm** (Research is top-right,
  Woods bottom-right; the Lake sits straight right, possibly farther out horizontally).
  Reuses `CameraPanController` locations like Woods did.
- **Smokehouse: placed between the Lake and the Woods.** **Cannery: between the Farm and
  the Woods.** (Positions may shift in scene work.)
- Both are **clickable world buildings** (Research-style): idle vs active animation —
  **chimney smoke while the fire is lit** — and tapping opens the building's UITK popup
  (`WoodRackPopupUITK` pattern, shared PanelSettings).
- Batch-complete moments surface via the existing ToastManager.

## 9. Future (documented, NOT v1)

- **Wood Hauler helper** — a considerable one-time purchase (~50,000 coins): walks wood
  from the wood holding spot to both furnaces, carry-capacity-limited and taking real
  time per trip, keeping each furnace stocked at "exactly enough" so no wood is wasted.
  The automation endgame for stoking.
- **Damper research** — fire auto-banks when nothing is cooking (removes empty-burn
  waste as a QOL unlock, never a default).
- **Helper-fisher** — passive catches while away (cow-compost pattern).
- **Achievement gates** on pole/axe upgrade tiers, once an achievements system exists.
- **Castable pools** at the Lake (distance meter becomes mechanical), rare-crop jackpot
  lane for the Cannery, unified market-stall sell screen.

## 10. Build phasing

1. **Phase 1:** Pantry + `ProcessingMath` firebox core + **Cannery** (Tier-1 intake +
   ON/OFF, slots, selling). No new gathering loop — proves the whole engine.
2. **Phase 2:** Lake location + pole (Carpenter Tools) + fishing loop + **Smokehouse**.
3. **Phase 3:** QOL ladder — intake bin, pre-assignment, research entries (kickoff +
   slot gates + efficiency), then Future items as separate specs.

## 11. Architecture notes

- `ProcessingMath` (pure, EconomyCore) + `ProcessingBuilding` state model shared by both
  buildings; `SmokehouseManager` / `CanneryManager` MonoBehaviours are thin consumers,
  as are the UITK popups.
- Fishing: `FishingManager` (pole level, cast state: castUtc / biteUtc / pending tier —
  persisted), `LakeNode` world interaction mirroring `TreeNode`'s no-tool hint flow.
- Persistence adds to `GameData`: pantry counts, per-building {built, fuel, slot count,
  per-slot {item, cookSecondsRemaining}}, QOL tier levels, pole level, cast state.
- Save triggers: existing (change events → AutoSave debounce, pause/quit, explicit).
- Offline catch-up: pure piecewise simulation in `ProcessingMath`; bite timer via UtcNow.
  Welcome-back ledger can list completions later (not v1-blocking).
- EditMode tests follow the WoodcuttingMath pattern: burn/pause/resume/offline, stage
  math, intake routing, jar/smoke value formulas, sell gating, slot purchase gating.

## Open questions

1. **Cannery name** — working name "Cannery"; alternatives: Preserving Kitchen, Jam
   Kettle, Canning Shed. User picks any time before UI text lands.
2. Exact knob values (burn rate, capacities, slot costs, crop tier mapping) — tuning
   pass during implementation; the formulas above are the contract.
3. Which crops bucket into which cannery tier (berries vs tomatoes vs apples etc.) —
   data decision once crop list is reviewed.

## Phase 3 implementation decisions (2026-07-09)

Locked during the Phase 3 brainstorm. Refines §4a / §5 / §10 for the build:

- **Scope = research entries only.** The intake QOL ladder (§4a T2 intake bin, T3
  per-slot pre-assignment) is **deferred to a Phase 4** — Phase 3 ships the research
  gating layer alone. One combined Phase 3 plan doc (not split).
- **Kickoff gating puts the building "in stock" at the Carpenter** (not a save-migration
  concern — grandfathering is explicitly out of scope). Two binary researches gate the
  existing Carpenter build rows: **"Preserving"** → `cannery_unlocked`, **"Smoking"** →
  `smokehouse_unlocked`. Before the flag is set the build row simply isn't offered. Intended
  ladder: research → build → buy slots → later research expansions/efficiency ("a few
  steps, feels normal"). Woods, Lake, axe, and pole stay ungated — these buildings are the
  opt-in enhancements to the fish/woods loops.
- **Final-slot gates** use the managers' existing purchasable-vs-total split. Cannery
  reserves slots 21–24: **Cannery Expansion I** (`cannery_expansion_1`, +2 → 22) and **II**
  (`cannery_expansion_2`, +2 → 24, prereq I). Smokehouse reserves 7–8: **Smokehouse
  Expansion** (`smokehouse_expansion_1`, +2 → 8). Each is a binary flag; managers compute
  `effectivePurchasableCap = maxPurchasableSlots + Σ(unlocked expansion flags × 2)`, clamped
  to `totalMaxSlots`; `CanBuySlot` reads the effective cap.
- **Burn efficiency is per-building** (not shared): `cannery_burn_efficiency` +
  `smokehouse_burn_efficiency` scalar (leveled) researches. Each manager scales
  `perSlotBurnPerHour × (1 − clamp(GetBonus, 0, ~0.40))` before calling
  `ProcessingMath.Simulate`. The ~40% clamp keeps demand alive (the "keep chopping" loop
  must survive); only per-slot burn is reduced, not base waste burn. Consistent with the One
  Rule — this only makes burning more efficient vs selling.
- **Branch = Equipment** (reuse the existing tab; no new ResearchPopupUITK tab). All 7
  entries created via `CreateBinary`/`CreateStd` in `ResearchCatalogGenerator`, then the
  catalog is regenerated to `Resources/Research/`.
- **New constants:** `StatKey.CanneryBurnEfficiency`, `StatKey.SmokehouseBurnEfficiency`;
  `FeatureFlag.CanneryUnlocked`, `SmokehouseUnlocked`, `CanneryExpansion1`,
  `CanneryExpansion2`, `SmokehouseExpansion1`.
- **Supply-scaling levers deferred** (§6 axe yield multiplier, chop-yield research, more
  trees): only needed once late-game slot demand actually materializes, so they belong with
  a post-Phase-3 playtest-tuning pass, not this plan.
