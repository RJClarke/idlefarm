# Reputation, Town Requests Board & Barn Skills — Design

**Date:** 2026-07-19
**Branch context:** builds on `feat/run-ender-economy` (Pantry Economy Phases 1–3, run-ender loop)
**Status:** Approved by user in brainstorm; ready for implementation planning

## 1. Overview

Three connected systems, built in four shippable phases:

1. **Item Inventory rework** — crops (and eggs) can be *collected* as counted stacks instead of auto-sold mid-run; the Inventory popup becomes an interactive "mega-list" of everything you own, with bulk-sell controls.
2. **Town Requests board** (Market) — three ever-present delivery quests (Easy/Medium/Hard) that consume items and grant **Reputation** (rep). Rep is XP, not a currency: filling the rep bar grants a **skill point**.
3. **Barn skills** (Farm) — spend points across **7 skill tracks** (25 levels each) for passive per-point bonuses plus tier bonuses at 5/10/15/20/25. A **Farm Renown** meta-bar (total levels, max 175) grants milestone bonuses, including **silo storage addons**.

Design intent: create pressure between the outputs of the farm — sell raw for Coins, can/smoke for more Coins, or donate to town quests for rep. Nothing is ever Money *and* an item: the run-ender economy's forgone-income rule (established by the Cannery) is the single cost model.

## 2. Core economic rules (binding)

