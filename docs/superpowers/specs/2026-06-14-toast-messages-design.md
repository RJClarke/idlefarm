# Toast Messages — Design

**Date:** 2026-06-14
**Branch:** feat/run-ender-economy (UI polish work)
**Status:** BUILT & VERIFIED in play mode (2026-06-14)

## Implementation notes (what changed during the build)

- **Animation is coroutine-driven.** The lifecycle uses a MonoBehaviour coroutine writing
  `opacity`/`translate` each frame with `Time.unscaledDeltaTime` (works through
  `Time.timeScale` changes during runs). This is the verified, shipped approach. (A
  USS-transition / `VisualElement.schedule` version was tried first and *appeared* not to
  tick — but that was really the unfocused-editor issue below, not a panel problem.
  Coroutines were kept because they're proven and robust.)
- **Panel build fix:** `AddComponent<UIDocument>()` runs `OnEnable` before `panelSettings`
  is assigned, so the panel never builds. Fixed by `enabled = false; panelSettings = …;
  enabled = true;` in `Awake`.
- **Verification gotcha:** the Unity Editor does not tick the player loop while unfocused,
  so MCP-driven play tests appeared frozen (time-based coroutines stalled). Real gameplay
  is unaffected. Confirmed working once the loop ticked.
- Verified: 3 stacked toasts visible, 4th queued (MAX_VISIBLE=3), emoji (✨/🔓) render
  correctly in UITK, gold accent for research / green for unlock, drawn above gameplay.

## Goal

Add transient celebratory "toast" banners for big completed milestones — research
completing, first-time unlocks, etc. Distinct from the existing `FloatingTextManager`
(world-anchored `+$`/`+G` currency pops drifting off harvested crops).

Not in scope: error/blocked-action nags, info/status notices, tap-to-dismiss, sound
(can be added later), persistence.

## UX

- **Placement:** top of screen, just below the top bar.
- **Animation:** slides down into view (~0.25s) → holds (~2.2s) → slides back up while
  fading (~0.3s) → removed.
- **Look:** rounded dark translucent pill, light text, optional leading emoji/icon,
  soft drop shadow. Matches the project's UI Toolkit aesthetic (reuses theme via the
  shared PanelSettings).
- **Stacking:** up to 3 visible at once, newest at top, older ones pushed down. Beyond
  3, extras queue and appear as visible slots free up.

## Architecture

### ToastManager (singleton MonoBehaviour)

Mirrors the `FloatingTextManager` "build everything in code" philosophy so no scene
wiring beyond placing one GameObject is required.

- Owns a runtime `UIDocument`. PanelSettings is a **clone** of the shared
  `RunewoodPanelSettings` (`ScriptableObject.Instantiate`) with `sortingOrder` bumped
  from 1000 → **2000**, so toasts render above all popups. The shared settings are
  referenced via `[SerializeField] private PanelSettings sourcePanelSettings;`
  (assigned in-scene); if null, falls back to a fresh `CreateInstance` configured for
  1080×1920 `ScaleWithScreenSize`.
- Builds a top-anchored flex **column container** (`toast-stack`) as the panel root's
  child: anchored top-center, padded down from the top bar, `align-items: center`.
- Public API:
  ```csharp
  public static void Show(string message, ToastKind kind = ToastKind.Success);
  // ToastKind { Success, Unlock }  — drives the leading icon + accent only.
  ```

### Stack / queue logic

- `visibleCount` capped at `MAX_VISIBLE = 3`. A `Queue<PendingToast>` holds overflow.
- `Show` enqueues; a pump method shows the next pending toast whenever a slot is free.
- Each toast element lifecycle, driven by the UITK `schedule` API (unscaled,
  survives `Time.timeScale` changes during runs):
  1. Added to the top of `toast-stack` at opacity 0, translated up by its own height.
  2. Inline USS transition on `opacity` + `translate` animates it into place.
  3. `schedule.Execute(dismiss).StartingIn(HOLD_MS)` reverses opacity + translate.
  4. On dismiss-complete, the element is removed, `visibleCount--`, pump runs again.

### Triggers (initial wiring)

`ToastManager.Start` subscribes (null-guarded, like other managers):

| Event | Toast |
|-------|-------|
| `ResearchManager.OnResearchLeveledUp(id, lvl)` | `✨ {research name} researched!` |
| `AnimalManager.OnAnimalUnlocked(id)` | `🔓 {animal name} unlocked!` |

Research name via `ResearchManager.GetResearch(id).displayName`; animal name via
`AnimalManager.GetAnimalData(id).displayName`. `OnFeatureFlagUnlocked` is intentionally
**not** wired — it fires inside the same level-up path, so it would double-toast with
`OnResearchLeveledUp`. The API is generic, so new triggers are one-liners later.

> Emoji (`✨`/`🔓`) render through the UITK panel text settings; if play-test shows
> boxes, swap to a colored accent stripe (already on the pill border). Verify in play mode.

## Files

- **New:** `Assets/Scripts/UI/ToastManager.cs`
- **Scene:** one new `ToastManager` GameObject (created via MCP) with the component;
  `sourcePanelSettings` ← `RunewoodPanelSettings`.

## Verification

Enter play mode, fire a research level-up / unlock (or a temporary debug key), and
confirm: toast slides from the top, holds, slides away; multiple in quick succession
stack to 3 and queue the rest; renders above open popups.
