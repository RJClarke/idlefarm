# Farm Upgrades — Design

**Date:** 2026-06-19
**Branch context:** built atop current work (`feat/run-ender-economy` era)
**Status:** Approved design, ready for implementation planning

## Summary

A new **Upgrades menu in the Farm bottom-nav tab** that adds a large set of
slow-scaling soil/water/crop upgrades, plus a per-zone **Zone Level** multiplier.
Inspired by *The Tower*: lots of long-scaling tracks that take a long time to max
but are valuable to grab incrementally.

The menu reuses the game's established **dual-currency progression**:
- **Coins** (outside a run) buy **permanent** levels — saved forever.
- **Cash/Money** (inside a run) buy **temporary** levels that always scale at a
  fixed rate, starting from the player's permanent floor, and reset at run end.

This pattern already exists in `UpgradeManager` (`GetCurrentLevel = max(permanent,
temporary)`), so we are extending a proven system, not inventing one.

This menu is **new ground** — it does not need to visually or structurally match
the existing Helpers / Equipment / Zones popups. Those are untouched by this work.

## Goals

- Add ~18 long-scaling upgrade tracks players can chip away at over months.
- Nothing is literally infinite — every track has a (sometimes very high) cap.
- Mixed scaling per stat: a few **high-cap** tracks (big number, gentle curve with
  milestone breakpoints) and several **low-cap** tracks (~20–40, or very low for
  buffers).
- Dual currency: Coins permanent / Cash temporary, per the existing pattern.
- A per-zone **Zone Level** master multiplier — one track per unlocked zone — to
  give each zone its own long progression axis without 4× duplicate menus.
- A fresh 2-column card UI with simple, descriptive section names (Soil / Water /
  Crops / Land).

## Non-Goals

- No re-skin of Helpers / Equipment / Zones popups (explicitly out of scope; this
  is allowed to look different and "break new ground").
- No new save infrastructure beyond registering the new upgrade IDs (permanent
  levels already persist through `UpgradeManager` → GameData JSON).
- No day/night, tier prestige, or other economy changes.

## Menu placement & information architecture

- Lives as a new **Upgrades view in the Farm tab**, separate from the existing
  Zones/Grid popup (which remains as-is).
- **2-column card grid**, grouped into collapsible/labeled **sections**.
- Section names are simple and describe what they affect — **Soil, Water, Crops,
  Land** — deliberately *not* matching the large nav items (no "Helpers"/"Equipment").
- **No level bars.** Each card shows a small current-level readout (e.g. `Lv 12`).
- **Card anatomy:** icon + name + one-line subtext (the bonus) on the card; a buy
  button with the price. Background art per card where assets allow.
- The buy button swaps **Coin price (gold) outside a run** ↔ **Cash price (green)
  inside a run** based on `RunManager.IsRunActive`.

## Stat catalog

Caps below are starting proposals to be tuned. "High-cap" = a large finite number
(target 500–1000) on a gentle curve braked by breakpoints.

### 🟫 Soil
| Stat | Effect | Cap class |
|---|---|---|
| 🌾 Fertilizer A | +% Cash yield per harvest | high-cap |
| 🪙 Fertilizer B | +% Coin yield per harvest (banked) | high-cap |
| 🏆 Soil Quality | +% to ALL crop output (master multiplier) | high-cap |
| 💩 Compost Yield | +% compost from crops/cow | capped ~30 |
| ⛏️ Soil Prep | faster baseline tilling (tile-side) | capped ~25 |

### 💧 Water
| Stat | Effect | Cap class |
|---|---|---|
| 💧 Water Retention | slows dehydration rate | capped ~40 |
| 🛢️ Water Capacity | allows watering past 100% (overflow buffer) | very low cap ~8 |
| 🌧️ Drying Grace | flat delay before drying begins after full water | capped ~25 |
| 🚿 Watering Power | each watering action adds more moisture | capped ~25 |

### 🌱 Crops
| Stat | Effect | Cap class |
|---|---|---|
| 🌱 Growth Rate | faster crop growth | high-cap |
| 🥀 Slow Decay | slows decay once a crop starts drying | capped ~40 |
| 🍂 Rot Resistance | slows the rot stage specifically | capped ~30 |
| 🛡️ Crop Hardiness | crops survive threats/strikes longer before dying | capped ~30 |
| ⏩ Head Start | new crops begin partway into stage 1 | capped ~20 |
| ✨ Bountiful Harvest | % chance to double a harvest (crit) | capped % ~40 |
| ♻️ Seed Refund | % chance to refund the seed bag on harvest | capped % ~30 |

