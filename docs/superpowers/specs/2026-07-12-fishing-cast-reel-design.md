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
It also ships a **placeholder whirlpool hotspot** (a dark-blue circle) so aiming has a
real payoff to playtest: landing your bobber in a whirlpool speeds up that cast's bite.

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
- **Whirlpool hotspots** (placeholder dark-blue circle): landing your bobber in one
  gives that cast a much faster bite, so accurate aiming has a real, testable payoff.

## Non-Goals (this pass)

- **Dynamic "boost only while the bobber sits in the whirlpool."** The whirlpool boost
  is **snapshotted at cast time** (see Whirlpool Hotspots) to stay offline-safe. A
  continuous while-inside boost that also fades when the whirlpool despawns mid-wait is
  a later refinement.
- **Whirlpool as a rarity boost.** For now it only speeds up the bite, not the odds.
- Changing baseline bite timing, rarity odds, pole tiers, costs, or Pantry economy.
- Final art. Everything ships with **placeholder art** (emoji / simple sprites),
  matching how Pantry Economy shipped.
- Multiple cast origins / choosing which dock to fish from. One fixed pole.

## Decisions (from brainstorming)

| Question | Decision |
|---|---|
| Does cast distance matter mechanically? | **Yes, via whirlpools** (in this pass): landing your bobber in a whirlpool speeds up that cast's bite, so accurate aiming pays off. Baseline fishing (no whirlpool present, or a miss) is unaffected. |
| Whirlpool in scope? | **Yes** — placeholder dark-blue circle, so we can playtest whether aiming is fun. Boost is snapshotted at cast. |
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
- **Whirlpool hook:** `Cast` accepts `float biteSpeedMultiplier = 1f` and multiplies
  the rolled bite seconds by it (a value `< 1` means a faster bite). The multiplier is
  supplied by `LakeNode` from `WhirlpoolManager` at cast time and baked into
  `biteReadyUtcTicks`, so it survives offline with no extra state.

Reel/cast math (taps-from-distance, power→distance normalization) lives in
`FishingMath` as pure functions so it's unit-tested.

### `LakeNode` (rework) — input, gesture, meter, rendering
Extends today's pointer handling. Responsibilities:

- **Idle gesture:** press-down sets reticle + direction + target tick; drives the
  charge meter over time; on release computes the bobber's world landing point, queries
  `WhirlpoolManager.BiteMultiplierAt(landing)`, and calls
  `FishingManager.Cast(power01, dir, multiplier)`.
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

### `WhirlpoolManager` (new) — the aim target
A small scene MonoBehaviour near the lake that owns the placeholder hotspot. It knows
the castable water (references the water collider + the pole's `castOrigin` +
`maxCastRange`, same spatial data `LakeNode` uses).

- **Spawning:** one active whirlpool at a time. It picks a random point that is inside
  the water collider **and** within `maxCastRange` of `castOrigin` (so it's always
  reachable), shows the circle for `whirlpoolLifetimeSeconds`, then despawns and waits
  `whirlpoolGapSeconds` before the next. Randomized within tunable ranges.
- **Visual:** a semi-transparent **dark-blue circle** sprite (placeholder), with a slow
  rotate/pulse tween for legibility. Sized to `whirlpoolRadius`.
- **Query:** `float BiteMultiplierAt(Vector2 worldLanding)` → returns
  `whirlpoolBiteMultiplier` (e.g. ~0.05, a near-instant bite) if `worldLanding` is
  within `whirlpoolRadius` of the active whirlpool, else `1f`.
- On cast, `LakeNode` computes the bobber's world landing point, calls
  `BiteMultiplierAt`, and passes the result into `FishingManager.Cast(...)`. Landing in
  the circle → that cast bites fast; missing → normal.
- **Not persisted, not offline-simulated.** Whirlpools are ephemeral cosmetic targets;
  on reload they simply respawn fresh. Any speed-up you earned is already baked into the
  cast's `biteReadyUtcTicks`.

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
- Whirlpool: `whirlpoolRadius`, `whirlpoolLifetimeSeconds` / `whirlpoolGapSeconds`
  (ranges), `whirlpoolBiteMultiplier` (~0.05), circle sprite/color.

## Whirlpool Hotspots (in scope, placeholder art)

A single roaming **dark-blue circle** on the water is the reward for aiming well.
Owned by `WhirlpoolManager` (see Architecture).

- **Behavior:** one at a time; spawns at a random reachable water point, lives
  `whirlpoolLifetimeSeconds`, despawns, waits `whirlpoolGapSeconds`, repeats.
- **Payoff:** if the bobber's landing point is within `whirlpoolRadius`, that cast's
  bite time is multiplied by `whirlpoolBiteMultiplier` (~0.05 → a near-instant bite),
  baked into `biteReadyUtcTicks` at cast. A miss casts normally.
- **Snapshot, not continuous (this pass):** the boost is decided once, at cast. A line
  already in the water is unaffected by a whirlpool appearing/despawning later, and the
  farther refinement — boost applies only *while* the bobber sits in a live whirlpool
  and fades when it goes — is a Non-Goal here (noted above).
- **No persistence / no offline sim:** whirlpools respawn fresh on reload; the earned
  speed-up already lives in the cast's saved bite time.

This is deliberately minimal — enough to answer "is aiming fun?" before investing in
real art or the dynamic model.

## Testing

Extend `FishingMathTests` / `FishingManager` coverage (EditMode):

- Power→distance normalization is monotonic and clamped to `[0, maxCastRange]`.
- `reelTapsTotal` scales with `castPower01` between `minReelTaps` and `maxReelTaps`.
- `Reel()` decrements; reaching 0 in **Bite** deposits to Pantry, in **Waiting**
  returns to Idle with no deposit.
- `Cast` guards (`Idle` + `hasPole`) still hold; bite time still rolled/anchored.
- `Cast(power, dir, multiplier)` scales the bite seconds by the multiplier (a `< 1`
  value yields a proportionally earlier `biteReadyUtcTicks`); default `1f` is unchanged.
- `WhirlpoolManager.BiteMultiplierAt` returns the boost inside `whirlpoolRadius` and
  `1f` outside / when no whirlpool is active (pure point-in-circle; EditMode-testable).
- Save/load round-trips the new fields; offline Waiting→Bite still resolves.

## Edge Cases

- Release off-water / over UI / drag away while charging → cancel, no cast.
- Camera must be settled at the Lake (existing `CanInteract`).
- No pole → existing hint; no charge.
- Bite mid-reel → bubble appears, finishing the reel lands the catch.
- Reticle shown only while charging; cleared on release/cancel.
- Max-distance spots can't overshoot (meter caps at full) — acceptable.
- Whirlpool spawns only where it's reachable (inside water **and** within
  `maxCastRange`), so there's never an un-castable target.
- No active whirlpool when you cast → normal bite; a cancelled cast never queries it.

## Rollout

Single branch (`feat/run-ender-economy`, where all Pantry Economy work lives).
Placeholder art throughout; visual playtest after build. No migration needed — new
`GameData` fields default to zero, which reads as an Idle line.