- **Collect forgoes Money.** A harvested crop becomes EITHER run Money (Sell mode, today's behavior) OR an inventory item (Collect mode). Never both. Same rule the Cannery intake already applies.
- **Collected items live in the Coins economy.** Selling a collected crop from inventory pays **Coins**, never Money. There is no Money↔Coins exchange.
- **Processed always beats raw.** Each crop's raw Coin sell value is priced below its jar-equivalent per-crop value (preserves the Pantry anchor: canning/smoking remains the premium play; donating to the board is the rep play).
- **Rep is not a currency.** It is XP toward points. Points are the only spendable unit, and only at the Barn.
- **No respec.** Spent points are permanent.

## 3. Town Requests board

### 3.1 Fiction & placement
A wooden notice board prop in the **Market** area. Townsfolk pin requests with a name and one-line flavor ("Marta's making jam — 40 Blueberries"). Warm, named-people tone; sets up future per-NPC fulfillment quests.

### 3.2 Slots & lifecycle
- Exactly **3 slots, always one Easy, one Medium, one Hard**. Slot difficulty is fixed; contents roll per-slot.
- A quest sits on the board **forever** until fulfilled or skipped. No expiry, no daily reset, no time-of-day anchor.
- **Fulfill** (all-or-nothing; no partial deposits — the player's inventory is the bank): deducts the requested items, grants rep with a celebration pop, and starts **that slot's 8-hour cooldown**. Cooldown is UTC-timestamp based (Cannery pattern) so it elapses while the app is closed. Other slots are unaffected. On expiry the slot rolls a fresh quest.
- **Skip**: instantly rerolls that slot (no cooldown). Costs Gems — the purple currency (user says "shards"; if that is a rename it is global and out of scope here). Consecutive-skip ladder: **25 → 50 → 100 → 150 → 250 (cap)**, counted globally across the board. The ladder resets to 25 when any quest is **fulfilled**. Purpose: cheap rescue from a bad roll (e.g. "catch a Pike"), expensive to slot-machine for easy quests.
- No gem-reroll-for-better-reward, no extra slots in v1 (future levers).

### 3.3 Quest generation
- Data-driven **RequestCatalog** ScriptableObject: templates per difficulty (item pool, count ranges, requester names, flavor lines, rep reward range).
- Roll-time filtering by unlock state: **only ask for what the player can produce** (no fish before a pole is owned, no jars before the Cannery is built, no compost before the Compost Bay, no smoked fish before the Smokehouse, crops limited to unlocked crops).
- Requestable item classes: **crops, eggs, raw fish, smoked fish, jars, wood, compost** ("everything"). Wood/compost/egg requests are weighted rarer and tuned so they don't fight the firebox economy.
- Difficulty shape: Easy = one common item, small count. Medium = larger count or two item types. Hard = large counts and/or 2–3 item types, may include rare items (Pike, smoked fish, jars) but weights toward *large quantities of obtainable things* over rare-drop walls.
- Rep rewards: base **30 / 60 / 90** (E/M/H) with small per-quest variance (±20%), values from the catalog.
- The rolled quest (item ids, counts, requester, rep) is **persisted in GameData** — reloading never rerolls the board.

### 3.4 Rep bar & point curve
- Horizontal rep bar pinned across the top of the **Market** view (always visible there).
- Filling the bar grants **+1 unspent skill point** (toast/celebration) and resets progress against the next threshold. Overflow rep carries into the next bar.
- Point cost curve (tuning default): `cost(n) = 85 + 15·n` rep for the nth lifetime point → point 1 ≈ 100, point 25 ≈ 460, point 150 ≈ 2,335. Total for 150 points ≈ 183k rep ≈ **~1.5 years** at a realistic ~300–350 rep/day (most of the board, most days), while the first points land within the first days of play. Pure math in EconomyCore, knobs on a tuning SO.

### 3.5 Extensibility (build the seams, not the feature)
Future **NPC fulfillment quests** (e.g. seed-shop lady wants apple butter → special reward + rep) must be constructible without touching board code:
- The "deliver these item stacks → reward" requirement is a reusable struct (`DeliveryRequest`: list of (itemId, count) + rewards).
- `ReputationManager.AddRep(int amount, string source)` is public and not welded to board slots.
- `DeliveryService` (§6) fulfills any `DeliveryRequest` from any caller.

## 4. Barn skills

### 4.1 Entry & UI
Clicking the **Barn** on the farm opens `BarnPopupUITK`:
- **"Points to spend: X"** treatment near the top.
- **Farm Renown bar** at top (total levels 0–175) with clearly delineated milestone notches.
- Seven horizontal skill bars below, each rendered as **25 discrete tick rectangles** (tall, rounded): full color for spent points, dull/desaturated for unspent. Each row ends with a **"+" button** (the spend gesture — row tap alone does not spend).
- **Every tier marker is clickable** — a visibly interactive notch/icon at levels 5/10/15/20/25 on each skill bar, and at each milestone on the Renown bar. Clicking opens a readable description of that bonus so players can plan builds before committing. Unreached bonuses are readable too.
- **No respec.**

### 4.2 Tracks (all 7 scoped; drop boring ones during implementation review)
25 levels each. Per-point passives are small (+2–4%) because 25 levels; tier bonuses carry the excitement. All values are tuning-SO knobs.

| Track | Color | Per-point passive (default) | Tier bonus flavor (5/10/15/20/25) |
|---|---|---|---|
| Harvesting | Yellow | +4% Money from crop sales | double-harvest chance → golden crop chance |
| Planting | Green | +2% growth speed | free-seed chance → instant-sprout chance |
| Watering | Cyan | +3% moisture duration | sprinkler radius +1 → rain fully waters |
| Fishing | Blue | −2% bite wait time | 2× whirlpools → double-catch chance → better rare odds |
| Forestry | Orange | +2% wood per chop | double wood knockdown → faster tree regrow |
| Ranching | Red | +3% egg value | second-egg chance → faster dog patrol |
| Processing | Purple | +2% cook/smoke speed | double-jar chance → double-smoked chance |

- Consumers read passives via the research-style `GetBonus(StatKey)` pattern — one-line multiplier at existing call sites.
- Processing deliberately touches **speed**, not burn rate, to avoid double-dipping the research fuel-efficiency knob.
- Tier bonuses that are *new behaviors* (2× whirlpools, golden crops, double knockdown) are scoped individually at implementation time inside their owning systems; the skills system only exposes flags/values.

### 4.3 Farm Renown milestones
Keyed to total levels across all tracks (max 175). Rewards spreading points vs. rushing one track. Draft ladder (tuning): 10 → +2% all Money; 25 → Gem drip; 50 → −1h request cooldowns; 100 → +5% all yields; plus **silo storage addons** (§5.3) at select thresholds. The 175 capstone is **TBD** (user wants something better than a cosmetic; decide later).

Thematic frame for silo milestones: the grateful townsfolk come out and build a storage addon onto your farm.

## 5. Item inventory

### 5.1 Storage model
- `ItemInventoryManager`: counted stacks (`PantryManager` pattern — ints + change events, no item objects) for **crops (per type)** and **eggs**.
- Wood and Compost stay in their existing stores; fish/smoked/jars stay in Pantry/Cannery. No duplication.
- **Stack caps**: generous base per stack (always above what Hard quests request; never binding in early weeks), raised by Renown silo addons.

### 5.2 Collect vs Sell toggle
- One **master toggle in the run HUD**: **Sell** (default; harvest → Money, today's behavior) / **Collect** (harvest → crop stack, no Money; floating text shows an item pop, not cash).
- Cannery intake keeps first priority when enabled; the toggle governs what the Cannery doesn't take.
- Eggs follow the same toggle at claim time.
- Toggle state persists in GameData across runs.
- If a stack is at cap in Collect mode, overflow falls back to selling for Money (never silently lost).

### 5.3 Inventory mega-list (popup upgrade)
The read-only Inventory popup becomes the one place showing **everything you own**: Coins/Gems/Cash/Wood/Compost (as today) plus crop stacks, eggs, raw fish, smoked fish, jars.
- **Sell rows** wherever a sell price exists: slider + quick amounts (10 / half / max) + confirm. Crops/eggs sell for **Coins** (raw value < jar-equivalent). Wood mirrors the rack's price. Fish/smoked/jars can keep selling at their buildings; inventory sell rows for them may reuse the same prices.
- **Compost: row visible, Sell control present but disabled/grayed** (no established price in v1; it is a boost input). Do not hide the button.
- Live-updates off change events while open (existing pattern).

## 6. Architecture

### New code
- **EconomyCore (pure, unit-tested):** `ReputationMath` (point-cost curve, skip ladder, overflow carry), storage-cap math, quest-generation filtering/rolling as pure functions over injected unlock state + RNG.
- **Managers (singleton + events, existing patterns):**
  - `ItemInventoryManager` — stacks, caps, add/spend, capture/load.
  - `ReputationManager` — rep total, bar progress, unspent points, 3 slot states, per-slot cooldown timestamps, skip ladder, `AddRep(amount, source)`.
  - `FarmSkillsManager` — per-track levels, renown total, `GetBonus(StatKey)`, tier/milestone flag exposure.
  - `DeliveryService` — the single facade that knows where every item type lives (inventory/pantry/wood/compost); checks and fulfills any `DeliveryRequest`.
- **UI (UITK, existing popup conventions):** `TownRequestsPopupUITK` + notice-board world prop (Market, clickbox pattern); `BarnPopupUITK` + clickable Barn; rep-bar overlay on the Market view; run-HUD Collect/Sell toggle; Inventory popup sell rows.
- **Data:** `RequestCatalog` SO (quest templates), skills/reputation tuning SO (curve knobs, per-point values, tier definitions, milestone ladder, caps, skip costs).

### Save (GameData JSON via SaveManager + AutoSave)
Rep total + bar progress, unspent points, per-track levels (`int[7]`), 3 slot records (rolled quest payload + cooldownEndUtc), consecutive-skip counter, crop stacks, egg stack, collect-toggle bool. Load uses the catch-up-after-subscribe pattern (resume-run audit).

### Consumer wiring (one-liners at existing sites)
Plant growth / moisture drain / harvest Money value, FishingManager bite timer, WoodcuttingMath yield, egg value, Cannery+Smokehouse cook speed.

## 7. Build phasing (each independently shippable)

1. **Inventory** — ItemInventoryManager, collect toggle, mega-list sell UI, Coin pricing for raw crops/eggs.
2. **Board** — RequestCatalog, ReputationManager, TownRequestsPopupUITK, rep bar, point curve, skip ladder, DeliveryService.
3. **Barn** — BarnPopupUITK, FarmSkillsManager, 7 per-point passive consumers, clickable tier descriptions.
4. **Renown** — milestone ladder, silo addons + caps enforcement UI, exotic tier bonuses (2× whirlpools, golden crops, double knockdown, etc.).

## 8. Testing

- EditMode unit tests for all EconomyCore math (curve totals, skip ladder reset, cap math, generation filtering: never requests locked content; determinism under a seeded RNG).
- Manager tests where the existing suite has precedent; headless smoke via `Temp/` bridges (enter play mode, drive fulfill/skip/spend, assert saves round-trip).
- Balance sanity assertions: total curve cost within lifetime rep budget; raw crop Coin value < jar-equivalent for every cannable crop.

## 9. Open items (deliberate)

- 175 Renown capstone reward (user: "we can do better" — decide later).
- "Shards" vs "Gems" naming.
- Gem reroll-for-reward, extra board slots, quest expiry — future levers, not v1.
- Per-crop raw Coin values, catalog contents, milestone ladder values — tuning pass at implementation.
- Compost sell price (button ships disabled).