### 🏞️ Land
| Stat | Effect | Cap class |
|---|---|---|
| 🏞️ Zone Level | +% multiplier to that zone's output | high-cap, **one track per unlocked zone** (`zone_level_z1..z4`) |

Distinctions kept deliberate so each stat is meaningfully different:
- **Water Retention** = rate of drying; **Drying Grace** = delay before drying
  starts; **Water Capacity** = how far above 100% moisture can go.
- **Slow Decay** = decay after drying; **Rot Resistance** = the rot stage.
- **Seed Refund** eases the Money-as-fuel bankruptcy pressure from the run-ender
  economy; **Bountiful Harvest** is a feel-good crit.

## Scaling / math model

Each track is described by data fields (extend `UpgradeData` or a sibling SO):
- `maxLevel` (the hard cap — large for high-cap, small for capped).
- `curveType` / cost growth parameters.
- `breakpoints`: levels at which the cost takes a larger milestone jump.

Behaviour:
- **High-cap tracks:** gentle geometric base (small % increase per level) so the
  player is "always one level away," **plus milestone breakpoints** (e.g. every
  25/50 levels) that jump the cost. The breakpoints are the brake that prevents
  early maxing even though early levels are cheap.
- **Capped tracks:** smaller `maxLevel` with a slightly steeper curve so each
  level reads as a real decision.
- **% chance / rate-reduction stats are always capped** (can't refund >100% of a
  seed, can't dry slower than instant). These are the low/medium caps above.

Effect application reuses the existing additive `bonusPerLevel`-style model where
it fits; percent stats apply as multipliers, buffer stats (Water Capacity) as
flat ceiling increases.

## Dual-currency wiring (existing system)

- **Permanent (Coins, between runs):** `UpgradeManager.PurchasePermanentUpgrade`.
  Already enforces `maxLevel`, blocks purchases during a run, persists to GameData.
- **Temporary (Cash, during runs):** `UpgradeManager.PurchaseTemporaryUpgrade`.
  Sets `temporaryLevels[id]` above the permanent floor; cleared on run start.
  **Add a `maxLevel` guard here too** (it currently has none) so in-run buys can't
  exceed a track's cap.
- Effective level for all consumers: `UpgradeManager.GetCurrentLevel(id)`.

## Consumer wiring

Each stat needs one consumer hookup reading `GetCurrentLevel(id)`:
- Yield (Fertilizer A/B, Soil Quality, Zone Level, Bountiful Harvest, Seed Refund,
  Compost Yield) → harvest/banking path + compost path.
- Moisture lifecycle (Water Retention, Drying Grace, Water Capacity, Watering
  Power, Slow Decay, Rot Resistance) → `SoilTile` / `Plant` lifecycle.
- Growth (Growth Rate, Head Start) → `Plant` growth.
- Defense (Crop Hardiness) → `Plant.Die` / threat-and-weather damage timing.
- Tilling (Soil Prep) → tile till duration.
- Zone Level applies per-zone, keyed to the tile's zone.

## Save / load

- Permanent levels: **no new plumbing** — they ride the existing
  `UpgradeManager` permanent-level save path in GameData JSON. We just register
  the new upgrade IDs (including `zone_level_z1..z4`).
- Temporary levels: already part of the active-run snapshot.

## UI build

- New UITK document/template for the Upgrades view (2-column card grid + section
  headers), wired from the Farm tab.
- Card template: icon, name, subtext (bonus text from the SO), `Lv N` readout,
  buy button. Button label/colour and price source (Coin vs Cash) chosen by run
  state. Reuse the affordable/can't-afford/pressed visual states already present
  in `FarmPopupUITK`.
- Refresh on `OnUpgradePurchased`, `OnCoinsChanged`/`OnMoneyChanged`,
  `OnRunStarted`/`OnRunEnded` (same dirty-debounce pattern as `FarmPopupUITK`).

## Phasing

Phase 1 (this spec) builds **all ~18 tracks including per-zone Zone Level**, the
new card UI, the scaling model, and all consumer wiring. (Per explicit decision —
no deferral.) Tuning of caps/curves is expected to iterate after playtest.

## Open tuning questions (not blockers)

- Exact high-cap numbers (500 vs 1000) and breakpoint spacing.
- Per-stat `bonusPerLevel` magnitudes and the % caps for crit/refund.
- Whether Soil Quality and Zone Level stack multiplicatively with Fertilizer
  (recommended: multiplicative, but verify it doesn't explode late-game).
