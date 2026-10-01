# Farmer's Almanac + Crop Traits — Design

**Date:** 2026-09-28 · **Status:** approved in chat, building · **Branch:** `feat/run-ender-economy`

## Problem

Players can't see what anything *does*: why pick corn over peppers, how much HP a crop has, what a
scarecrow's radius is and how much their upgrades added. Worse, six of nine crops are statistically
identical and `CropData.threatResistance` is unused, so there is currently no real answer to "why
choose corn?". The Almanac must be backed by real differences.

## Goals

1. **Farmer's Almanac** — a book the player opens to read about every Crop, piece of Equipment,
   Animal and Pest they've unlocked; locked entries show as "???" with an unlock hint.
2. **Contextual entry** — an "i" on seed packets (seed picker) and equipment/animal tiles opens the
   Almanac straight to that entry.
3. **Honest stats** — every number is shown as *base (+bonus) = total*, computed by the same code
   gameplay uses (no parallel formulas that can drift).
4. **Real crop personality** — per-crop deer appetite, crow appetite and thirst that change play.

Non-goals (deferred): weather/storm resistance, balance pass on base values, offline-simulator
support for per-crop traits (the offline sim uses aggregate threat reductions; traits are ignored
there for now), run/account statistics.

## Crop traits (gameplay)

New `CropData` fields replace the unused `threatResistance`:

| Field | Meaning | Applied in |
|---|---|---|
| `deerAppetite` (0–2, default 1) | multiplier on deer bite damage to this crop | `AnimalThreat.EatPlant` |
| `crowAppetite` (0–2, default 1) | multiplier on crow bite damage | `AnimalThreat.EatPlant` |
| `moistureDepletionRate` (exists, now "Thirst") | multiplier on soil drying | `Plant.UpdateMoisture` (already) |

Hunger satisfied stays "HP actually removed", so a loved crop is eaten harder and fills a pest
faster. First-draft values (retune in the balance pass):

| Crop | Deer | Crow | Thirst |
|---|---|---|---|
| Radish | 1 | 0.5 | 0.8 |
| Carrot | 1.5 | 0.5 | 1 |
| Corn | 0.75 | 1.5 | 1.25 |
| Tomato | 1.25 | 1 | 1.25 |
| Strawberry | 1 | 1.5 | 1 |
| Blueberry | 0.75 | 1.5 | 0.9 |
| Green Beans | 1.5 | 0.75 | 0.8 |
| Green Pepper | 0.5 | 0.75 | 1 |
| Red Pepper | 0.25 | 0.5 | 1.25 |

## Shared stat calculators

Pure helpers in `EconomyCore` return a `StatLine` (`label`, `baseValue`, ordered `bonuses`
[`source`, `delta`], `total`, `format`). Gameplay call sites switch to the same helpers so the book
cannot disagree with play:

- **Crop:** money per harvest (research sell bonuses → Barn Harvesting → farm-upgrade cash yield for
  Field 1), coins per harvest, health (CropHp research), grow time (research growth speed, Barn
  Planting, Growth Rate upgrade; shown at normal moisture), harvest window, thirst (base × crop
  thirst ÷ water-lasting bonuses), seed bag (size, cost), deer/crow appetite.
- **Equipment:** radius, cooldown, capacity, water power — wrapping `EquipmentManager.GetEffective*`
  so the breakdown is base + upgrade levels + research.
- **Animals:** cooldown, reward, compost/min (as authored; bonuses where they exist).

Where a value depends on the field (zone level upgrades), the page shows Field 1 and says so.

## Tags (derived, never hand-set)

*Regrows* (canRegrow) · *Quick grower* (≤ 150s) · *Slow grower* (≥ 330s) · *Cannable* (canBeCanned)
· *Sturdy* (HP ≥ 90) · *Fragile* (HP ≤ 70) · *Thirsty* (thirst ≥ 1.2) · *Drought-hardy* (≤ 0.85) ·
*Deer love it / Deer avoid it* (≥ 1.25 / ≤ 0.6) · *Crows love it / Crows avoid it* (same).

Appetite bars read as words: ≤0.3 "won't touch it", ≤0.6 "avoids it", ≤0.9 "nibbles", ≤1.1
"normal", ≤1.4 "likes it", else "loves it".

## Copy

Crop blurbs, equipment/animal blurbs (fallback to existing `description` fields) and pest pages live
in the LetterCatalog as `almanac_<kind>_<id>` tip entries, so they show in
`docs/narrative/cast-and-copy.md` and follow the existing copy workflow.

## UI

`AlmanacPopupUITK` (code-built UITK, BarnPopupUITK conventions, wood frame, phone-first):
list view with tabs Crops · Equipment · Animals · Pests (icon grid, "???" when locked) → entry view
(icon, name, tags as chips, blurb, stat rows `label  base (+bonus) = total`, appetite bars) with a
Back button. Opened from a book button added to the top-left HUD stack, and via
`AlmanacPopupUITK.OpenEntry(kind, id)` from "i" badges on seed-picker packets and
equipment/animal tiles. No accent stripes; no emoji.

Unlocked = crop is selectable in the seed picker / equipment `IsUnlocked()` / animal unlocked /
pest has been seen (deer & crow always listed with their unlock hint).

## Testing

EditMode: stat calculators (base+bonuses=total, zero-bonus identity, trait multiplier), tag
derivation thresholds, appetite wording. Play: fresh + dev save at 1080x2400 via UIDriveBridge —
book button, crop page, scarecrow page with upgrades, seed-packet "i", locked "???".
