# Woodcutting v1 — Design

**Date:** 2026-06-30
**Branch context:** brainstormed on `feat/run-ender-economy`
**Status:** Design approved, pending spec review → implementation plan

## Summary

A new **active gathering area** in the bottom-right of the world where the player chops
trees for **Wood**. It is the deliberate opposite of Research (top-right, cerebral,
set-and-forget): Woodcutting is spatial, hands-on, and something you *do* during run
downtime. Wood is a clean, tracked, persisted resource with two exits — **sell for Cash**
(Money, during a run only) and **sell for Gold** (Coins, anytime). Supply is throttled by
finite, regrowing trees so the Cash exit can never become an infinite bankruptcy-escape.
An **Axe** upgraded at the Carpenter unlocks harder tree types and reduces taps-to-fell.

This is **v1 of a larger ecosystem** (see §9). v1 ships the active area + the two sell
exits + axe progression, and models Wood as a resource that future systems (Construction,
Preserves) can consume without touching Woodcutting internals.

## Goals (why this exists)

1. **Anti-boredom / downtime filler** — give the player something to *do* while crops grow
   and helpers work. The interleave *is* the loop: farm idles → go chop → trees deplete →
   farm's ready again.
2. **A second economy layer** — Wood is a new resource, the seed of a future tech/crafting
   tree that coins alone can't buy.
3. **An alternative income faucet** — active effort converts to real income (Cash and Gold),
   rewarding players who lean in.

## Non-goals (explicitly deferred)

- **Passive / auto-chop.** v1 is active-only. Helpers auto-chopping, and auto-feeding a
  future "engine," come later.
- **Faster regrowth as an axe effect.** Regrow-speed tuning belongs to Research / other
  upgrades later, NOT the axe.
- **Construction and Preserves/Crafting systems.** Documented as future consumers (§9); not
  built in v1.
- **Multiple distinct wood *types* as separate resources.** v1 has one "Wood" resource;
  node variety affects yield/speed only (§4).
- **Stump chipping as a filler mechanic.** Parked — a stump that gives 1/tap is just a slow
  tree and adds no real value. Revisit only if the "nothing to tap" gap actually bites in
  playtest.

## Core loop

```
pan to Woods (bottom-right)
  → tap a tree repeatedly → tree falls → +Wood → stump remains
  → stump regrows to full tree on a timer (the throttle)
  → click the wood rack → sell Wood for Cash (in-run) or Gold (anytime)
  → upgrade Axe at Carpenter → unlock harder trees + fewer taps-to-fell
```

## Design detail

### 1. Location & access
- New **bottom-right nav button** ("Woods", axe icon). Tapping pans the camera down-right to
  a forest strip beside the farm.
