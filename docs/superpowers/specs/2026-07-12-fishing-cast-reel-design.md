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
- **Whirlpool hotspots** (placeholder dark-blue circle): while the bobber sits inside
  one, bites come fast (under ~30s). A whirlpool holds a few fish and then despawns, so
  it's an occasional "something to do," not a faucet. You can **reel into** one you
  overshot, so aiming rewards without demanding perfection.

## Non-Goals (this pass)

- **Whirlpool as a rarity boost.** For now it only speeds up the bite, not the odds.
- **Offline whirlpool simulation.** Whirlpools exist only while you're at the lake; they
  don't spawn or grant catches offline. (A fast bite you already earned is safe — it's
  baked into the cast's saved bite time.)
- **Reel physics / continuous drag.** Reeling stays discrete taps; the bobber doesn't
  free-slide, and you can't reel *outward* — only shoreward.
- Changing baseline bite timing, rarity odds, pole tiers, costs, or Pantry economy.
- Final art. Everything ships with **placeholder art** (emoji / simple sprites),
  matching how Pantry Economy shipped.
- Multiple cast origins / choosing which dock to fish from. One fixed pole.

## Decisions (from brainstorming)

| Question | Decision |
|---|---|
| Does cast distance matter mechanically? | **Yes, via whirlpools** (in this pass): while the bobber sits inside a whirlpool, bites come fast, so accurate aiming pays off. Baseline fishing (no whirlpool, or bobber outside) is unaffected. |
| Whirlpool in scope? | **Yes** — placeholder dark-blue circle, to playtest whether aiming is fun. |
| Whirlpool boost model | **Dynamic / position-based** — evaluated from the bobber's live position, so you can **reel into** a whirlpool you overshot. (Reeling is shoreward-only, so an undershoot can't reach it.) |
| Whirlpool bite speed | Fast but **not instant** — bite in **under ~30s** while inside (vs ~20 min baseline). |
| Whirlpool capacity | Holds **2–4 fish**; each bite consumes one; empties → despawns (also despawns at lifetime end). |
| Whirlpool cadence | Occasional, present-only, tunable. Deliberately throttled so fishing + woodcutting can't be farmed nonstop. |
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
- **Whirlpool hook (dynamic):** `Cast(power01, dir)` keeps its simple signature. The
  whirlpool influence instead arrives through the bobber's live position:
  `SetInHotspot(bool inside)` is called by `LakeNode` on each enter/exit transition.
  - **Enter** while `Waiting` and not yet biting → re-anchor a **fast** bite:
    `biteReadyUtcTicks = now + roll(hotspotBiteAvgSeconds)` (~20s avg → mostly <30s).
  - **Exit** before biting (reeled out the shore side, or the whirlpool despawned) →
    re-anchor a fresh **baseline** bite. Matches "back to normal when it's gone."
  - A fast bite that has already landed is baked into `biteReadyUtcTicks`, so it's
    **offline-safe** with no extra saved state.
  - Expose `bool CaughtFromHotspot` on the pending catch so `LakeNode` can tell
    `WhirlpoolManager` to consume one of the whirlpool's fish when a hotspot bite fires.

Reel/cast math (taps-from-distance, power→distance normalization) lives in
`FishingMath` as pure functions so it's unit-tested.

### `LakeNode` (rework) — input, gesture, meter, rendering
Extends today's pointer handling. Responsibilities:

- **Idle gesture:** press-down sets reticle + direction + target tick; drives the
  charge meter over time; on release calls `FishingManager.Cast(power01, dir)`.
- **Waiting/Bite gesture:** a tap on the water calls `FishingManager.Reel()`.
- **Whirlpool tracking (per frame, while Waiting/Bite):** compute the bobber's current
  world position, call `WhirlpoolManager.IsInside(...)`, and forward enter/exit to
  `FishingManager.SetInHotspot(...)`. When a hotspot bite fires, call
  `WhirlpoolManager.ConsumeFish()`.
