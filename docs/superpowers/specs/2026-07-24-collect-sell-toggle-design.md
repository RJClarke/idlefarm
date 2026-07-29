# Collect/Sell Toggle — Always-Available Segmented Control

**Date:** 2026-07-24
**Status:** Approved, ready for implementation
**Branch context:** `feat/run-ender-economy`

## Problem

Two distinct defects in the Collect/Sell mode switch.

**1. Eggs cannot be banked outside a run.** Animal eggs mature on a wall-clock timer that is
independent of run state (`AnimalManager.cs:74-78` ticks every second regardless of
`RunManager.IsRunActive`). So the player claims eggs in town mode too. But the toggle is gated to
`inRun && atFarm` (`RunUI.cs:129`), leaving no way to switch collect mode on while out of a run.
The claim then falls through to the `else` branch at `AnimalManager.cs:399` and pays Coins instead
of banking an egg item — so Reputation quests that require eggs are unsatisfiable.

The toggle was gated this way because it is built as a clone of the Start Run button and shares its
exact anchor slot (`RunUI.cs:233-237`). Out of a run that slot is occupied by Start Run + Equip
Fields, so showing it there would overlap them.

**2. The label does not communicate state.** `RunUI.cs:259` renders `"Collecting"` when the mode is
on and `"Auto-Sell"` when off. Because the two labels are different nouns rather than one subject
with a changing state, the player cannot tell whether the text describes the current mode or the
action a tap will perform.

## Goals

- Collect mode is switchable whenever farm output can be claimed, in-run or out.
- The control's state is unambiguous at a glance.
- No change to economy behavior, save format, or the claim/harvest code paths.

## Non-goals

- Rebalancing what collect mode is worth. Out of scope.
- Per-item collect rules (e.g. "collect eggs but sell crops"). Single global flag stays.
- Surfacing inventory-full fallback (`AnimalManager.cs:393` already degrades to coins silently).

## Design

### 1. State and persistence — unchanged

`ItemInventoryCore.CollectMode` (`ItemInventoryCore.cs:24`) remains the single source of truth,
persisted as `collectModeOn` (`ItemInventoryCore.cs:84`, `ItemInventoryManager.cs:90,95`).

- No save-schema change.
- No changes to consumers: `Plant.cs` and `AnimalManager.cs:392`.
- `ItemInventoryManager.OnCollectModeChanged` already fires on `Import`
  (`ItemInventoryManager.cs:96`), so any subscribed widget refreshes correctly on load.
- The setter already early-returns when the value is unchanged (`ItemInventoryManager.cs:39`), so
  redundant sets do not spam the event.

### 2. New component: `CollectModeToggle`

Extract the widget into its own MonoBehaviour at `Assets/Scripts/UI/CollectModeToggle.cs` and delete
the corresponding code from `RunUI`.

Rationale:
- The toggle no longer shares Start Run's anchor slot, so cloning that button is pointless.
- `RunUI` already builds and owns five unrelated widgets; this reduces its surface.
- A standalone component owns its own subscribe/unsubscribe lifecycle, removing that concern from
  `RunUI.cs:490-491`.

Removed from `RunUI.cs`:
- `BuildCollectToggle()`, `OnCollectToggleClicked()`, `OnCollectModeChanged()`,
  `RefreshCollectToggleVisual()` (lines 212-266)
- fields `collectToggleBtn`, `collectToggleLabel`, `collectToggleBg`
- the call site at line 41
- the visibility line at line 129
- the `OnCollectModeChanged` unsubscribe at lines 490-491

### 3. Visual: two-segment control

Left segment `COLLECT`, right segment `SELL`. The active segment is filled and saturated; the
inactive one is dark. Showing both options at all times removes the state-vs-action ambiguity and
self-documents that a second mode exists.

Each segment is a wood panel reusing the HUD's `UI_Wood_Slot_Available_01_0` sprite, borrowed at
runtime from the existing InventoryButton rather than hardcoded by path.

**The state color tints the frame sprite, not a fill behind it.** That sprite is an *opaque* panel,
not a transparent-center frame, so anything drawn behind it is invisible — an earlier attempt tinted
a background Image and both segments rendered identically tan. Tint values are therefore pre-divided
by the sprite's own tan (~`0.85, 0.75, 0.60`) so `sprite x tint` lands on the intended final color:

| State | Tint | Resulting surface | Label |
|---|---|---|---|
| Collect active | `(0.52, 0.88, 0.50)` | mid green | near-black, bold |
| Sell active | `(0.95, 0.88, 0.35)` | warm amber | near-black, bold |
| Inactive | `(0.38, 0.34, 0.29)` | dark brown | cream, regular |

Contrast rule: the active surfaces come out light, where near-black reads strongest (~6.5:1 on the
green, ~9.8:1 on the amber; white would only reach ~3.5:1 on the green). The inactive surface is dark
brown, where black would be unreadable, so it gets cream (~6.7:1). All three states are legible, and
the dark-on-light vs light-on-dark flip reinforces which segment is selected. Bold on the active
label adds a second, non-color cue.

**Set, don't flip.** Each segment sets its mode explicitly (`CollectMode = true` / `= false`) rather
than inverting the bool. Tapping the already-active segment is a no-op. This matters because an
accidental inversion silently redirects output for as long as it goes unnoticed — the exact failure
that cost the reported Rep quest its eggs.

**Text rendering.** TMP labels with plain uppercase ASCII words. No emoji — they render invisible on
Android, and the project's `NotoSans` lacks assorted symbol glyphs. Sprite icons only if added later.