- Implemented via the existing camera-pan location system (`LocationModeController`), the
  same mechanism as Market / Greenhouse. **Same world**, so weather / sway / cloud shadows /
  YSort already apply for free, and the farm stays partly in frame ("your farm's still
  going").
- No new scene — a new framed camera area plus props (trees, wood rack) placed in
  `SampleScene`.

### 2. Trees / nodes
- Trees are **world sprites** (YSort-participating). Tree needs at minimum 2 visual states:
  **standing** and **stump**; optionally a brief **falling** beat. (Art dependency — see §10.)
- **Tap-repeatedly-to-fell.** Each tap = one chop hit; `hitsToFell` taps drop the tree and
  award its `woodYield`. No minigame.
- **Node variety** (drives sequencing/decisions, all yielding the same Wood resource):
  - *Softwood* — low `hitsToFell`, low `woodYield` (fast, small).
  - *Hardwood* — higher `hitsToFell`, higher `woodYield` (slow, big). **Gated behind an axe
    upgrade** (§5).
  - *(Future)* Rare tree — occasional, big payout.
- **Regrowth (the throttle):** after felling, the stump regrows to a standing tree after
  `regrowSeconds`. This caps Wood/minute at the source — the reason no separate Cash cap is
  needed. `regrowSeconds` is data-driven and later tunable by Research (NOT the axe).

### 3. Chop feel
- Satisfying, mobile-first: tap → chop SFX + small shake/particle + a chip of progress; on
  the final hit the tree falls with a floating `+N Wood`.
- Number of trees on screen is small (e.g. 3–5) so "I cleared them" is the natural cue to go
  back to the farm — this is a feature, not a dead end.

### 4. Wood as a resource
- A single tracked **Wood** quantity, persisted, with an `OnWoodChanged` event — modeled on
  the existing **Compost** currency pattern (integer, event-driven, offline-safe storage).
- Home: extend `CurrencyManager` (alongside Coins/Money/Compost) or a small dedicated
  manager that mirrors its shape. **Decision for the plan:** prefer extending
  `CurrencyManager` for consistency unless it bloats the file past comfort.
- This event + accessor pair is the **stable interface** future consumers (Construction,
  Preserves) depend on.

### 5. Axe & Carpenter progression
- New **"Tools" section** in the Carpenter shop (existing shop UI). Buy the starter Axe,
  then a small number of upgrade levels.
- **Axe upgrade effects (v1):**
  1. **Unlock harder tree types** — level 1 → can fell Hardwood (more wood per tree because
     you can now chop a bigger tree AND it yields more).
  2. **Reduce taps-to-fell** on already-choppable (softer) trees — each upgrade lowers
     `hitsToFell`, so softwood cuts faster.
- **Axe upgrades do NOT affect regrow speed** (that's Research/other later).
- **Cost:** Coins **+ Wood** — deliberately self-referential ("spend wood to chop wood
  better") as an early hook. Optionally gated by achievement-style triggers ("chop N trees")
  in a later pass; v1 can ship with straight Coin+Wood costs.
- Persisted axe level drives which tree types are choppable and the effective `hitsToFell`.

### 6. Wood rack (sell UI)
- A clickable **wood rack** prop near the trees opens a small panel (styled with the
  `UI_Runewood` kit). Shows current Wood and two actions with **stack controls** (x1 / x10 /
  All, or a slider):
  - **Sell for Cash 💵** — adds to run **Money**. **Enabled only during an active run**;
    otherwise greyed with a hint ("Start a run to sell for Cash"). Priced so a full forest's
    worth ≈ "a few more minutes" of run fuel — a *limited* lifeline.
  - **Sell for Gold 🪙** — converts Wood → **Coins**. **Always enabled.** Slower rate; the
    patient, uncapped bank. Powers permanent upgrades.
- Run-state gating hooks the existing `RunManager` run-active state / `OnRunStarted` /
  `OnRunEnded` events.

### 7. Throttle summary (why it's not an infinite money button)
- Cash income is bounded by **wood supply**, and supply is bounded by **finite regrowing
  trees**. You physically cannot extract more than the forest regrows. The Cash exit extends
  a stalling run by minutes, not indefinitely. (Market price-decay on Cash sales remains a
  future option if playtest shows the node throttle isn't enough.)

### 8. Persistence & save
- Add to `GameData` JSON: **wood quantity**, **per-tree regrowth state** (which are stumps +
  their regrow timestamps, UtcNow-based for offline correctness), **axe level**.
- Save on the existing triggers (currency-change event, autosave debounce, app pause/quit).
- Offline: stumps regrow based on elapsed UtcNow, consistent with Compost/Research offline
  catch-up.

### 9. Documented future consumers (NOT built in v1)
- **Construction** — spend Wood to build greenhouse / chicken coop / barn extension / fence.
  A *burst goal-sink* (each build consumes once). Reuses existing equipment/animals/zone
  systems.
- **Preserves / Crafting** — bank grown crops mid-run → a furnace **burns Wood** → cooldown
  (4–8h) → yields 4–8× value in Coins vs. cashing out in-run. The *recurring engine-sink*
  that keeps Wood relevant into the late game and enables a "go heavy into wood" playstyle.
- Both are separate specs later. v1 only guarantees the Wood resource interface (§4) they
  will consume.

### 10. Asset inventory & dependencies
Ready to use:
- **Sell panel UI:** `Assets/Sprites/UI/UI_Runewood/` (wood-themed frames, buttons, sliders,
  scrollbars, slots — thematically perfect).
- **Wood/resource icons:** `Assets/Sprites/UI/Icons/Cute/RpgResources/Log.png`,
  `LogSplit.png`; `Assets/Sprites/UI/UI_Book/UI_NoteBook_IconLog01a.png`.
- **Axe icons:** `Assets/Sprites/UI/Icons/Cute/RpgResources/Axe.png`,
  `Assets/Sprites/UI/Icons/Cute/RpgItems/Axe_Magic.png`,
  `Assets/Sprites/UI/Icons/Raven/Misc_AxeSilver.png`.
- **(Branch option) Mining pack:** `Assets/Sprites/Mining/` — full rocks/ores/pickaxe set if
  a mine variant is ever wanted.

**Art dependency (open):** no clean standalone **world tree sprite** with chop states was
found (only `Plants/Tree_Bonsai.png`). Likely sliceable from environment sheets
(`Assets/Sprites/Environment/CL_MainLev.png` / `CL_Buildings.png`) or needs sourcing. v1
needs at least a **standing tree** + **stump** sprite (softwood + hardwood variants ideal).
Flag for the implementation plan; a placeholder can unblock code.

### 11. Testing
Pure, unit-testable logic (following the existing pure-logic + EditMode-test pattern):
- Chop math: `hitsToFell` countdown → fell → `woodYield` award.
- Regrowth timers, including offline elapsed catch-up.
- Cash/Gold pricing and stack (x1/x10/All) math.
- Sell gating: Cash disabled when no active run; Gold always enabled.
- Axe-upgrade effects: tree-type unlock gating + effective `hitsToFell` reduction.

## Open questions carried into the plan
1. Wood resource home: extend `CurrencyManager` vs. new `WoodManager` (lean: extend).
2. Exact starting numbers (`hitsToFell`, `woodYield`, `regrowSeconds`, Cash/Gold prices,
   axe costs) — tuning pass, placeholder values to start.
3. World tree sprite sourcing (see §10).