- **Rendering** (reads `FishingManager` + its own authored `castOrigin`/`maxCastRange`):
  - Bobber sprite at
    `castOrigin + CastDir × (CastPower01 × maxCastRange × ReelProgress01)`
    (retreats toward origin as it's reeled). Swaps to an **agitated look** while inside
    a whirlpool.
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
  `whirlpoolGapSeconds` before the next. Ranges are tunable and intentionally sparse
  (see Whirlpool Hotspots → cadence/economy).
- **Reservoir:** at spawn it rolls `fishRemaining` in `[minWhirlpoolFish, maxWhirlpoolFish]`
  (default 2–4). `ConsumeFish()` decrements it; hitting 0 despawns the whirlpool
  immediately (the already-hooked fish still reels in normally).
- **Visual:** a semi-transparent **dark-blue circle** sprite (placeholder) with a slow
  rotate/pulse tween, sized to `whirlpoolRadius`. Optionally shows `fishRemaining` as
  small pips. It visibly **intensifies** while a bobber is inside it (see below).
- **Query:** `bool IsInside(Vector2 worldPoint)` → true if within `whirlpoolRadius` of
  the active whirlpool. Pure point-in-circle; EditMode-testable.
- **No persistence, no offline sim.** Whirlpools are present-only cosmetic targets that
  respawn fresh on reload; a fast bite already earned lives in the cast's saved bite
  time.

`LakeNode` drives the interaction each frame while `Waiting`: it computes the bobber's
current world position, calls `WhirlpoolManager.IsInside(...)`, and forwards
enter/exit transitions to `FishingManager.SetInHotspot(...)`. On a hotspot bite it
calls `WhirlpoolManager.ConsumeFish()`. While the bobber is inside, `LakeNode` swaps the
bobber to an agitated "in the whirlpool" look (spin/wobble) so the player knows to stop
reeling and wait.

### Charge meter UI
A **vertical fill bar** with a target tick, pinned to the **left near the shore**,
never over the water. World-space sprites anchored near the pole (pan with the camera at
the Lake). Visible only while charging.

**Art (from `Assets/Sprites/UI/UI_Craftpix/Bars.png`, sliced during implementation):**
- **Track/frame:** the gold vertical bar at sheet rect **(242, 913, 11×45)** — its tan
  interior reads as "empty."
- **Fill:** the solid green bar at **(84, 957, 52×6)**, used as a bottom-pivoted
  `SpriteRenderer` whose `localScale.y` = `CastPower01` (grows upward inside the frame;
  no mask needed since the fill is a plain rect). Green reads as "power/charge."
- **Target tick:** a thin marker sprite positioned at the pressed spot's distance level.
- These are thin at native res (11px wide); scale up in the scene. **Vector fallback:**
  if slicing is fussy, a plain two-sprite rect track+fill is fine — the mock proved the
  layout works either way.

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
  (ranges), `minWhirlpoolFish` / `maxWhirlpoolFish` (2–4), `hotspotBiteAvgSeconds`
  (~20s), circle sprite/color, agitated-bobber look.

## Whirlpool Hotspots (in scope, placeholder art)

A single **dark-blue circle** on the water is the reward for aiming well. Owned by
`WhirlpoolManager` (see Architecture).

### The player loop
1. A whirlpool appears at a random reachable spot; the reticle/meter let you aim at it.
2. **Cast at or past it.** Landing directly inside is best; overshooting is fine —
   because reeling is shoreward-only, you can **reel back through** a whirlpool you
   overcast. (Undershooting can't reach it: reeling only moves toward shore.)
3. When the bobber is **inside** the circle, it takes on an agitated look — your cue to
   **stop reeling and wait.** A bite comes fast (~under 30s vs ~20 min baseline).
4. **Reel the rest of the way in to bank** the fish (normal `Collect` at the pole).
5. The whirlpool holds **2–4 fish**; that bite consumed one. If any remain (and its
   lifetime hasn't ended), recast and repeat; otherwise it despawns.

### Rules
- **Dynamic, position-based boost.** The fast bite is granted while the bobber sits
  inside a live whirlpool (`FishingManager.SetInHotspot`), not snapshotted at cast.
  Leaving the circle before biting — or the whirlpool despawning under you — reverts to
  a baseline bite ("back to normal when it's gone").
- **Fast, not instant.** In-whirlpool bite averages `hotspotBiteAvgSeconds` (~20s) with
  the normal 0.5×–1.5× spread, so mostly 10–30s.
- **Capacity.** `fishRemaining` rolls 2–4 at spawn; each hotspot bite consumes one;
  reaching 0 despawns the whirlpool immediately (the hooked fish still reels in).
- **Rarity unchanged.** In-whirlpool casts use the pole's normal tier weights.

### Cadence & economy (the throttle)
Whirlpools are a periodic "nice thing to do," **not a faucet** — the explicit goal is
that fishing + woodcutting can't be farmed nonstop without breaking the economy.
Levers, all inspector-tunable:

- **Sparse spawns:** long `whirlpoolGapSeconds` between whirlpools.
- **Small reservoir:** 2–4 fish each.
- **Present-only:** no offline spawns or catches — you must actively be at the lake.

Starting defaults are deliberate guesses (e.g. lifetime ~90–150s, gap ~3–6 min, 2–4
fish); **final cadence is a playtest tuning target**, watched against total fish/hour
feeding the Pantry economy. This is minimal on purpose — enough to answer "is aiming
fun, and is the pace healthy?" before investing in real art.

## Testing

Extend `FishingMathTests` / `FishingManager` coverage (EditMode):

- Power→distance normalization is monotonic and clamped to `[0, maxCastRange]`.
- `reelTapsTotal` scales with `castPower01` between `minReelTaps` and `maxReelTaps`.
- `Reel()` decrements; reaching 0 in **Bite** deposits to Pantry, in **Waiting**
  returns to Idle with no deposit.
- `Cast` guards (`Idle` + `hasPole`) still hold; bite time still rolled/anchored.
- `SetInHotspot(true)` while Waiting re-anchors `biteReadyUtcTicks` to a fast roll;
  `SetInHotspot(false)` before biting re-anchors to a baseline roll; toggling has no
  effect once `Bite` is reached.
- `WhirlpoolManager.IsInside` is true within `whirlpoolRadius` and false outside / when
  no whirlpool is active (pure point-in-circle, EditMode-testable); `ConsumeFish`
  decrements and despawns at 0.
- Save/load round-trips the new fields; offline Waiting→Bite still resolves (a fast bite
  already anchored resolves offline; no whirlpool is simulated offline).

## Edge Cases

- Release off-water / over UI / drag away while charging → cancel, no cast.
- Camera must be settled at the Lake (existing `CanInteract`).
- No pole → existing hint; no charge.
- Bite mid-reel → bubble appears, finishing the reel lands the catch.
- Reticle shown only while charging; cleared on release/cancel.
- Max-distance spots can't overshoot (meter caps at full) — acceptable.
- Whirlpool spawns only where it's reachable (inside water **and** within
  `maxCastRange`), so there's never an un-castable target.
- Undershoot → bobber never reaches the whirlpool (reeling is shoreward-only); a normal
  bite applies. Overshoot → reeling passes through it and grants the fast bite.
- Whirlpool despawns (fish depleted or lifetime) while the bobber is parked inside
  waiting → treated as an exit; the pending bite reverts to baseline.
- Bobber sitting inside a whirlpool when it despawns still keeps any bite that already
  fired; only a not-yet-fired fast bite reverts.

## Rollout

Single branch (`feat/run-ender-economy`, where all Pantry Economy work lives).
Placeholder art throughout; visual playtest after build. No migration needed — new
`GameData` fields default to zero, which reads as an Idle line.