Labels use `NoWrap` and a **fixed** 20pt size, deliberately not auto-sizing. TMP's auto-size will not
shrink text to satisfy a width constraint while `overflowMode` is `Overflow`: with wrapping on it
tried to wrap the unbreakable word `COLLECT`, and with wrapping off it still settled at 25pt needing
129px inside a 115px segment, spilling past the frame both times. 20pt measures ~92px, clearing the
segment with room to spare, and a fixed size also keeps both labels the same size instead of letting
the shorter word render larger.

### 4. Placement

Directly under the top-right currency stack, as a child of the main HUD `Canvas`: anchored top-right
with pivot `(1,1)` at `(-20, -320)`, sized `260x64`. The 260 width matches the dev-button column so
the right edge reads as one stack.

Note the currency stack is **UI Toolkit** (`TopBarUITK`) while this control is uGUI, so the two cannot
share a layout container — vertical position is matched numerically. This is the same convention
`DevToolsSetup`/`QuestDebug` already use via their `CURRENCY_CLEARANCE = 300` constant. The legacy
uGUI currency texts (`MoneyText`/`CoinsText`/`GemsText`/`CompostText`) are all inactive and unused.

This control takes the slot the Dev Tools button used to occupy, so both dev drawers shift down past
it by a new `COLLECT_TOGGLE_CLEARANCE = 76f` (64px tall + 12px gap) in `DevToolsSetup.cs` and
`QuestDebug.cs`. Dev Tools moves from y `-320` to `-396`, Quest Debug from `-374` to `-450`.

The component is hosted on a bare `CollectModeToggle` child of `Canvas`, matching how `RunUI` and
`CurrencyUI` are hosted, and builds its container at runtime. Requires a scene edit to
`FarmMain.unity` (one empty GameObject + component).

### 5. Visibility

Shown at every location **except Market**.

| Location | In run | Out of run |
|---|---|---|
| Farm | shown | shown ← fixes the egg defect |
| Greenhouse | shown | shown |
| Lake / Woods | shown | shown |
| Market | hidden | hidden |

Farm-only was considered and rejected: unpenned idle animals follow the camera to the
Greenhouse/Woods (`AnimalVisual.cs:234-238`), so out of a run a chicken can be standing in the
Greenhouse when its egg matures. Hiding the toggle there would reproduce a quieter version of the
same defect. Gating on "not at Market" is the same single condition and closes that hole. Hiding at
Market matches the established `hideAtMarket` convention used by the other top-bar buttons
(`LocationModeController.cs:15-21,82`).

The gate follows the pattern `RunUI` already uses: read `CurrentLocation()` from
`CameraPanController`, and subscribe to both `OnPanStarted` and `OnPanCompleted`
(`RunUI.cs:44-56`). Subscribing to both matters because `CurrentLocation` only flips when a pan
*completes*, so a pan-start-only or completion-only refresh lets the button render one frame in the
wrong state.

### 6. Error handling

- Null `ItemInventoryManager.Instance` (domain reload, early frames): the click handler returns
  without acting and the visual refresh treats missing state as collect-off, matching
  `RunUI.cs:256`.
- Null `CameraPanController`: default to visible rather than hidden, so a wiring failure cannot
  silently reintroduce the unswitchable-mode bug.
- Subscribe in `OnEnable` / unsubscribe in `OnDisable` so the component survives being toggled.

## Testing

**EditMode** — extend `Assets/Tests/EditMode/ItemInventorySaveTests.cs`:
- repeated assignment of `CollectMode` is idempotent (the set-not-flip invariant the UI relies on)
- `CollectMode = false` survives an export/import round trip

Note the scope limit: `IdleFarm.EditModeTests.asmdef` references only `IdleFarm.EconomyCore` with
`overrideReferences: true`, deliberately excluding `Assembly-CSharp`. So the manager's
`OnCollectModeChanged` no-refire guard (`ItemInventoryManager.cs:39`) and the widget itself are *not*
EditMode-testable without breaching that boundary, which is not worth doing for a one-line delegation
and a UI builder. Both are covered by play-mode verification instead.

**Play mode** — via the `Temp/enter_play_mode.request` bridge. All verified:
- at the farm, out of a run, the toggle is visible and reads `COLLECT`/`SELL` correctly
- repeat taps on the active segment are no-ops (set-not-flip holds through the real Button wiring)
- collect mode + egg claim, **out of a run** → `eggs +1, coins +0`
- sell mode + egg claim, out of a run → `eggs +0, coins +30`
- hidden at Market, shown at Farm/Greenhouse/Woods/Lake
- 220/220 EditMode tests pass

Screenshots must come from `Temp/screenshot.request` (which uses
`ScreenCapture.CaptureScreenshot`) — `look_at_game_view` renders the world camera only and will not
show HUD overlay at all. Also disable the `OfflineProgressModalUITK` UIDocument before capturing:
its dim overlay mutes the HUD enough to make color judgements unreliable.

## Risks

- **Scene edit.** `FarmMain.unity` is already modified on this branch; the new GameObject adds to
  that diff.
- **Discoverability of the new mode.** Players used to the old in-run-only button will find the
  control in a new place. No migration needed — it's a UI move, not a data change.
- **Numeric alignment to a UITK panel.** If the currency stack ever grows a fifth row, this control
  and both dev drawers need their clearance constants bumped together. Pre-existing coupling; this
  change adds one more consumer of it.
