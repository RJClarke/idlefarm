# Fishing — Active Cast & Tap-to-Reel Interaction

**Date:** 2026-07-12
**Branch:** `feat/run-ender-economy`
**Status:** Design — awaiting user review before planning

## Problem / Motivation

Pantry Economy Phase 2 shipped fishing as a **placeholder**: tap the water to cast,
tap again to collect, and a fixed-position `🐟` emoji bubble appears over the lake
when a fish bites. The original spec (§3a) always intended a **press/hold cast
distance meter**, but it was explicitly deferred as an aesthetic polish pass
(see `docs/superpowers/plans/2026-07-08-pantry-economy-phase2-lake-smokehouse.md`,
"Deferred within Phase 2").

This design delivers that polish pass and expands it into a full active interaction:
**pick a spot → time a vertical charge meter to reach it → the bobber lands in the
water → tap-to-reel it back in**, with the bite bubble anchored above the real bobber.
It is built **hotspot-ready** so a future "whirlpool" system can reward accurate casts.

The underlying economy is unchanged: bite timing, rarity, pole tiers, offline
resolution, and Pantry deposit all keep working as they do today. This is an
**interaction + visual layer** over the existing `FishingManager` state machine.

## Goals

- Active, tactile cast: press-and-hold to charge, release to fire a 2D cast.
- A real **bobber** placed in the water at the cast landing point.
- **Aim by touch**: the spot you press sets the cast *direction*; the vertical meter
  sets the *distance* — you try to time the release to land on your chosen spot.
- **Tap-to-reel**: after casting, tapping the water pulls the bobber back a step at a
  time; reeling effort scales with cast distance.
- Bite bubble (fish icon) anchored **above the bobber**, following it as it's reeled.
- Penalty-free re-aim: reel an empty line all the way in and recast.
- Leave a clean seam for future whirlpool "hotspots" without building them now.

## Non-Goals (this pass)

- **Whirlpool / hotspot spots** — not built. Only the seam is left in place.
- Changing bite timing, rarity odds, pole tiers, costs, or Pantry economy.
- Final art. Everything ships with **placeholder art** (emoji / simple sprites),
  matching how Pantry Economy shipped.
- Multiple cast origins / choosing which dock to fish from. One fixed pole.

## Decisions (from brainstorming)

| Question | Decision |
|---|---|
| Does cast distance matter mechanically? | Cosmetic **for now**, but built to support future whirlpool hotspots that briefly speed up bites when you land on them. |
| Charge meter behavior | **Fill-and-hold**: fills 0→full while held, caps and holds at full. Release locks the power. |
| Reel effort | **Scales with distance** — ~3 taps for a short cast, ~10 for a max cast. |
| Aim dimensionality | **2D**, derived from the touch. |
| 2D aim scheme | **Touch = target spot (direction + distance); meter = skill gate to reach it** (refined Option 3). Meter is the primary feedback via a target tick; no ghost bobber required. |
| Meter orientation & placement | **Vertical**, pinned on the **left near shore**, never over the water. |
| Cast origin | **Fixed pole/rod prop on the near shore.** All casts fly out from there so the distance meter has real range. |

## The Interaction, End to End

State machine is unchanged in shape: `FishingManager.CastState` stays
`Idle → Waiting → Bite`. Reeling is **progress within** Waiting/Bite, not a new state.

### 1. Idle → Aim → Cast
1. **Press-and-hold** on a spot in the water. At press-down:
   - A **reticle** marker drops on that spot.
   - The cast **direction** is fixed: the unit vector from the pole's `castOrigin`
     toward the pressed spot.
   - The **target distance** is `|spot − castOrigin|`, clamped to `maxCastRange`, and
     shown as a **target tick** on the meter.
2. The **vertical charge meter** (left, near shore) fills `0 → full` over
   `chargeFillSeconds` (~1.2s) and **holds at full**. A tick marks the level that
   reaches the pressed spot.
