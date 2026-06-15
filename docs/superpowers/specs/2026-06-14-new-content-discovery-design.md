# New-Content Discovery (toast + NEW badges) — Design

**Date:** 2026-06-14
**Branch:** feat/run-ender-economy (UI polish)
**Status:** BUILT (boots clean; awaiting behavioral playtest — see note)
**Builds on:** [toast system](2026-06-14-toast-messages-design.md)

> **Verification note:** Because first run seeds all *currently-available* content as
> "seen", badges/toast only appear for content unlocked *after* this ships. To test, unlock
> something not yet owned — e.g. the **chicken** (reveals Chicken: Cooldown/Efficiency
> research) or the **scarecrow** (reveals its section + research). Greenhouse must be built
> for its world dot to show. World-badge offset/size on GreenhouseBuilding may need tuning.

## Goal

When the player unlocks something (e.g. the Scarecrow), make the newly-available content
*discoverable*: research entries gated behind that unlock (`Scarecrow: Range`, `Scarecrow:
Refresh`) and the scarecrow equipment upgrades. Two cues:

1. **Toast** at the unlock moment naming what opened up.
2. **Persistent NEW badges** (notification dots) that lead the player to it and clear once seen.

Data-driven off `ResearchManager.IsResearchVisible` + the unlock→content map, so it covers
every current and future unlock, not just the scarecrow.

## Clear behavior (per user)

- **Research rows:** per-item; a row's dot clears when the player **clicks the row to view it**.
- **Equipment rows:** per-item; a row's dot clears when the row **scrolls into view** in the
  Equipment menu.
- **Aggregate "trail" dots** (Equipment nav button, research building) clear automatically
  once nothing unseen remains in their area.

## Architecture

### `NewContentTracker` (new singleton MonoBehaviour, persisted)

- `HashSet<string> seen` — content IDs already seen. ID scheme: `research:{researchID}`,
  `equip:{equipmentID}`.
- Available sets (computed live):
  - Research: every `researchID` with `ResearchManager.Instance.IsResearchVisible(id)`.
  - Equipment: every equipment ID currently unlocked/shown in the Equipment menu
    (from `EquipmentManager`).
- API: `bool IsNew(string id)` (available && !seen), `void MarkSeen(string id)`,
  `bool HasUnseenResearch()`, `bool HasUnseenEquipment()`, `event Action OnChanged`.
- `MarkSeen` adds to `seen`, fires `OnChanged`, and nudges a save (AutoSaveManager debounce).
- **First-run seeding:** on first load with no saved `seen` set, mark everything
  *currently available* as seen, so existing content doesn't all light up as NEW. Only
  content that becomes available *after* that baseline shows as NEW.

### Toast on unlock

- Subscribe to `UpgradeManager.OnUpgradePurchased(upgradeID)`.
- Compute what that unlock opened: researches whose `requiredUnlockID == upgradeID` (now
  visible) + equipment unlocked by it. (The newly-available IDs are automatically "unseen".)
- If non-empty, fire one toast: headline `🔓 {Name} Unlocked!`, subtitle e.g.
  `"2 new researches + farm upgrades"`. Uses the existing `ToastManager.Show(title, subtitle, Unlock)`.

### Badges

Reusable small dot visuals; reuse existing notification-dot patterns where present.

- **Equipment nav button** (uGUI, `BottomNav`): a dot child shown when `HasUnseenEquipment()`.
- **Equipment popup rows** (`EquipmentPopupUITK`, UITK): per-row dot when `IsNew(equip:id)`;
  clears via `GeometryChangedEvent` / viewport check when the row is on-screen in the scroll.
- **Research building** (`ShopBuilding` that opens the research popup): a dot shown when
  `HasUnseenResearch()`.
- **Research popup rows** (`ResearchPopupUITK`, UITK): per-row dot when `IsNew(research:id)`;
  clears in the row's existing click/view handler.

All badge holders subscribe to `NewContentTracker.OnChanged` to refresh.

### Persistence

- Add `string[] seenContentIds` to `GameData`; `SaveManager` load → `NewContentTracker.LoadState`,
  save → `GetState`. Auto-save already debounces on game events.

## Build order

1. `NewContentTracker` + `GameData`/`SaveManager` persistence + first-run seeding.
2. Smart unlock toast via `OnUpgradePurchased`.
3. Research: row dots (click-clear) + research-building dot.
4. Equipment: row dots (scroll-clear) + Equipment nav-button dot.

## Verification

Unlock the scarecrow in play mode → toast fires naming new content; research building +
Equipment nav show dots; opening research shows dots on the two scarecrow rows that clear
when clicked; equipment rows' dots clear when scrolled into view; dots stay cleared across
a save/reload.

## Out of scope (v1)

Animated/“!” pulsing badges, per-branch header dots in research, badges for non-research/
non-equipment unlocks (helpers, zones) — easy to add later via the same tracker.
