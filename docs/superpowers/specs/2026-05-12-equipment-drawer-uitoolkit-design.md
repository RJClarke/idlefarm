# Equipment Drawer — UI Toolkit Rebuild

**Date:** 2026-05-12
**Status:** Design — pending user review
**Author:** Brainstorming session

## 1. Goal

Rebuild the Equipment drawer as a standalone UI Toolkit popup (`EquipmentPopupUITK`) so it scales to many equipment items with variable numbers of upgrade rows. The current `EquipmentMenuPanel` hard-codes 3 cards × 3 upgrade rows by walking a pre-built scene hierarchy; this prevents adding more equipment without manual scene work.

The new popup:

- Slides up from the bottom, covering ~75% of the screen.
- Renders one section per equipment in a vertically scrolling list.
- Within each section, renders one row per upgrade path with horizontally scrolling level tiles.
- Reuses the existing `UpgradeData` + `UpgradeManager` purchasing system unchanged.

## 2. Non-goals

- Changing gameplay code that reads `EquipmentData.aoeUpgradeID` / `cooldownUpgradeID` / etc. — those fields stay.
- Migrating Market / Farm / Helpers / Settings drawer panels. `DrawerUI` keeps owning those.
- Designing a polished "locked equipment" state. For now, locked equipment renders as a single compact header row with a static "Locked — unlock at Market" hint; full design is deferred.
- Wiring `Sprinkler_Duration` upgrade into actual gameplay (extending `activeDurationSeconds`). The asset and `durationUpgradeID` field land in this spec; the gameplay hookup is a follow-up.

## 3. Decisions (from brainstorming)

| # | Decision |
| - | -------- |
| 1 | UI framework: **UI Toolkit, standalone popup** (not inside `DrawerUI`). Follows the QuestPopupUITK/AnimalPopupUITK/SeedSelectionPopupUITK/DailyRewardPopupUITK pattern. |
| 2 | Coexistence: **Swap the bottom-nav Equipment button immediately** to open the new popup. Old `EquipmentMenuPanel` + its scene hierarchy stay unreferenced until the new popup is proven, then deleted. |
| 3 | Data model: **EquipmentRegistry SO** holds the display-ordered list of `EquipmentData`. Each `EquipmentData` gains a new `UpgradeData[] uiUpgradeRows` field — direct asset references dragged in the inspector, ordered for display. Hardcoded `*UpgradeID` fields stay untouched (gameplay code unchanged). |
| 4 | Chrome: **Flat drawer** — rounded top, no full wood frame, no title bar. A small drag handle (visual only) and the word "Equipment" sit at the top of the drawer. |
| 5 | Locked equipment: **Section header only** — name + greyed icon + "Locked — unlock at Market" hint. No upgrade rows. |

## 4. Visual target

Reference (player-supplied mockup): equipment sections with the equipment name above, an icon column on the left, and upgrade rows on the right. Each row has a title, a one-line description, and a horizontally scrolling strip of five level tiles. Tiles have three states:

- **Bought** — faded background, level/bonus text muted, ✓ at the bottom.
- **Affordable** — dotted border around the tile, full opacity, cost shown with coin glyph.
- **Can't afford** — dimmed to ~40% opacity, cost shown but muted.

Browser mockup reviewed and approved (see `.superpowers/brainstorm/.../equipment-drawer.html`).

## 5. File layout

```
Assets/UI/EquipmentPopupUITK/
  EquipmentPopupUITK.uxml         — root, drawer container, ScrollView for sections
  EquipmentPopupUITK.uss          — styles (drawer chrome, sections, rows, tile states)
  EquipmentSectionTemplate.uxml   — one per unlocked equipment (header + icon column + rows container)
  EquipmentLockedSectionTemplate.uxml — compact header-only row for locked equipment
  EquipmentRowTemplate.uxml       — one per upgrade row (title + desc + horizontal tile strip)
  EquipmentTileTemplate.uxml      — one level tile

Assets/Scripts/UI/
  EquipmentPopupUITK.cs           — controller (singleton, Open/Close, RefreshAll, BindSection, BindRow, BindTile)

Assets/Scripts/
  EquipmentRegistry.cs            — new ScriptableObject (List<EquipmentData> equipment)

Assets/Data/Equipment/
  EquipmentRegistry.asset         — instance of the registry SO
  Sprinkler_Duration.asset        — new UpgradeData (5 levels)
```

## 6. Data model

### EquipmentRegistry.cs (new)

```csharp
[CreateAssetMenu(fileName = "EquipmentRegistry", menuName = "Farm Game/Equipment Registry")]
public class EquipmentRegistry : ScriptableObject
{
    public List<EquipmentData> equipment = new List<EquipmentData>();
}
```

### EquipmentData.cs (additive changes)

Add two fields, no removals:

```csharp
[Header("UI Display")]
[Tooltip("UpgradeData assets to display in the Equipment popup, in row order.")]
public UpgradeData[] uiUpgradeRows;

[Header("Sprinkler-only upgrade IDs")]
[Tooltip("Upgrade ID for sprinkler active-duration boost. Only used by Sprinkler.")]
public string durationUpgradeID = "";
```

`uiUpgradeRows` is the source of truth for what the popup renders. Per-equipment expected contents:

- **Scarecrow**: `[Scarecrow_AoE, Scarecrow_Capacity, Scarecrow_Cooldown]` (Capacity is labelled "Effectiveness" in the UI — comes from the asset's `displayName`).
- **Fence**: `[Fence_AoE, Fence_Capacity, Fence_Cooldown]`.
- **Sprinkler**: `[Sprinkler_AoE, Sprinkler_WaterPower, Sprinkler_Cooldown, Sprinkler_Duration]`.

`durationUpgradeID` is the gameplay-side string ID for the new Sprinkler Duration upgrade — parallel to `aoeUpgradeID`/`waterPowerUpgradeID`/etc. The popup doesn't read it (it reads `uiUpgradeRows`); a gameplay follow-up will read it when actually extending `activeDurationSeconds`.

## 7. Lifecycle

Same pattern as the four existing UITK popups.

```
Awake   → singleton + cache UIDocument
OnEnable → CacheElements, WireCallbacks, TrySubscribeEvents
OnDisable → UnsubscribeEvents
```

**Open()**
1. If already open, return.
2. `root.pickingMode = PickingMode.Position` (start absorbing clicks).
3. `popup-root.style.display = Flex`.
4. `popup-root.schedule.Execute(() => AddToClassList("open")).StartingIn(0)` — triggers USS slide-up.
5. `RefreshAll()` — rebuild sections.

**Close()**
1. Remove `.open` class.
2. After 260ms: `Display = None`; `root.pickingMode = Ignore` so uGUI behind the popup stays interactive.

**Backdrop click** → `Close`. **Equipment bottom-nav click while open** → `Close` (toggle).

## 8. Build & bind

`RefreshAll()` clears the section ScrollView and iterates `registry.equipment`:

```
foreach (EquipmentData eq in registry.equipment)
{
    if (eq.IsUnlocked())
        SpawnUnlockedSection(eq);
    else
        SpawnLockedSection(eq);
}
```

`SpawnUnlockedSection(eq)`:
1. Instantiate `EquipmentSectionTemplate`.
2. Bind name label, icon image (from `eq.iconSprite`).
3. For each `UpgradeData data` in `eq.uiUpgradeRows`:
   - Skip null entries (logs a warning so unconfigured slots are noticed).
   - Instantiate `EquipmentRowTemplate`, bind `data.displayName` + `data.description`.
   - For levels 1..maxLevel: instantiate `EquipmentTileTemplate`, call `BindTile(tile, data, level)`.

`SpawnLockedSection(eq)`:
1. Instantiate `EquipmentLockedSectionTemplate`.
2. Bind name, icon (low opacity), static "Locked — unlock at Market" hint label.

`BindTile(tile, data, level)`:
- Read `int permLevel = UpgradeManager.GetPermanentLevel(data.upgradeID)`.
- Apply state:
  - `level <= permLevel` → `.tile--bought` + ✓.
  - `level == permLevel + 1` and `CurrencyManager.CanAffordCoins(cost)` → `.tile--affordable`, wire click.
  - Otherwise → `.tile--cant-afford`.
- Tile click handler (only attached for affordable):
  ```
  if (UpgradeManager.PurchasePermanentUpgrade(data.upgradeID, cost))
      RefreshAll();
  ```

## 9. Live refresh

Subscribe in `TrySubscribeEvents`:
- `UpgradeManager.Instance.OnUpgradePurchased += _ => MarkDirty();`
- `CurrencyManager.Instance.OnCoinsChanged += _ => MarkDirty();` (event exists — verified at `CurrencyManager.cs:26`)

`MarkDirty()` sets a flag and schedules `RefreshAll()` at most every 200ms via `root.schedule` — keeps coin-change spam from causing flicker. Skip the refresh entirely while `!isOpen`.

Use the same `TrySubscribeEvents` guard pattern as the other popups to handle manager-startup race conditions.

## 10. BottomNav integration

The Equipment button currently calls `DrawerUI.Instance.OpenMenu(MenuType.Equipment)`. Change it to:

```csharp
void OnEquipmentButtonClicked()
{
    // Close any other drawer first so we don't stack panels.
    if (DrawerUI.Instance != null && DrawerUI.Instance.IsAnyMenuOpen())
        DrawerUI.Instance.CloseDrawer();

    if (EquipmentPopupUITK.Instance == null) return;
    if (EquipmentPopupUITK.Instance.IsOpen) EquipmentPopupUITK.Instance.Close();
    else EquipmentPopupUITK.Instance.Open();
}
```

Expose `EquipmentPopupUITK.IsOpen { get; }` for the toggle.

DrawerUI no longer routes `MenuType.Equipment`. The enum value stays for now to avoid touching unrelated callers; the equipment branch in `OpenMenu` becomes unreachable and is deleted along with the old `EquipmentMenuPanel`.

## 11. Styling

USS classes (key ones):

```
.drawer-root            — full-screen container (popup-root)
.drawer-backdrop        — semi-transparent scrim, fades in
.drawer-container       — the sliding panel (75vh, rounded top, translate-Y for animation)
.drawer-container.open  — translateY(0) (USS transition handles the slide)
.drawer-handle          — small horizontal pill at top
.drawer-title           — "Equipment" header text
.drawer-scroll          — vertical ScrollView

.section                — per equipment
.section-name           — equipment label
.section-body           — flex row: icon column + rows column
.icon-col / .icon-frame — fixed-width icon area with dashed border
.rows-col               — flex-grow rows container

.row                    — single upgrade row
.row-title / .row-desc  — text labels
.tiles                  — horizontal ScrollView (mode Horizontal, no vertical scrollbar)

.tile                   — base tile
.tile--bought           — faded + ✓
.tile--affordable       — dotted border + full opacity
.tile--cant-afford      — dim
.tile .lvl / .bonus / .footer — internal labels

.locked-section         — compact horizontal row for locked equipment
.locked-section .hint   — italic "Locked — unlock at Market"
```

Slide-up uses `transition: translate 0.25s ease-out` on `.drawer-container` plus `transition: opacity 0.25s` on `.drawer-backdrop`. No LeanTween needed.

## 12. New assets to create

1. **`EquipmentRegistry.asset`** — instance of `EquipmentRegistry`, with `equipment = [Scarecrow, Sprinkler, Fence]` in that display order.
2. **`Sprinkler_Duration.asset`** — new `UpgradeData`:
   - `upgradeID = "sprinkler_duration"`
   - `displayName = "Duration"`
   - `description = "amount of time sprinkler remains active"`
   - 5 levels of cost + bonus text (placeholder costs initially: 0/500/500000/1500000/10000000; bonuses: +0:05 / +0:05 / +0:05 / +0:05 / +5s — match mockup).
3. **Field updates on `Sprinkler` `EquipmentData`**:
   - `durationUpgradeID = "sprinkler_duration"` (for the gameplay follow-up).
   - `uiUpgradeRows = [Sprinkler_AoE, Sprinkler_WaterPower, Sprinkler_Cooldown, Sprinkler_Duration]`.
4. **Field updates on `Scarecrow` + `Fence` `EquipmentData`**: populate `uiUpgradeRows` with their existing 3 UpgradeData assets in display order.

## 13. Risks & mitigations

| Risk | Mitigation |
| ---- | ---------- |
| `UIDocument` PanelSettings raycast eats clicks on the game beneath when popup is closed | Use the established `root.pickingMode = Ignore` trick from QuestPopupUITK.cs:83 |
| `ScrollView` horizontal mode with flex children sometimes won't lay out tile widths correctly | Set explicit `flex-shrink: 0` and fixed width on `.tile`; QuestPopupUITK already wrestled with similar issues — reuse patterns |
| Live refresh causes flicker on every coin change | Debounce via scheduled refresh: mark dirty + refresh at most every 200ms |
| Old `EquipmentMenuPanel` still has `Update()` running its 1-second tile refresh and could conflict | After bottom-nav swap, the GameObject hosting EquipmentMenuPanel never activates (DrawerUI no longer routes to it); leave inactive in scene until cleanup |
| Spec quietly drops a row if a `uiUpgradeRows` slot is left null | `RefreshAll` skips null entries but logs a warning naming the equipment so misconfigurations are visible in the console. |

## 14. Parallel-test / cleanup plan

1. Build the new popup + assets behind a feature branch.
2. Swap the BottomNav Equipment button to `EquipmentPopupUITK.Open()`.
3. Verify: open from BottomNav, close via backdrop, close via the same button, tile purchases work, locked equipment shows correctly, no input leaks through the closed popup.
4. Once approved, delete:
   - `Assets/Scripts/EquipmentMenuPanel.cs`(+.meta)
   - `Assets/Scripts/Editor/EquipmentPanelBuilder.cs`(+.meta)
   - The old EquipmentPanel hierarchy from `SampleScene.unity`
   - The `MenuType.Equipment` branch in `DrawerUI.OpenMenu` / `ShowPanel` / `HideAllPanels` / `UpdateTitle`
   - (Keep `MenuType.Equipment` enum value if anything else references it; otherwise remove.)

## 15. Open questions (deferred)

- Locked-equipment polish: icon treatment, market deep-link, "unlock" CTA.
- Should the drawer have any "currently equipped on field N" affordance? Out of scope here; today this lives on home-screen tiles.
- Pull-to-dismiss (drag handle is currently decorative). Defer.
