# Market Open During a Run

**Date:** 2026-07-25
**Status:** BUILT + play-verified
**Branch context:** `feat/run-ender-economy`

## Goal

Reach the Market during an active run so the player can check Reputation, work the town board,
deposit goods, and manage Carpenter tools (axe, fishing pole) without ending the run.

## Decision: everything stays open, nothing is disabled

The original idea was to block purchases mid-run behind a "Closed during farming" message. Rejected,
because the mechanics already enforce what that message would claim:

- **Crop unlocks** only matter at seed selection, and `RunUI.cs:117-119` hides both Start Run and
  Fields during a run, so seeds cannot be changed mid-run anyway.
- **Equipment unlocks** are applied by `EquipmentManager` in `OnRunStarted`; its
  `OnUpgradePurchased` handler (`EquipmentManager.cs:261-267`) deliberately skips the zone rebuild
  while a run is active, so gear bought mid-run lands next run on its own.

So nothing sold at the Market retroactively edits an active run. A "Closed" gate would add a rule the
player must learn in order to prevent something that cannot happen. There was also already a
precedent for mid-run management: `ShopBuilding` previously carved out the Greenhouse (Research) as
the one shop that stayed open during a run.

The run clock keeps ticking while shopping — threats spawn, crops grow and die. That is the intended
cost of the trip, and it is what keeps the visit a real decision.

## Changes

### 1. `CameraPanController.PanTo` — the actual gate

Lines 88-95 blocked `Location.Market` whenever a run was active, behind a `blockDuringRun`
serialized field. **This was the authoritative block**; the hidden nav button was only cosmetic. The
condition is removed, and `blockDuringRun` with it — that field's only reader was this branch, and
its tooltip ("pans are blocked while a run is active") was already inaccurate since only the Market
was ever blocked.

### 2. `MarketNavButton` — show the button mid-run

`ShouldHide` drops the run clause, leaving `current == Location.Market`. The
`OnRunStarted`/`OnRunEnded` subscriptions in `SubscribeExtra`/`UnsubscribeExtra` and the
`OnRunChanged` handler existed solely to refresh that visibility, so they are removed as dead code.
The pan subscription stays.

The return trip already worked: `RefreshBackVisibility` keys off location alone, never run state.

### 3. `ShopBuilding.CanInteract` — remove the run gate

Drops `if (inRun && shopType != ShopType.Greenhouse) return false;` and the now-unused `inRun` local.
Location and pan state are the only gates left.

Note the Greenhouse building has `requiredLocation = Greenhouse`, not Market — it is not a Market
building. It remains interactable at its own location during a run, exactly as before.

### 4. Threats never route through town — two layers

**Root fix.** `AnimalThreat`'s edge helpers (`ScreenTopY`, `ScreenBottomY`, `ScreenLeftX`,
`ScreenRightX`, `GetScreenEdgePoint`) computed spawn and exit points from
`Camera.main.transform.position`. They now compute from the **Farm** view rect via a shared
`GetFarmFrame` helper, falling back to the live camera when no `CameraPanController` exists (edit
mode, tests).

This matters beyond looks. The farm sits at x≈0 and the Market at x≈-100. Camera-relative, a deer
spawned while the player was shopping would enter at the market's edge and walk ~100 world units to
reach the crops — visually absurd (deer running through town) and an **exploit**: panning to town as
a wave spawned would delay its arrival dramatically. Farm-relative spawning keeps traversal distance
constant, so a wave costs the same whether the player shops, fishes, or stays home.

Signatures are unchanged, so `DeerThreat` and `CrowThreat` need no edits — including their exit
paths, which now leave via the farm edge rather than whatever screen the camera is on.

This is a **no-op during normal play**: parked at the farm, the camera position and the farm rect are
the same point, so spawn geometry is identical to before. It only diverges when the camera is
elsewhere, which is the bug.

**Guard layer.** `ThreatWaveManager`'s renderer hiding extends from Lake to Lake + Market via a new
`HidesThreats(Location)` helper; `threatsHiddenForLake` is renamed `threatsHidden` since it now
covers two locations. Redundant once spawns are farm-relative, kept as cheap insurance.

### 5. Livestock — already done

Handled the previous day: `AnimalVisual.IsPenned()` includes Market, and
`AnimalManager.GetHomeScreenSpawnPosition()` never spawns at the Market. No further work.

## Verification

Play-mode, with a run active (`RunManager.StartNewRun`):

| Check | Result |
|---|---|
| Pan to Market during a run | camera reached x = -100.0, `CurrentLocation = Market` |
| Market nav button at Farm, mid-run | visible |
| Plants / Equipment / Carpenter / Town Board at Market | all `CanInteract = true` |
| Back to Farm button at Market, mid-run | active |
| Greenhouse at its own location, mid-run | `CanInteract = true` (no regression) |
| Threat edges while standing in town | left −18.38, right 18.38 vs farm −16.88..16.88 |
| 72 edge points around the compass | x range −18.38..18.38, **0** inside the market rect |
| `threatsHidden` at Market / at Greenhouse | `true` / `false` |
| EditMode suite | 220/220 |

## Risks

- **Balance, not correctness.** Shopping mid-run is now a real option, and the run clock keeps
  running. Whether the time cost is correctly priced is a balance question for the tuning pass, not
  something this change settles.
- **Removed serialized field.** Dropping `blockDuringRun` discards its stored scene value. Harmless
  — nothing read it but the branch that was deleted.