3. **Release** locks `castPower01` = the fill level at release.
   - Bobber flies along the fixed direction to
     `castOrigin + dir × (castPower01 × maxCastRange)`.
   - Released **on the tick** → lands on the spot.
   - Released **early** → falls short along the same ray.
   - Released **late** (past the tick, before cap) → overshoots along the ray.
   - The farthest spots (tick at/near full) can't overshoot — you just hold to full.
4. **Cancel:** dragging off the water and releasing (or releasing over UI) cancels
   the cast — no fire, back to Idle, no reticle.
5. **No pole:** press shows the existing "buy a pole first" hint; no charge starts.

### 2. Waiting
- The bobber sits at its landing point. The **bite timer runs exactly as today**
  (UtcNow-anchored; resolves offline). Reeling does **not** pause or reset it.
- **Tap the water to reel.** Each tap pulls the bobber one step toward `castOrigin`
  and gives a small tug animation (LeanTween) on the bobber.
- Total reel taps were computed at cast time from distance:
  `reelTapsTotal = round(lerp(minReelTaps, maxReelTaps, castPower01))`.
- Reel all the way in (`reelTapsRemaining == 0`) with **no fish** → line retrieved,
  back to **Idle**, **no penalty**. Recast to re-aim.

### 3. Bite
- When the bite timer elapses, `state → Bite` and `pendingTier` is rolled (unchanged).
- A **speech bubble with a fish icon** anchors **above the current bobber** and follows
  it as it's reeled (replaces today's fixed-offset bubble).
- Keep tapping to reel. When the bobber reaches `castOrigin`
  (`reelTapsRemaining == 0`) → `Collect()` deposits the fish into the Pantry
  (unchanged) and returns to Idle.
- A bite may occur **mid-reel**; the bubble simply appears and finishing the reel
  lands the catch.

## Architecture (Approach A — extend the manager, add a visual)

Chosen over "put it all in `LakeNode`" (couples logic to a scene object, breaks the
existing test seam) and "new `ActiveCastController`" (duplicates state ownership /
save-load with `FishingManager`).

### `FishingManager` (extend) — spatially-agnostic logic + state
Owns the truth, stays testable and non-spatial (`DontDestroyOnLoad`). New state:

- `float castPower01` — meter fill at release (0..1).
- `Vector2 castDir` — **unit** aim direction (dimensionless; the visual scales it by
  `maxCastRange`). Keeps the manager free of world units.
- `int reelTapsTotal`, `int reelTapsRemaining`.

New methods:

- `bool Cast(float power01, Vector2 dir)` — replaces the current parameterless `Cast()`.
  Guards (`hasPole`, `state == Idle`), stores `castPower01`/`castDir`, computes
  `reelTapsTotal`/`reelTapsRemaining`, rolls the bite time (unchanged), sets
  `Waiting`, fires `OnChanged`.
- `bool Reel()` — only in Waiting/Bite. Decrements `reelTapsRemaining`. On reaching 0:
  if `state == Bite` → `Collect()` (existing Pantry deposit); else retrieve empty →
  `Idle`. Fires `OnChanged`. Returns whether a step was consumed.
- Expose `CastPower01`, `CastDir`, `ReelTapsRemaining`, `ReelTapsTotal`,
  `ReelProgress01` (= `reelTapsRemaining / reelTapsTotal`) for the visual.
- Optional hotspot seam: `Cast` accepts an optional `float biteSpeedMultiplier = 1f`
  (or an `OnCast(Vector2 worldLanding)` event a future `WhirlpoolManager` subscribes
  to). Minimal — just don't preclude it.

Reel/cast math (taps-from-distance, power→distance normalization) lives in
`FishingMath` as pure functions so it's unit-tested.

### `LakeNode` (rework) — input, gesture, meter, rendering
Extends today's pointer handling. Responsibilities:

- **Idle gesture:** press-down sets reticle + direction + target tick; drives the
  charge meter over time; on release calls `FishingManager.Cast(power01, dir)`.
- **Waiting/Bite gesture:** a tap on the water calls `FishingManager.Reel()`.
- **Rendering** (reads `FishingManager` + its own authored `castOrigin`/`maxCastRange`):
  - Bobber sprite at
    `castOrigin + CastDir × (CastPower01 × maxCastRange × ReelProgress01)`
    (retreats toward origin as it's reeled).
  - A line (`LineRenderer`) from the pole to the bobber.
  - Bite bubble above the bobber (the current `SyncBiteIndicator`, now following the
    bobber instead of a fixed `bobberOffset`).
  - Reticle while charging.

If `LakeNode` grows unwieldy, split rendering into a small `FishingLineVisual`; decide
during planning. Keep input/gesture in `LakeNode`.

### Charge meter UI
A **vertical fill bar** with a target tick, pinned to the **left near the shore**,
never over the water. World-space sprite anchored near the pole (pans with the camera
at the Lake) is simplest; placeholder art = a filled rect + tick sprite. Visible only
while charging.

### Pole prop
A visible fishing-pole/rod sprite placed at the fixed `castOrigin` on the near shore
(placeholder art). Purely cosmetic anchor for the cast.

## Data & Persistence (`GameData`)

Existing fishing fields unchanged (`poleLevel`, `hasPole`, `fishingState`,
`fishingCastUtcTicks`, `fishingBiteReadyUtcTicks`, `fishingPendingTier`). Add:

- `float fishingCastPower01`
- `float fishingCastDirX`, `float fishingCastDirY`
- `int fishingReelTapsTotal`
- `int fishingReelTapsRemaining`

`CaptureTo` / `LoadFrom` extended accordingly. **Offline:** a line left Waiting still
resolves its bite on load exactly as today; the persisted landing + reel progress mean
you resume the bobber mid-reel where you left it.

## Tuning Knobs (inspector)

- `maxCastRange` (world units) — on `LakeNode` / visual.
- `chargeFillSeconds` (~1.2s) — time to fill the meter.
- `minReelTaps` (~3) / `maxReelTaps` (~10).
- Meter position/size, bobber/line/reticle placeholder sprites.

## Future Hotspot Seam (not built)

The bobber's landing world position and the cast distance are both available, so a
later `WhirlpoolManager` can: spawn hotspots in the castable water, on cast check
whether the landing is within a hotspot radius, and if so pass a `biteSpeedMultiplier`
(or fire through the `OnCast` event) to shorten that cast's bite time until the
whirlpool despawns. This design only guarantees the seam exists; the whirlpool system
is a separate future spec.

## Testing

Extend `FishingMathTests` / `FishingManager` coverage (EditMode):

- Power→distance normalization is monotonic and clamped to `[0, maxCastRange]`.
- `reelTapsTotal` scales with `castPower01` between `minReelTaps` and `maxReelTaps`.
- `Reel()` decrements; reaching 0 in **Bite** deposits to Pantry, in **Waiting**
  returns to Idle with no deposit.
- `Cast` guards (`Idle` + `hasPole`) still hold; bite time still rolled/anchored.
- Save/load round-trips the new fields; offline Waiting→Bite still resolves.

## Edge Cases

- Release off-water / over UI / drag away while charging → cancel, no cast.
- Camera must be settled at the Lake (existing `CanInteract`).
- No pole → existing hint; no charge.
- Bite mid-reel → bubble appears, finishing the reel lands the catch.
- Reticle shown only while charging; cleared on release/cancel.
- Max-distance spots can't overshoot (meter caps at full) — acceptable.

## Rollout

Single branch (`feat/run-ender-economy`, where all Pantry Economy work lives).
Placeholder art throughout; visual playtest after build. No migration needed — new
`GameData` fields default to zero, which reads as an Idle line.
