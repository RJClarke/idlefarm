# Equipment Drawer (UI Toolkit) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the uGUI Equipment drawer with a standalone UI Toolkit popup (`EquipmentPopupUITK`) that scales to N equipment × N upgrade rows, with vertical section scrolling and horizontal level-tile scrolling.

**Architecture:** Singleton MonoBehaviour controller hosts a `UIDocument` with UXML templates (root + section + locked-section + row + tile). Data comes from a new `EquipmentRegistry` ScriptableObject. Sections are spawned dynamically on `Open()` from `EquipmentData.uiUpgradeRows` (a new field of direct `UpgradeData` references). Bottom-nav Equipment button reroutes to the new popup; old `EquipmentMenuPanel` stays in the scene unreferenced until cleanup.

**Tech Stack:** Unity 6 / UI Toolkit (UXML + USS + C#), existing `UpgradeManager` + `CurrencyManager` + `UpgradeData` system, `RunewoodPanelSettings.asset` (shared with other migrated popups).

**Testing note:** This is a Unity Editor UI feature. There is no automated test harness for play-mode UI in this project. "Test" steps mean: compile cleanly in the Editor and verify behavior visually in Play Mode. Steps describe the exact action to take and the exact thing to verify on screen.

---

## File map

**Create:**
- `Assets/Scripts/EquipmentRegistry.cs` — SO holding the ordered equipment list
- `Assets/Scripts/UI/EquipmentPopupUITK.cs` — controller singleton
- `Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uxml` — root layout
- `Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uss` — styles
- `Assets/UI/EquipmentPopupUITK/EquipmentSectionTemplate.uxml`
- `Assets/UI/EquipmentPopupUITK/EquipmentLockedSectionTemplate.uxml`
- `Assets/UI/EquipmentPopupUITK/EquipmentRowTemplate.uxml`
- `Assets/UI/EquipmentPopupUITK/EquipmentTileTemplate.uxml`
- `Assets/Data/Equipment/EquipmentRegistry.asset` — created in Unity (Right-click → Farm Game → Equipment Registry)
- `Assets/Data/Upgrades/Sprinkler_Duration.asset` — created in Unity

**Modify:**
- `Assets/Scripts/EquipmentData.cs` — add `uiUpgradeRows` and `durationUpgradeID`
- `Assets/Data/Equipment/Scarecrow.asset` — fill `uiUpgradeRows`
- `Assets/Data/Equipment/Fence.asset` — fill `uiUpgradeRows`
- `Assets/Data/Equipment/Sprinkler.asset` — fill `uiUpgradeRows` + set `durationUpgradeID`
- `Assets/Scripts/BottomNav.cs` — reroute Equipment button to the new popup
- `Assets/Scenes/SampleScene.unity` — add UIDocument GameObject (manual editor step)

**Deferred (after popup is approved):**
- Delete `Assets/Scripts/EquipmentMenuPanel.cs`(+.meta) and `Assets/Scripts/Editor/EquipmentPanelBuilder.cs`(+.meta)
- Remove the old Equipment panel hierarchy from SampleScene
- Remove the `Equipment` branch from `DrawerUI.OpenMenu`/`ShowPanel`/`HideAllPanels`/`UpdateTitle`

---

### Task 1: Add fields to EquipmentData

**Files:**
- Modify: `Assets/Scripts/EquipmentData.cs`

- [ ] **Step 1: Add the two new fields**

Open `Assets/Scripts/EquipmentData.cs`. Locate the `[Header("Upgrade IDs (must match UpgradeData assets)")]` block (line ~66) and the `aoeUpgradeID`/`cooldownUpgradeID`/`capacityUpgradeID` declarations.

Immediately above `[Header("Upgrade IDs ...")]`, add:

```csharp
    [Header("UI Display")]
    [Tooltip("UpgradeData assets to display in the Equipment popup, in row order. Drag the UpgradeData assets for this equipment in the desired display order.")]
    public UpgradeData[] uiUpgradeRows;
```

In the existing Sprinkler-specific section (after `waterPowerUpgradeID` and `waterPowerBonusPerLevel`), add:

```csharp
    [Tooltip("Upgrade ID for sprinkler active-duration boost (e.g. 'sprinkler_duration'). Gameplay code reads this when calculating active duration; the popup reads uiUpgradeRows instead.")]
    public string durationUpgradeID = "";
```

- [ ] **Step 2: Verify compilation**

Switch to Unity, wait for domain reload, check Console. Expected: no errors, no warnings about EquipmentData. Existing Scarecrow/Fence/Sprinkler assets retain all their previous values; the new `uiUpgradeRows` field is an empty array on each.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/EquipmentData.cs"
git commit -m "feat(equipment): add uiUpgradeRows and durationUpgradeID fields to EquipmentData"
```

---

### Task 2: Create EquipmentRegistry ScriptableObject

**Files:**
- Create: `Assets/Scripts/EquipmentRegistry.cs`
- Create: `Assets/Data/Equipment/EquipmentRegistry.asset` (in Unity editor)

- [ ] **Step 1: Write the script**

Create `Assets/Scripts/EquipmentRegistry.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered list of every EquipmentData asset that should appear in the Equipment popup.
/// Display order = inspector order.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentRegistry", menuName = "Farm Game/Equipment Registry", order = 7)]
public class EquipmentRegistry : ScriptableObject
{
    public List<EquipmentData> equipment = new List<EquipmentData>();
}
```

- [ ] **Step 2: Verify compilation**

Switch to Unity, wait for domain reload, check Console. Expected: no errors.

- [ ] **Step 3: Create the asset**

In Unity Project window: right-click `Assets/Data/Equipment/` → Create → Farm Game → Equipment Registry. Rename the new asset to exactly `EquipmentRegistry`.

Select the asset. In the Inspector, set `Equipment` size to 3 and drag in:
- Element 0 → `Scarecrow.asset`
- Element 1 → `Sprinkler.asset`
- Element 2 → `Fence.asset`

(This is the display order — Scarecrow first because it unlocks earliest.)

Save (Ctrl+S).

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/EquipmentRegistry.cs" "Assets/Data/Equipment/EquipmentRegistry.asset"
git commit -m "feat(equipment): add EquipmentRegistry SO listing display-ordered equipment"
```

---

### Task 3: Create Sprinkler_Duration UpgradeData asset

**Files:**
- Create: `Assets/Data/Upgrades/Sprinkler_Duration.asset` (in Unity)

- [ ] **Step 1: Create the asset**

In Unity: right-click `Assets/Data/Upgrades/` (or wherever existing UpgradeData assets like `Sprinkler_AoE` live) → Create → Farm Game → Upgrade Data. Rename to exactly `Sprinkler_Duration`.

If the menu name differs, look at an existing `Sprinkler_AoE.asset` in the Inspector to confirm the menu path (it's likely Farm Game → Upgrade Data).

- [ ] **Step 2: Fill values**

In the Inspector, set:

| Field | Value |
| ----- | ----- |
| Upgrade ID | `sprinkler_duration` |
| Display Name | `Duration` |
| Description | `amount of time sprinkler remains active` |
| Upgrade Type | `Stat` (same as `Sprinkler_AoE`) |
| Max Level | `5` |
| Base Coin Cost | `500` |
| Coin Cost Multiplier | `1000` (matches the steep scaling shown in the mockup: 500 → 500k → 1.5M → 10M; tune later if needed) |
| Bonus Per Level | `5` |
| Bonus Unit | `s` |
| Show Plus Sign | `true` |
| Icon | `⏱` |

Save (Ctrl+S).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Data/Upgrades/Sprinkler_Duration.asset"
git commit -m "feat(equipment): add Sprinkler_Duration UpgradeData (5 levels)"
```

---

### Task 4: Populate uiUpgradeRows on the three EquipmentData assets

**Files:**
- Modify: `Assets/Data/Equipment/Scarecrow.asset`
- Modify: `Assets/Data/Equipment/Fence.asset`
- Modify: `Assets/Data/Equipment/Sprinkler.asset`

- [ ] **Step 1: Scarecrow**

In Unity, select `Assets/Data/Equipment/Scarecrow.asset`. In the Inspector, expand `Ui Upgrade Rows`, set size to 3, and drag in (in this order):
- Element 0 → the UpgradeData asset whose `upgradeID == "scarecrow_aoe"` (likely `Scarecrow_AoE.asset`)
- Element 1 → the asset whose `upgradeID == "scarecrow_capacity"` (likely `Scarecrow_Capacity.asset`) — this is labelled "Effectiveness" via its `displayName`. **If the asset's `Display Name` is still `Capacity`, change it to `Effectiveness` and `Description` to `amount of crows repelled`.**
- Element 2 → the asset whose `upgradeID == "scarecrow_cooldown"`

- [ ] **Step 2: Fence**

Select `Fence.asset`. `Ui Upgrade Rows` size 3:
- Element 0 → Fence_AoE (or whichever asset has `upgradeID` = `fence_aoe`)
- Element 1 → Fence_Capacity / "Effectiveness" — if its `Display Name` isn't already `Effectiveness`, update it and set `Description` to `amount of deer repelled`
- Element 2 → Fence_Cooldown (description: `how often deer are repelled`)

- [ ] **Step 3: Sprinkler**

Select `Sprinkler.asset`. `Ui Upgrade Rows` size 4:
- Element 0 → Sprinkler_AoE
- Element 1 → Sprinkler_WaterPower — if its `Display Name` isn't `Effectiveness`, update it and set `Description` to `increase amount of water replenished`
- Element 2 → Sprinkler_Cooldown (description: `time between sprinkler uses`)
- Element 3 → Sprinkler_Duration (created in Task 3)

Also set `Duration Upgrade ID` to `sprinkler_duration`.

- [ ] **Step 4: Save and verify**

Save scene + project (Ctrl+S). In Project window, select each of the three equipment assets and confirm the `Ui Upgrade Rows` list is populated with the expected count.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Data/Equipment/Scarecrow.asset" "Assets/Data/Equipment/Fence.asset" "Assets/Data/Equipment/Sprinkler.asset" "Assets/Data/Upgrades/"
git commit -m "feat(equipment): populate uiUpgradeRows for Scarecrow/Fence/Sprinkler"
```

(The `Assets/Data/Upgrades/` add is in case any UpgradeData displayName/description was tweaked — git status will catch which files changed.)

---

### Task 5: Create UXML templates

**Files:**
- Create: `Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uxml`
- Create: `Assets/UI/EquipmentPopupUITK/EquipmentSectionTemplate.uxml`
- Create: `Assets/UI/EquipmentPopupUITK/EquipmentLockedSectionTemplate.uxml`
- Create: `Assets/UI/EquipmentPopupUITK/EquipmentRowTemplate.uxml`
- Create: `Assets/UI/EquipmentPopupUITK/EquipmentTileTemplate.uxml`

- [ ] **Step 1: Root UXML**

Create `Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="EquipmentPopupUITK.uss" />
    <ui:VisualElement name="popup-root" class="popup-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="backdrop" picking-mode="Position" />
        <ui:VisualElement name="drawer-container" class="drawer-container">
            <ui:VisualElement name="drawer-handle" class="drawer-handle" />
            <ui:Label name="drawer-title" text="Equipment" class="drawer-title" />
            <ui:ScrollView name="section-list" class="section-list" mode="Vertical" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Section template (unlocked)**

Create `Assets/UI/EquipmentPopupUITK/EquipmentSectionTemplate.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <ui:VisualElement class="section">
        <ui:Label name="equipment-name" class="section-name" />
        <ui:VisualElement class="section-body">
            <ui:VisualElement class="icon-col">
                <ui:VisualElement name="equipment-icon" class="icon-frame" />
            </ui:VisualElement>
            <ui:VisualElement name="rows-container" class="rows-col" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: Locked section template**

Create `Assets/UI/EquipmentPopupUITK/EquipmentLockedSectionTemplate.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <ui:VisualElement class="locked-section">
        <ui:VisualElement name="equipment-icon" class="icon-frame locked-icon" />
        <ui:VisualElement class="locked-meta">
            <ui:Label name="equipment-name" class="locked-name" />
            <ui:Label text="Locked — unlock at Market" class="locked-hint" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 4: Row template**

Create `Assets/UI/EquipmentPopupUITK/EquipmentRowTemplate.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <ui:VisualElement class="row">
        <ui:Label name="row-title" class="row-title" />
        <ui:Label name="row-desc" class="row-desc" />
        <ui:ScrollView name="tiles-scroll" mode="Horizontal" class="tiles-scroll">
            <ui:VisualElement name="tiles-strip" class="tiles-strip" />
        </ui:ScrollView>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 5: Tile template**

Create `Assets/UI/EquipmentPopupUITK/EquipmentTileTemplate.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <ui:VisualElement class="tile">
        <ui:Label name="tile-level" class="tile-level" />
        <ui:Label name="tile-bonus" class="tile-bonus" />
        <ui:Label name="tile-footer" class="tile-footer" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 6: Wait for import**

Switch to Unity and wait for the asset import. Check Console — expected: no UXML parse warnings.

- [ ] **Step 7: Commit**

```bash
git add "Assets/UI/EquipmentPopupUITK/"
git commit -m "feat(equipment): add UXML templates for EquipmentPopupUITK"
```

---

### Task 6: Create USS stylesheet

**Files:**
- Create: `Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uss`

- [ ] **Step 1: Write the stylesheet**

Create `Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uss`:

```css
/* ─────────────── Popup root + backdrop ─────────────── */

.popup-root {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
}

.backdrop {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.5);
    opacity: 0;
    transition-property: opacity;
    transition-duration: 0.25s;
}

.popup-root.open > .backdrop {
    opacity: 1;
}

/* ─────────────── Drawer container ─────────────── */

.drawer-container {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    height: 75%;
    background-color: rgb(179, 154, 110);
    border-top-left-radius: 24px;
    border-top-right-radius: 24px;
    translate: 0 100%;
    transition-property: translate;
    transition-duration: 0.25s;
    transition-timing-function: ease-out;
}

.popup-root.open > .drawer-container {
    translate: 0 0;
}

.drawer-handle {
    width: 48px;
    height: 4px;
    background-color: rgba(0, 0, 0, 0.25);
    border-top-left-radius: 2px;
    border-top-right-radius: 2px;
    border-bottom-left-radius: 2px;
    border-bottom-right-radius: 2px;
    margin-top: 8px;
    margin-bottom: 4px;
    align-self: center;
}

.drawer-title {
    -unity-font-style: bold;
    font-size: 16px;
    color: rgb(43, 43, 43);
    -unity-text-align: middle-center;
    padding-top: 4px;
    padding-bottom: 8px;
}

.section-list {
    flex-grow: 1;
    padding-left: 12px;
    padding-right: 12px;
    padding-bottom: 16px;
}

/* ─────────────── Section (unlocked) ─────────────── */

.section {
    margin-top: 8px;
    padding-bottom: 16px;
}

.section-name {
    -unity-font-style: bold;
    font-size: 14px;
    color: rgb(43, 43, 43);
    padding-top: 4px;
    padding-bottom: 4px;
}

.section-body {
    flex-direction: row;
}

.icon-col {
    width: 64px;
    align-items: center;
    padding-top: 4px;
}

.icon-frame {
    width: 60px;
    height: 60px;
    border-top-width: 1px;
    border-right-width: 1px;
    border-bottom-width: 1px;
    border-left-width: 1px;
    border-color: rgba(0, 0, 0, 0.35);
    border-top-left-radius: 8px;
    border-top-right-radius: 8px;
    border-bottom-left-radius: 8px;
    border-bottom-right-radius: 8px;
    background-color: rgba(255, 255, 255, 0.05);
    -unity-background-scale-mode: scale-to-fit;
}

.rows-col {
    flex-grow: 1;
    flex-shrink: 1;
    margin-left: 10px;
}

/* ─────────────── Row ─────────────── */

.row {
    margin-bottom: 12px;
}

.row-title {
    -unity-font-style: bold;
    font-size: 13px;
    color: rgb(43, 43, 43);
}

.row-desc {
    font-size: 11px;
    color: rgba(0, 0, 0, 0.65);
    margin-bottom: 6px;
}

.tiles-scroll {
    height: 86px;
}

.tiles-strip {
    flex-direction: row;
}

/* ─────────────── Tile (3 states) ─────────────── */

.tile {
    width: 64px;
    height: 76px;
    border-top-left-radius: 8px;
    border-top-right-radius: 8px;
    border-bottom-left-radius: 8px;
    border-bottom-right-radius: 8px;
    background-color: rgba(230, 213, 178, 0.7);
    align-items: center;
    justify-content: space-between;
    padding-top: 6px;
    padding-bottom: 6px;
    padding-left: 4px;
    padding-right: 4px;
    margin-right: 6px;
    flex-shrink: 0;
    border-top-width: 1px;
    border-right-width: 1px;
    border-bottom-width: 1px;
    border-left-width: 1px;
    border-color: rgba(0, 0, 0, 0);
}

.tile-level {
    -unity-font-style: bold;
    font-size: 11px;
    color: rgb(43, 43, 43);
}

.tile-bonus {
    font-size: 11px;
    -unity-font-style: bold;
    color: rgb(43, 43, 43);
}

.tile-footer {
    font-size: 10px;
    color: rgb(43, 43, 43);
}

.tile--bought {
    opacity: 0.55;
}

.tile--bought .tile-footer {
    color: rgb(45, 111, 45);
    -unity-font-style: bold;
}

.tile--affordable {
    border-color: rgb(43, 43, 43);
    background-color: rgba(230, 213, 178, 1);
}

.tile--cant-afford {
    opacity: 0.4;
}

/* ─────────────── Locked section ─────────────── */

.locked-section {
    margin-top: 8px;
    padding-top: 10px;
    padding-bottom: 10px;
    padding-left: 10px;
    padding-right: 10px;
    border-top-left-radius: 10px;
    border-top-right-radius: 10px;
    border-bottom-left-radius: 10px;
    border-bottom-right-radius: 10px;
    background-color: rgba(0, 0, 0, 0.08);
    flex-direction: row;
    align-items: center;
    opacity: 0.75;
}

.locked-icon {
    width: 44px;
    height: 44px;
}

.locked-meta {
    margin-left: 12px;
}

.locked-name {
    -unity-font-style: bold;
    font-size: 14px;
    color: rgb(43, 43, 43);
}

.locked-hint {
    font-size: 11px;
    color: rgba(0, 0, 0, 0.55);
    -unity-font-style: italic;
}
```

Note: USS does not support `border-style: dashed`. The affordable-tile border is solid here; visual polish (dashed via 9-slice sprite or a stylized graphic) is deferred.

- [ ] **Step 2: Wait for import + verify**

Switch to Unity. Console expected clear of USS parse errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uss"
git commit -m "feat(equipment): add EquipmentPopupUITK USS stylesheet"
```

---

### Task 7: Create EquipmentPopupUITK controller

**Files:**
- Create: `Assets/Scripts/UI/EquipmentPopupUITK.cs`

- [ ] **Step 1: Write the controller**

Create `Assets/Scripts/UI/EquipmentPopupUITK.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EquipmentPopupUITK : MonoBehaviour
{
    public static EquipmentPopupUITK Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private EquipmentRegistry registry;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset sectionTemplate;
    [SerializeField] private VisualTreeAsset lockedSectionTemplate;
    [SerializeField] private VisualTreeAsset rowTemplate;
    [SerializeField] private VisualTreeAsset tileTemplate;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private ScrollView sectionList;

    private bool isOpen;
    private bool eventsSubscribed;
    private bool refreshPending;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        CacheElements();
        WireCallbacks();
        TrySubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        bool any = false;
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradeChanged;
            any = true;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCoinsChanged;
            any = true;
        }
        eventsSubscribed = any;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradeChanged;
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCoinsChanged -= OnCoinsChanged;
        eventsSubscribed = false;
    }

    private void OnUpgradeChanged(string _) => MarkDirty();
    private void OnCoinsChanged(int _) => MarkDirty();

    private void MarkDirty()
    {
        if (!isOpen || refreshPending || root == null) return;
        refreshPending = true;
        root.schedule.Execute(() =>
        {
            refreshPending = false;
            if (isOpen) RefreshAll();
        }).StartingIn(200);
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[EquipmentPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        sectionList = root.Q<ScrollView>("section-list");

        if (popupRoot == null)   Debug.LogWarning("[EquipmentPopupUITK] popup-root not found in UXML");
        if (backdrop == null)    Debug.LogWarning("[EquipmentPopupUITK] backdrop not found in UXML");
        if (sectionList == null) Debug.LogWarning("[EquipmentPopupUITK] section-list not found in UXML");
    }

    private void WireCallbacks()
    {
        if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        RefreshAll();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    public void RefreshAll()
    {
        if (sectionList == null) return;
        sectionList.Clear();

        if (registry == null)
        {
            Debug.LogWarning("[EquipmentPopupUITK] EquipmentRegistry not assigned");
            return;
        }

        foreach (EquipmentData eq in registry.equipment)
        {
            if (eq == null) continue;
            if (eq.IsUnlocked()) SpawnUnlockedSection(eq);
            else SpawnLockedSection(eq);
        }
    }

    private void SpawnUnlockedSection(EquipmentData eq)
    {
        if (sectionTemplate == null)
        {
            Debug.LogWarning("[EquipmentPopupUITK] sectionTemplate not assigned");
            return;
        }

        TemplateContainer section = sectionTemplate.Instantiate();
        sectionList.Add(section);

        Label nameLabel = section.Q<Label>("equipment-name");
        VisualElement iconImage = section.Q<VisualElement>("equipment-icon");
        VisualElement rowsContainer = section.Q<VisualElement>("rows-container");

        if (nameLabel != null) nameLabel.text = eq.displayName;
        if (iconImage != null && eq.iconSprite != null)
            iconImage.style.backgroundImage = new StyleBackground(eq.iconSprite);

        if (eq.uiUpgradeRows == null || rowsContainer == null) return;

        for (int i = 0; i < eq.uiUpgradeRows.Length; i++)
        {
            UpgradeData data = eq.uiUpgradeRows[i];
            if (data == null)
            {
                Debug.LogWarning($"[EquipmentPopupUITK] null upgrade slot at index {i} on '{eq.displayName}'");
                continue;
            }
            SpawnRow(rowsContainer, data);
        }
    }

    private void SpawnRow(VisualElement parent, UpgradeData data)
    {
        if (rowTemplate == null) return;
        TemplateContainer row = rowTemplate.Instantiate();
        parent.Add(row);

        Label title = row.Q<Label>("row-title");
        Label desc  = row.Q<Label>("row-desc");
        VisualElement tilesStrip = row.Q<VisualElement>("tiles-strip");

        if (title != null) title.text = data.displayName;
        if (desc != null)  desc.text  = data.description;

        if (tilesStrip == null) return;

        int max = Mathf.Max(1, data.maxLevel);
        for (int level = 1; level <= max; level++)
            SpawnTile(tilesStrip, data, level);
    }

    private void SpawnTile(VisualElement parent, UpgradeData data, int level)
    {
        if (tileTemplate == null) return;

        TemplateContainer tile = tileTemplate.Instantiate();
        parent.Add(tile);

        VisualElement tileRoot = tile.Q(className: "tile") ?? tile.contentContainer;
        Label lvlLabel   = tile.Q<Label>("tile-level");
        Label bonusLabel = tile.Q<Label>("tile-bonus");
        Label footerLabel = tile.Q<Label>("tile-footer");

        if (lvlLabel != null) lvlLabel.text = $"Level {level}";
        if (bonusLabel != null) bonusLabel.text = data.GetBonusText(level);

        int permLevel = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.GetPermanentLevel(data.upgradeID)
            : 0;
        int cost = data.GetCoinCost(level);

        tileRoot.RemoveFromClassList("tile--bought");
        tileRoot.RemoveFromClassList("tile--affordable");
        tileRoot.RemoveFromClassList("tile--cant-afford");

        if (level <= permLevel)
        {
            tileRoot.AddToClassList("tile--bought");
            if (footerLabel != null) footerLabel.text = "✓";
        }
        else if (level == permLevel + 1 &&
                 CurrencyManager.Instance != null &&
                 CurrencyManager.Instance.CanAffordCoins(cost))
        {
            tileRoot.AddToClassList("tile--affordable");
            if (footerLabel != null) footerLabel.text = FormatCost(cost);
            // capture by value
            UpgradeData capturedData = data;
            int capturedLevel = level;
            int capturedCost = cost;
            tileRoot.RegisterCallback<ClickEvent>(_ => OnTileClicked(capturedData, capturedLevel, capturedCost));
        }
        else
        {
            tileRoot.AddToClassList("tile--cant-afford");
            if (footerLabel != null) footerLabel.text = FormatCost(cost);
        }
    }

    private void OnTileClicked(UpgradeData data, int level, int cost)
    {
        if (UpgradeManager.Instance == null) return;
        int currentLevel = UpgradeManager.Instance.GetPermanentLevel(data.upgradeID);
        if (level != currentLevel + 1) return;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.CanAffordCoins(cost)) return;

        if (UpgradeManager.Instance.PurchasePermanentUpgrade(data.upgradeID, cost))
            RefreshAll();
    }

    private void SpawnLockedSection(EquipmentData eq)
    {
        if (lockedSectionTemplate == null)
        {
            Debug.LogWarning("[EquipmentPopupUITK] lockedSectionTemplate not assigned");
            return;
        }

        TemplateContainer section = lockedSectionTemplate.Instantiate();
        sectionList.Add(section);

        Label nameLabel = section.Q<Label>("equipment-name");
        VisualElement iconImage = section.Q<VisualElement>("equipment-icon");

        if (nameLabel != null) nameLabel.text = eq.displayName;
        if (iconImage != null && eq.iconSprite != null)
            iconImage.style.backgroundImage = new StyleBackground(eq.iconSprite);
    }

    private static string FormatCost(int cost)
    {
        if (cost >= 1_000_000) return $"{cost / 1_000_000f:0.#}M";
        if (cost >= 1_000)     return $"{cost / 1_000f:0.#}k";
        return cost.ToString();
    }
}
```

- [ ] **Step 2: Verify compilation**

Switch to Unity, wait for domain reload, watch Console. Expected: zero compile errors. Warnings are OK if any (e.g. about unused privates).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/UI/EquipmentPopupUITK.cs"
git commit -m "feat(equipment): add EquipmentPopupUITK controller (UI Toolkit popup)"
```

---

### Task 8: Set up the UIDocument GameObject in SampleScene

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via Unity Editor)

- [ ] **Step 1: Create the GameObject**

In Unity, open `SampleScene`. In the Hierarchy, create an empty GameObject at the root and name it `EquipmentPopupUITK`. Position doesn't matter (UI Toolkit uses screen-space).

- [ ] **Step 2: Add UIDocument**

Select the new GameObject. In Inspector: Add Component → UI Document. Fields:
- **Panel Settings**: `RunewoodPanelSettings` (drag from `Assets/Settings/`)
- **Source Asset**: `EquipmentPopupUITK.uxml` (drag from `Assets/UI/EquipmentPopupUITK/`)
- **Sort Order**: `10` (matches QuestPopupUITK / other migrated popups — adjust if you need this above another popup)

- [ ] **Step 3: Add EquipmentPopupUITK component**

Still on the same GameObject: Add Component → EquipmentPopupUITK. Drag into the Inspector slots:
- **Registry** → `Assets/Data/Equipment/EquipmentRegistry.asset`
- **Section Template** → `Assets/UI/EquipmentPopupUITK/EquipmentSectionTemplate.uxml`
- **Locked Section Template** → `Assets/UI/EquipmentPopupUITK/EquipmentLockedSectionTemplate.uxml`
- **Row Template** → `Assets/UI/EquipmentPopupUITK/EquipmentRowTemplate.uxml`
- **Tile Template** → `Assets/UI/EquipmentPopupUITK/EquipmentTileTemplate.uxml`

- [ ] **Step 4: Save the scene**

Ctrl+S.

- [ ] **Step 5: Smoke-test alone**

Enter Play Mode. In the Hierarchy, select the EquipmentPopupUITK GameObject. In the Inspector right-click the component header → invoke method (or call from Console) — actually simplest: from anywhere in Play Mode, open the existing Quest button or any other interaction. The popup shouldn't appear (Open() hasn't been called).

If anything is wrong, the Console will show null-ref warnings for unassigned templates or missing UXML elements. Fix and re-save.

Exit Play Mode.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scenes/SampleScene.unity"
git commit -m "feat(equipment): add EquipmentPopupUITK GameObject to SampleScene"
```

---

### Task 9: Reroute BottomNav Equipment button to the new popup

**Files:**
- Modify: `Assets/Scripts/BottomNav.cs`

- [ ] **Step 1: Update `OnButtonClicked`**

Open `Assets/Scripts/BottomNav.cs`. Find `OnButtonClicked` (currently lines 134-140):

```csharp
    private void OnButtonClicked(DrawerUI.MenuType menuType)
    {
        if (DrawerUI.Instance != null)
        {
            DrawerUI.Instance.OpenMenu(menuType);
        }
    }
```

Replace with:

```csharp
    private void OnButtonClicked(DrawerUI.MenuType menuType)
    {
        // Equipment is now served by the standalone UI Toolkit popup, not the uGUI drawer.
        if (menuType == DrawerUI.MenuType.Equipment)
        {
            if (DrawerUI.Instance != null && DrawerUI.Instance.IsAnyMenuOpen())
                DrawerUI.Instance.CloseDrawer();

            if (EquipmentPopupUITK.Instance == null) return;
            if (EquipmentPopupUITK.Instance.IsOpen) EquipmentPopupUITK.Instance.Close();
            else EquipmentPopupUITK.Instance.Open();
            return;
        }

        if (DrawerUI.Instance != null)
            DrawerUI.Instance.OpenMenu(menuType);
    }
```

- [ ] **Step 2: Update visual selection state for Equipment**

The bottom-nav button's "selected" highlight is driven by `DrawerUI.OnMenuOpened/OnMenuClosed`. Since Equipment no longer routes through DrawerUI, the highlight won't toggle when the new popup opens.

For now, leave the highlight logic alone — Equipment button will simply not "light up" when its popup is open. This is acceptable for the first iteration; we can add a callback from `EquipmentPopupUITK` later if needed.

(Skip if you'd rather wire the highlight now — it's not blocking.)

- [ ] **Step 3: Verify compilation**

Switch to Unity, wait for compile. Console expected clear.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/BottomNav.cs"
git commit -m "feat(equipment): reroute BottomNav Equipment button to EquipmentPopupUITK"
```

---

### Task 10: Verify in Play Mode

**Files:** none

- [ ] **Step 1: Open Play Mode and tap the Equipment button**

Enter Play Mode in Unity. Tap the Equipment button on the bottom nav.

Expected:
- Drawer slides up from the bottom with the backdrop dimming.
- Three sections render: Scarecrow (3 rows), Sprinkler (4 rows), Fence (3 rows) — or whichever subset is currently unlocked. Locked equipment shows the compact "Locked — unlock at Market" row.
- Each row shows title + description + 5 tiles.
- Tiles already purchased show ✓ at 55% opacity.
- The next purchasable level shows a solid border (the "affordable" state) — if the player has enough coins.
- Tiles beyond the affordable one are dim.

- [ ] **Step 2: Tap a tile**

Tap an affordable tile. Expected:
- A coin deduction occurs (verify via the on-screen coin counter).
- The tapped tile becomes the new "bought" state on next refresh (≤200ms).
- The next tile may flip to "affordable" if coins remain.

- [ ] **Step 3: Close via backdrop**

Tap above the drawer (on the dimmed backdrop). Expected: drawer slides down, backdrop fades out, popup hides.

- [ ] **Step 4: Close via the Equipment button**

Open again, then tap the Equipment bottom-nav button. Expected: drawer slides down (toggle behavior).

- [ ] **Step 5: Verify other drawer panels still work**

Tap Market, Farm, Helpers, Settings — they should still open the old uGUI DrawerUI as before. Tap Equipment after closing one of those — the new popup opens, old drawer closes.

- [ ] **Step 6: If issues, iterate**

If layout is broken, tweak `EquipmentPopupUITK.uss` and re-test. If sections are missing, check that `EquipmentRegistry.equipment` is populated and that each `EquipmentData.uiUpgradeRows` is configured (Task 4). If null-ref warnings appear in Console, the Inspector slots in Task 8 may be missing.

Iterate until the popup looks and behaves correctly. Commit any tweaks:

```bash
git add "Assets/UI/EquipmentPopupUITK/EquipmentPopupUITK.uss"
git commit -m "fix(equipment): tune drawer styles after first play-through"
```

---

### Task 11: Cleanup (only after Task 10 is approved)

**Files:**
- Delete: `Assets/Scripts/EquipmentMenuPanel.cs` + `.meta`
- Delete: `Assets/Scripts/Editor/EquipmentPanelBuilder.cs` + `.meta`
- Modify: `Assets/Scripts/DrawerUI.cs`
- Modify: `Assets/Scenes/SampleScene.unity`

Do not run this task until the user has confirmed the new popup is working and they want the old code removed.

- [ ] **Step 1: Remove old GameObject hierarchy from the scene**

In Unity, open SampleScene. In the Hierarchy, find the old `EquipmentPanel` GameObject under the DrawerUI's panel container (likely under `MainCanvas/Drawer/...`). Delete it.

Verify `DrawerUI` (component) `Equipment Panel` field is now empty.

Save scene.

- [ ] **Step 2: Delete EquipmentMenuPanel.cs**

```bash
git rm "Assets/Scripts/EquipmentMenuPanel.cs" "Assets/Scripts/EquipmentMenuPanel.cs.meta"
```

- [ ] **Step 3: Delete the editor tool**

```bash
git rm "Assets/Scripts/Editor/EquipmentPanelBuilder.cs" "Assets/Scripts/Editor/EquipmentPanelBuilder.cs.meta"
```

- [ ] **Step 4: Strip Equipment branches from DrawerUI**

Open `Assets/Scripts/DrawerUI.cs`. Remove these locations:

In the `[SerializeField] private GameObject equipmentPanel;` field declaration (line ~25): delete the line.

In `ShowPanel`:
```csharp
            case MenuType.Equipment:
                if (equipmentPanel != null) equipmentPanel.SetActive(true);
                break;
```
Delete those four lines.

In `HideAllPanels`:
```csharp
        if (equipmentPanel != null) equipmentPanel.SetActive(false);
```
Delete that line.

In `UpdateTitle`:
```csharp
            case MenuType.Equipment:
                titleText.text = "🪓 Equipment";
                break;
```
Delete those four lines.

Keep `MenuType.Equipment` in the enum — `BottomNav` still references it as a switch discriminator.

- [ ] **Step 5: Verify compilation + Play Mode smoke**

Switch to Unity, wait for compile. Console expected clear. Enter Play Mode: tap Equipment → new popup opens. Tap other nav buttons → old DrawerUI panels (Market/Farm/Helpers/Settings) open. Exit Play Mode.

- [ ] **Step 6: Commit cleanup**

```bash
git add -A
git commit -m "refactor(equipment): remove old EquipmentMenuPanel and uGUI Equipment hierarchy"
```

---

## Notes on testing

- This project does not currently run play-mode UI tests. Validation is manual via the Unity Editor.
- If a step fails (compile error, missing reference, layout glitch), fix the root cause; do not paper over with hidden retries.
- Domain reloads take a few seconds — wait for them before testing.

## Risks recapped

- **Dashed border for affordable tiles**: USS doesn't support dashed borders. Plan ships with a solid border. To get a dashed look, swap in a 9-slice sprite background later.
- **TemplateContainer click target**: `RegisterCallback<ClickEvent>` is attached to `tileRoot` (the element with class `tile`), not the outer `TemplateContainer`, so clicks register correctly even with the wrapper element.
- **Race with managers at Awake**: `TrySubscribeEvents` is called in both `OnEnable` and `Open()` — same pattern as `QuestPopupUITK.cs:57-62`, handles the case where `UpgradeManager.Instance`/`CurrencyManager.Instance` haven't run their `Awake` yet on scene load.
