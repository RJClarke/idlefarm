# QuestPopup UI Toolkit Rebuild — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a UI Toolkit version of `QuestPopup` (named `QuestPopupUITK`) that opens via Shift-click on the existing quest button, reads the same `QuestManager` data, and visually matches the reference in `docs/superpowers/specs/assets/2026-05-11-questpopup-reference.png`. The original `QuestPopup` stays intact as the default.

**Architecture:** New `UIDocument`-driven popup parallel to the existing uGUI popup. Single C# controller (`QuestPopupUITK.cs`) queries elements by name, subscribes to `QuestManager` events, clones UXML templates per quest/chip. All visuals in USS. Existing `QuestPopup.cs` gets a one-line debug toggle; `QuestRow.cs` loses its emoji prefix.

**Tech Stack:** Unity 6.3 (`6000.3.9f1`), UI Toolkit (UXML/USS), C#, gladekit-unity MCP for scene wiring.

**Reference spec:** `docs/superpowers/specs/2026-05-11-questpopup-uitoolkit-design.md`

---

## Verification model

Unity makes traditional unit tests for UI awkward. Each task uses the closest practical gates:

1. **Compile check** — call `mcp__UnityMCP__read_console` after any `.cs` edit and confirm zero compile errors.
2. **Runtime null-safety** — log a warning if `root.Q<>(name)` returns null; we'll see those in `read_console` after entering Play mode.
3. **Visual check** — user opens the popup in Play mode and either confirms or screenshots into `Assets/Screenshots/` for an `Edit`-driven USS iteration loop.
4. **Functional check** — user clicks Claim / milestone in Play mode; confirms `QuestManager` calls fire.

Each task explicitly names which gates apply.

---

## File map

**Create:**
- `Assets/UI/QuestPopupUITK/QuestPopupUITK.uxml`
- `Assets/UI/QuestPopupUITK/QuestPopupUITK.uss`
- `Assets/UI/QuestPopupUITK/QuestRowTemplate.uxml`
- `Assets/UI/QuestPopupUITK/MilestoneChipTemplate.uxml`
- `Assets/Settings/RunewoodPanelSettings.asset`
- `Assets/Scripts/UI/QuestPopupUITK.cs`

**Modify:**
- `Assets/Scripts/QuestRow.cs` — remove `ObjectiveEmoji` table + the `emoji + " " + ...` concatenation
- `Assets/Scripts/QuestPopup.cs` — add Shift-click branch in `questButton` handler

**Scene mutations (via MCP):**
- Add GameObject `QuestPopupUITK` under the existing UI canvas root, with `UIDocument` component + the `QuestPopupUITK` script

---

### Task 1: Strip emojis from existing QuestRow.cs

Lowest-risk task, immediate visible win. Removes leading emoji glyph from quest names in the *current* uGUI popup so it matches the new design's no-emoji rule.

**Files:**
- Modify: `Assets/Scripts/QuestRow.cs:40-49, 154`

- [ ] **Step 1: Remove the `ObjectiveEmoji` table**

Open `Assets/Scripts/QuestRow.cs`. Delete lines 40-49 (the `ObjectiveEmoji` static array). Keep everything else in the file intact.

- [ ] **Step 2: Remove the emoji prefix in `Bind`**

Replace this line (currently around line 154-155):

```csharp
string emoji = GetObjectiveEmoji(data.objectiveType);
if (questNameText) { questNameText.text = emoji + " " + data.displayName; questNameText.fontSize = 18; questNameText.fontWeight = FontWeight.Bold; }
```

With:

```csharp
if (questNameText) { questNameText.text = data.displayName; questNameText.fontSize = 18; questNameText.fontWeight = FontWeight.Bold; }
```

- [ ] **Step 3: Remove the now-unused `GetObjectiveEmoji` method**

Delete the entire method:

```csharp
private string GetObjectiveEmoji(QuestObjectiveType type)
{
    int idx = (int)type;
    if (idx >= 0 && idx < ObjectiveEmoji.Length)
        return ObjectiveEmoji[idx];
    return "⭐";
}
```

- [ ] **Step 4: Verify compile clean**

Run `mcp__UnityMCP__read_console` and confirm no compile errors mention `QuestRow.cs` or `ObjectiveEmoji`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/QuestRow.cs
git commit -m "refactor: strip emoji prefix from quest names in QuestRow"
```

---

### Task 2: Create folder structure and PanelSettings asset

Sets up the project locations and the shared `PanelSettings` asset that the `UIDocument` will reference.

**Files:**
- Create: `Assets/UI/QuestPopupUITK/` (folder)
- Create: `Assets/Settings/` (folder, if missing)
- Create: `Assets/Settings/RunewoodPanelSettings.asset`
- Create: `Assets/Scripts/UI/` (folder, if missing)

- [ ] **Step 1: Create the folders via MCP**

```
mcp__gladekit-unity__create_folder(path="Assets/UI/QuestPopupUITK")
mcp__gladekit-unity__create_folder(path="Assets/Settings")  # skip if exists
mcp__gladekit-unity__create_folder(path="Assets/Scripts/UI")  # skip if exists
```

- [ ] **Step 2: Create the PanelSettings asset via Unity menu**

Use `mcp__UnityMCP__execute_menu_item` with menu path `Assets/Create/UI Toolkit/Panel Settings Asset`. The new asset will be created in the active Project window folder.

If that menu approach is unavailable, fallback: use `mcp__UnityMCP__manage_asset` to create a ScriptableObject of type `UnityEngine.UIElements.PanelSettings` at path `Assets/Settings/RunewoodPanelSettings.asset`.

- [ ] **Step 3: Move and rename the asset**

If the asset was created elsewhere, use `mcp__gladekit-unity__move_asset` to move it to `Assets/Settings/RunewoodPanelSettings.asset`.

- [ ] **Step 4: Configure the PanelSettings**

Set these properties on the asset (via `mcp__UnityMCP__manage_asset` modify or `set_component_property`):

- `sortingOrder`: `1000` (must render above existing Canvas UI)
- `scaleMode`: `ScaleWithScreenSize`
- `referenceResolution`: `(1080, 1920)` — matches project portrait resolution
- `match`: `0.5` (balance width vs height scaling)
- `clearColor`: `false`
- `clearDepthStencil`: `true`

- [ ] **Step 5: Commit**

```bash
git add Assets/UI Assets/Settings Assets/Scripts/UI
git commit -m "feat: scaffold folders and PanelSettings for UI Toolkit quest popup"
```

---

### Task 3: Create UXML files (popup + templates)

UXML structure for the popup and two templates. No styling yet — styling lives entirely in USS.

**Files:**
- Create: `Assets/UI/QuestPopupUITK/QuestPopupUITK.uxml`
- Create: `Assets/UI/QuestPopupUITK/QuestRowTemplate.uxml`
- Create: `Assets/UI/QuestPopupUITK/MilestoneChipTemplate.uxml`

- [ ] **Step 1: Write `QuestPopupUITK.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <ui:VisualElement name="root" class="popup-root" picking-mode="Position">
        <ui:VisualElement name="backdrop" class="backdrop" picking-mode="Position" />
        <ui:VisualElement name="popup-container" class="popup-container" picking-mode="Position">

            <ui:VisualElement name="header" class="header">
                <ui:Label text="Daily Quests" name="title" class="title" />
                <ui:Button name="close-button" text="✕" class="close-button" />
            </ui:VisualElement>

            <ui:VisualElement name="weekly-strip" class="weekly-strip">
                <ui:VisualElement name="weekly-header-row" class="weekly-header-row">
                    <ui:Label text="WEEKLY TRACK" class="weekly-label" />
                    <ui:Label name="weekly-count" text="0 / 40 quests · resets Sun" class="weekly-count" />
                </ui:VisualElement>
                <ui:VisualElement name="weekly-progress-track" class="weekly-progress-track">
                    <ui:VisualElement name="weekly-progress-fill" class="weekly-progress-fill" />
                </ui:VisualElement>
                <ui:VisualElement name="milestone-strip" class="milestone-strip" />
            </ui:VisualElement>

            <ui:ScrollView name="quest-list" class="quest-list" mode="Vertical" />

            <ui:Label name="footer-text" text="Next drop in 0m · 0/10 slots" class="footer" />

        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

Notes:
- `picking-mode="Position"` on backdrop so it captures clicks (for close-on-backdrop later).
- Custom progress-track-and-fill `VisualElement` pair instead of `<ui:ProgressBar>` because `ProgressBar` has built-in chrome that's hard to fully restyle.

- [ ] **Step 2: Write `QuestRowTemplate.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="quest-row quest-row--in-progress" picking-mode="Position">
        <ui:VisualElement name="new-badge" class="new-badge" style="display: none;">
            <ui:Label text="NEW" />
        </ui:VisualElement>
        <ui:VisualElement class="quest-row__body">
            <ui:VisualElement class="quest-row__left">
                <ui:Label name="quest-name" class="quest-name" />
                <ui:Label name="quest-description" class="quest-description" />
                <ui:Label name="complete-label" text="✓ Complete!" class="complete-label" style="display: none;" />
            </ui:VisualElement>
            <ui:VisualElement class="quest-row__right">
                <ui:Label name="progress-text" class="progress-text" />
                <ui:Label name="reward-text" class="reward-text" />
                <ui:Button name="claim-button" class="claim-button" style="display: none;">
                    <ui:Label name="claim-button-text" text="Claim" class="claim-button__label" />
                    <ui:Label name="claim-button-reward" class="claim-button__reward" />
                </ui:Button>
            </ui:VisualElement>
        </ui:VisualElement>
        <ui:VisualElement class="quest-row__progress-track">
            <ui:VisualElement name="quest-progress-fill" class="quest-row__progress-fill" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: Write `MilestoneChipTemplate.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:Button class="chip chip--locked" picking-mode="Position">
        <ui:Label name="state-icon" class="chip__icon" />
        <ui:Label name="tier-label" class="chip__tier" />
        <ui:Label name="gem-label" class="chip__gem" />
    </ui:Button>
</ui:UXML>
```

- [ ] **Step 4: Refresh Unity asset DB**

Call `mcp__UnityMCP__refresh_unity` so Unity picks up the new UXML files.

- [ ] **Step 5: Commit**

```bash
git add "Assets/UI/QuestPopupUITK/QuestPopupUITK.uxml" "Assets/UI/QuestPopupUITK/QuestRowTemplate.uxml" "Assets/UI/QuestPopupUITK/MilestoneChipTemplate.uxml"
git commit -m "feat: add UXML structure for QuestPopupUITK + row and chip templates"
```

---

### Task 4: Create initial USS with layout + base styling

Get the popup laid out and visually approximating the reference. Per-state styling (claimed/next/locked, in-progress/completed/new) comes in later tasks.

**Files:**
- Create: `Assets/UI/QuestPopupUITK/QuestPopupUITK.uss`

- [ ] **Step 1: Write the USS file**

```css
/* ─────────────────────────────────────────────────────────────
   Root / backdrop / container
   ───────────────────────────────────────────────────────────── */

.popup-root {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    align-items: center;
    justify-content: center;
    opacity: 0;
    scale: 0.8 0.8;
    transition: opacity 0.25s ease-out, scale 0.25s ease-out;
    display: none;
}

.popup-root.open {
    opacity: 1;
    scale: 1 1;
}

.backdrop {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.55);
}

.popup-container {
    width: 660px;
    max-height: 80%;
    background-color: rgb(33, 25, 12);
    border-color: rgb(115, 89, 41);
    border-width: 3px;
    border-radius: 12px;
    padding: 0;
    overflow: hidden;
}

/* ─────────────────────────────────────────────────────────────
   Header
   ───────────────────────────────────────────────────────────── */

.header {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    padding: 14px 18px;
    border-bottom-width: 1px;
    border-bottom-color: rgb(58, 45, 20);
    background-color: rgb(26, 20, 8);
}

.title {
    color: rgb(255, 201, 60);
    font-size: 22px;
    -unity-font-style: bold;
}

.close-button {
    width: 36px;
    height: 36px;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    color: rgb(180, 175, 165);
    font-size: 18px;
    -unity-font-style: bold;
}

.close-button:hover {
    color: rgb(255, 255, 255);
}

/* ─────────────────────────────────────────────────────────────
   Weekly track strip
   ───────────────────────────────────────────────────────────── */

.weekly-strip {
    padding: 14px 18px 12px 18px;
    border-bottom-width: 1px;
    border-bottom-color: rgb(58, 45, 20);
}

.weekly-header-row {
    flex-direction: row;
    justify-content: space-between;
    margin-bottom: 8px;
}

.weekly-label {
    color: rgb(181, 167, 138);
    font-size: 14px;
    -unity-font-style: bold;
    letter-spacing: 1px;
}

.weekly-count {
    color: rgb(181, 167, 138);
    font-size: 13px;
}

.weekly-progress-track {
    height: 6px;
    background-color: rgb(42, 32, 16);
    border-radius: 3px;
    margin-bottom: 10px;
    overflow: hidden;
}

.weekly-progress-fill {
    height: 100%;
    width: 0;
    background-color: rgb(255, 201, 60);
    border-radius: 3px;
}

.milestone-strip {
    flex-direction: row;
    justify-content: space-between;
}

/* ─────────────────────────────────────────────────────────────
   Milestone chip (base; per-state in Task 7)
   ───────────────────────────────────────────────────────────── */

.chip {
    width: 70px;
    height: 90px;
    margin: 0 2px;
    align-items: center;
    justify-content: center;
    background-color: rgb(26, 20, 8);
    border-radius: 6px;
    border-width: 2px;
    border-color: rgb(46, 46, 46);
    padding: 4px;
}

.chip__icon {
    font-size: 18px;
    margin-bottom: 2px;
    -unity-text-align: middle-center;
}

.chip__tier {
    font-size: 14px;
    -unity-font-style: bold;
    -unity-text-align: middle-center;
    margin-bottom: 2px;
}

.chip__gem {
    font-size: 11px;
    -unity-text-align: middle-center;
}

/* ─────────────────────────────────────────────────────────────
   Quest list
   ───────────────────────────────────────────────────────────── */

.quest-list {
    flex-grow: 1;
    padding: 8px 12px;
}

.quest-list > .unity-scroll-view__content-container {
    padding-bottom: 4px;
}

/* ─────────────────────────────────────────────────────────────
   Quest row (base; per-state in Task 8)
   ───────────────────────────────────────────────────────────── */

.quest-row {
    margin: 4px 0;
    background-color: rgb(46, 36, 16);
    border-color: rgb(89, 69, 18);
    border-width: 1px;
    border-radius: 8px;
    padding: 10px 14px 12px 14px;
    overflow: hidden;
    min-height: 84px;
}

.quest-row__body {
    flex-direction: row;
    justify-content: space-between;
}

.quest-row__left {
    flex-grow: 1;
    flex-shrink: 1;
    padding-right: 12px;
}

.quest-row__right {
    align-items: flex-end;
    justify-content: flex-start;
    min-width: 110px;
}

.quest-name {
    color: rgb(255, 255, 255);
    font-size: 18px;
    -unity-font-style: bold;
}

.quest-description {
    color: rgb(180, 180, 180);
    font-size: 13px;
    margin-top: 2px;
}

.complete-label {
    color: rgb(139, 195, 74);
    font-size: 14px;
    -unity-font-style: bold;
    margin-top: 2px;
}

.progress-text {
    color: rgb(180, 180, 180);
    font-size: 14px;
}

.reward-text {
    color: rgb(255, 178, 64);
    font-size: 16px;
    -unity-font-style: bold;
    margin-top: 4px;
}

.new-badge {
    position: absolute;
    top: 8px;
    left: 12px;
}

.new-badge > Label {
    color: rgb(185, 92, 224);
    font-size: 12px;
    -unity-font-style: bold;
    letter-spacing: 1px;
}

.claim-button {
    width: 90px;
    height: 56px;
    background-color: rgb(35, 139, 35);
    border-width: 0;
    border-radius: 8px;
    align-items: center;
    justify-content: center;
}

.claim-button:hover {
    background-color: rgb(50, 160, 50);
}

.claim-button__label {
    color: rgb(255, 255, 255);
    font-size: 15px;
    -unity-font-style: bold;
}

.claim-button__reward {
    color: rgb(255, 220, 100);
    font-size: 13px;
    -unity-font-style: bold;
}

.quest-row__progress-track {
    height: 4px;
    margin-top: 8px;
    background-color: rgb(20, 16, 8);
    border-radius: 2px;
    overflow: hidden;
}

.quest-row__progress-fill {
    height: 100%;
    width: 0;
    background-color: rgb(139, 195, 74);
    border-radius: 2px;
}

/* ─────────────────────────────────────────────────────────────
   Footer
   ───────────────────────────────────────────────────────────── */

.footer {
    color: rgb(150, 150, 150);
    font-size: 13px;
    -unity-text-align: middle-center;
    padding: 10px 0 14px 0;
    border-top-width: 1px;
    border-top-color: rgb(58, 45, 20);
}
```

- [ ] **Step 2: Refresh Unity**

Call `mcp__UnityMCP__refresh_unity`.

- [ ] **Step 3: Commit**

```bash
git add "Assets/UI/QuestPopupUITK/QuestPopupUITK.uss"
git commit -m "feat: add base USS styling for QuestPopupUITK"
```

---

### Task 5: Create `QuestPopupUITK.cs` controller

The singleton controller. Caches refs, subscribes to `QuestManager` events, handles open/close, builds rows and chips, wires claim handlers.

**Files:**
- Create: `Assets/Scripts/UI/QuestPopupUITK.cs`

- [ ] **Step 1: Write the controller file**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class QuestPopupUITK : MonoBehaviour
{
    public static QuestPopupUITK Instance { get; private set; }

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset questRowTemplate;
    [SerializeField] private VisualTreeAsset milestoneChipTemplate;

    // Cached element references
    private UIDocument document;
    private VisualElement root;
    private VisualElement popupContainer;
    private VisualElement backdrop;
    private Button closeButton;
    private Label weeklyCountLabel;
    private VisualElement weeklyProgressFill;
    private VisualElement milestoneStrip;
    private ScrollView questList;
    private Label footerText;

    private bool isOpen;
    private readonly List<VisualElement> spawnedRows = new List<VisualElement>();
    private readonly List<VisualElement> spawnedChips = new List<VisualElement>();

    private static readonly int[] MilestoneThresholds = { 5, 10, 15, 20, 25, 30, 35, 40 };
    private static readonly int[] MilestoneGems       = { 1, 1, 2, 2, 2, 2, 2, 10 };

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
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += OnQuestStateChanged;
            QuestManager.Instance.OnQuestsDropped  += OnQuestStateChanged;
        }
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= OnQuestStateChanged;
            QuestManager.Instance.OnQuestsDropped  -= OnQuestStateChanged;
        }
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[QuestPopupUITK] rootVisualElement is null"); return; }

        popupContainer    = root.Q<VisualElement>("popup-container");
        backdrop          = root.Q<VisualElement>("backdrop");
        closeButton       = root.Q<Button>("close-button");
        weeklyCountLabel  = root.Q<Label>("weekly-count");
        weeklyProgressFill = root.Q<VisualElement>("weekly-progress-fill");
        milestoneStrip    = root.Q<VisualElement>("milestone-strip");
        questList         = root.Q<ScrollView>("quest-list");
        footerText        = root.Q<Label>("footer-text");

        WarnIfNull(popupContainer, "popup-container");
        WarnIfNull(backdrop, "backdrop");
        WarnIfNull(closeButton, "close-button");
        WarnIfNull(weeklyCountLabel, "weekly-count");
        WarnIfNull(weeklyProgressFill, "weekly-progress-fill");
        WarnIfNull(milestoneStrip, "milestone-strip");
        WarnIfNull(questList, "quest-list");
        WarnIfNull(footerText, "footer-text");
    }

    private static void WarnIfNull(object obj, string name)
    {
        if (obj == null) Debug.LogWarning($"[QuestPopupUITK] element '{name}' not found in UXML");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.clicked += Close;
        if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        if (root != null)
        {
            root.style.display = DisplayStyle.Flex;
            // Defer adding 'open' class one frame so the transition triggers
            root.schedule.Execute(() => root.AddToClassList("open")).StartingIn(0);
        }
        RefreshAll();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (root != null)
        {
            root.RemoveFromClassList("open");
            // After the transition (matches USS 0.25s), hide entirely
            root.schedule.Execute(() => { if (!isOpen) root.style.display = DisplayStyle.None; }).StartingIn(260);
        }
    }

    private void OnQuestStateChanged()
    {
        if (isOpen) RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshMilestoneStrip();
        RefreshList();
        RefreshFooter();
    }

    public void RefreshMilestoneStrip()
    {
        if (QuestManager.Instance == null || milestoneStrip == null) return;

        int completed = QuestManager.Instance.QuestsCompletedThisWeek;
        bool[] claimed = QuestManager.Instance.WeeklyMilestonesClaimed;

        if (weeklyCountLabel != null)
            weeklyCountLabel.text = $"{completed} / 40 quests · resets Sun";

        if (weeklyProgressFill != null)
            weeklyProgressFill.style.width = new Length(Mathf.Clamp01(completed / 40f) * 100f, LengthUnit.Percent);

        // Determine next-unclaimed tier
        int nextTier = -1;
        for (int i = 0; i < 8; i++)
        {
            if (!claimed[i]) { nextTier = i; break; }
        }

        // Rebuild chips
        foreach (VisualElement old in spawnedChips) old.RemoveFromHierarchy();
        spawnedChips.Clear();

        if (milestoneChipTemplate == null)
        {
            Debug.LogWarning("[QuestPopupUITK] milestoneChipTemplate not assigned");
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            TemplateContainer chip = milestoneChipTemplate.Instantiate();
            milestoneStrip.Add(chip);
            VisualElement chipRoot = chip.Q(className: "chip") ?? chip.contentContainer;

            int tier = i;
            int threshold = MilestoneThresholds[i];
            int gemReward = MilestoneGems[i];
            bool isClaimed = claimed[i];
            bool isNext = i == nextTier;
            bool isFinal = i == 7;

            Label iconLabel = chip.Q<Label>("state-icon");
            Label tierLabel = chip.Q<Label>("tier-label");
            Label gemLabel = chip.Q<Label>("gem-label");

            if (tierLabel != null) tierLabel.text = threshold.ToString();
            if (gemLabel != null) gemLabel.text = "◆" + gemReward;

            chipRoot.RemoveFromClassList("chip--claimed");
            chipRoot.RemoveFromClassList("chip--next");
            chipRoot.RemoveFromClassList("chip--locked");
            chipRoot.RemoveFromClassList("chip--final");

            if (isClaimed)
            {
                chipRoot.AddToClassList("chip--claimed");
                if (iconLabel != null) iconLabel.text = "✓";
            }
            else if (isNext)
            {
                chipRoot.AddToClassList("chip--next");
                if (iconLabel != null) iconLabel.text = "→";
            }
            else
            {
                chipRoot.AddToClassList("chip--locked");
                if (isFinal) chipRoot.AddToClassList("chip--final");
                if (iconLabel != null) iconLabel.text = isFinal ? "★" : "○";
            }

            Button chipButton = chipRoot as Button;
            if (chipButton != null)
            {
                chipButton.clicked -= () => OnChipClicked(tier);
                if (isNext)
                {
                    chipButton.SetEnabled(true);
                    chipButton.clicked += () => OnChipClicked(tier);
                }
                else
                {
                    chipButton.SetEnabled(false);
                }
            }

            spawnedChips.Add(chip);
        }
    }

    public void RefreshList()
    {
        if (QuestManager.Instance == null || questList == null) return;

        foreach (VisualElement old in spawnedRows) old.RemoveFromHierarchy();
        spawnedRows.Clear();

        if (questRowTemplate == null)
        {
            Debug.LogWarning("[QuestPopupUITK] questRowTemplate not assigned");
            return;
        }

        List<ActiveQuest> quests = QuestManager.Instance.GetActiveQuests();
        quests.Sort((a, b) =>
        {
            int scoreA = a.isCompleted ? 0 : (a.progress == 0 ? 2 : 1);
            int scoreB = b.isCompleted ? 0 : (b.progress == 0 ? 2 : 1);
            return scoreA.CompareTo(scoreB);
        });

        foreach (ActiveQuest quest in quests)
        {
            QuestData data = QuestManager.Instance.GetQuestData(quest.questID);
            if (data == null) continue;
            TemplateContainer row = questRowTemplate.Instantiate();
            BindRow(row, quest, data);
            questList.Add(row);
            spawnedRows.Add(row);
        }
    }

    private void BindRow(TemplateContainer row, ActiveQuest quest, QuestData data)
    {
        VisualElement rowRoot = row.Q(className: "quest-row") ?? row.contentContainer;

        Label nameLabel       = row.Q<Label>("quest-name");
        Label descLabel       = row.Q<Label>("quest-description");
        Label completeLabel   = row.Q<Label>("complete-label");
        Label progressLabel   = row.Q<Label>("progress-text");
        Label rewardLabel     = row.Q<Label>("reward-text");
        Button claimButton    = row.Q<Button>("claim-button");
        Label claimRewardLbl  = row.Q<Label>("claim-button-reward");
        VisualElement newBadge = row.Q<VisualElement>("new-badge");
        VisualElement progressFill = row.Q<VisualElement>("quest-progress-fill");

        if (nameLabel != null) nameLabel.text = data.displayName;
        if (descLabel != null) descLabel.text = data.description;
        if (rewardLabel != null) rewardLabel.text = data.coinReward.ToString();

        bool isComplete = quest.isCompleted;
        bool isNew = quest.progress == 0 && !quest.isCompleted;

        rowRoot.RemoveFromClassList("quest-row--in-progress");
        rowRoot.RemoveFromClassList("quest-row--completed");
        rowRoot.RemoveFromClassList("quest-row--new");

        float fill = data.targetCount > 0 ? Mathf.Clamp01((float)quest.progress / data.targetCount) : 0f;
        if (progressFill != null) progressFill.style.width = new Length(fill * 100f, LengthUnit.Percent);

        string objectiveTypeClass = "quest-row--obj-" + data.objectiveType.ToString().ToLower();
        ClearObjectiveClasses(rowRoot);
        rowRoot.AddToClassList(objectiveTypeClass);

        if (isComplete)
        {
            rowRoot.AddToClassList("quest-row--completed");
            if (completeLabel != null) completeLabel.style.display = DisplayStyle.Flex;
            if (descLabel != null)     descLabel.style.display     = DisplayStyle.None;
            if (progressLabel != null) progressLabel.style.display = DisplayStyle.None;
            if (rewardLabel != null)   rewardLabel.style.display   = DisplayStyle.None;
            if (newBadge != null)      newBadge.style.display      = DisplayStyle.None;
            if (claimButton != null)
            {
                claimButton.style.display = DisplayStyle.Flex;
                if (claimRewardLbl != null) claimRewardLbl.text = data.coinReward.ToString();
                string capturedID = quest.questID;
                claimButton.clicked += () => OnClaimClicked(capturedID);
            }
        }
        else if (isNew)
        {
            rowRoot.AddToClassList("quest-row--new");
            if (completeLabel != null) completeLabel.style.display = DisplayStyle.None;
            if (descLabel != null)     descLabel.style.display     = DisplayStyle.Flex;
            if (progressLabel != null) { progressLabel.style.display = DisplayStyle.Flex; progressLabel.text = "0 / " + data.targetCount; }
            if (rewardLabel != null)   rewardLabel.style.display   = DisplayStyle.Flex;
            if (newBadge != null)      newBadge.style.display      = DisplayStyle.Flex;
            if (claimButton != null)   claimButton.style.display   = DisplayStyle.None;
        }
        else
        {
            rowRoot.AddToClassList("quest-row--in-progress");
            if (completeLabel != null) completeLabel.style.display = DisplayStyle.None;
            if (descLabel != null)     descLabel.style.display     = DisplayStyle.Flex;
            if (progressLabel != null) { progressLabel.style.display = DisplayStyle.Flex; progressLabel.text = quest.progress + " / " + data.targetCount; }
            if (rewardLabel != null)   rewardLabel.style.display   = DisplayStyle.Flex;
            if (newBadge != null)      newBadge.style.display      = DisplayStyle.None;
            if (claimButton != null)   claimButton.style.display   = DisplayStyle.None;
        }
    }

    private static void ClearObjectiveClasses(VisualElement rowRoot)
    {
        foreach (QuestObjectiveType type in Enum.GetValues(typeof(QuestObjectiveType)))
            rowRoot.RemoveFromClassList("quest-row--obj-" + type.ToString().ToLower());
    }

    private void RefreshFooter()
    {
        if (QuestManager.Instance == null || footerText == null) return;
        DateTime nextDrop = QuestManager.Instance.GetNextDropTimeUtc();
        TimeSpan remaining = nextDrop - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        int hours = (int)remaining.TotalHours;
        int minutes = remaining.Minutes;
        string timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        footerText.text = $"Next drop in {timeStr}  ·  {QuestManager.Instance.ActiveQuestCount}/10 slots";
    }

    private void OnClaimClicked(string questID)
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimQuest(questID))
            RefreshList();
    }

    private void OnChipClicked(int tier)
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimMilestone(tier))
            RefreshMilestoneStrip();
    }
}
```

- [ ] **Step 2: Refresh Unity and verify compile**

```
mcp__UnityMCP__refresh_unity()
mcp__UnityMCP__read_console()
```

Expected: zero errors. The script compiles standalone — it doesn't yet have scene wiring, so it won't run, just compile.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/UI/QuestPopupUITK.cs"
git commit -m "feat: add QuestPopupUITK controller (UI Toolkit) script"
```

---

### Task 6: Wire up scene — add `QuestPopupUITK` GameObject

Scene mutation via MCP. Adds the GameObject, attaches `UIDocument` + script, references the UXML and templates.

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via MCP, not direct edit)

- [ ] **Step 1: Discover the existing UI canvas root**

Call `mcp__gladekit-unity__get_scene_hierarchy()` and identify the parent GameObject the existing `QuestPopup` sits under (typically a Canvas named "UI" or similar). Record its hierarchy path.

- [ ] **Step 2: Create the `QuestPopupUITK` GameObject**

```
mcp__gladekit-unity__create_game_object(
    name="QuestPopupUITK",
    parent_path="<UI root path discovered in step 1>"
)
```

- [ ] **Step 3: Add `UIDocument` component**

```
mcp__gladekit-unity__add_component(
    gameobject_path="<parent>/QuestPopupUITK",
    component_type="UnityEngine.UIElements.UIDocument"
)
```

- [ ] **Step 4: Add `QuestPopupUITK` script component**

```
mcp__gladekit-unity__add_component(
    gameobject_path="<parent>/QuestPopupUITK",
    component_type="QuestPopupUITK"
)
```

- [ ] **Step 5: Assign UIDocument references**

Set the UIDocument's `panelSettings` and `sourceAsset`:

```
mcp__gladekit-unity__set_object_reference(
    gameobject_path="<parent>/QuestPopupUITK",
    component_type="UnityEngine.UIElements.UIDocument",
    property_name="panelSettings",
    asset_path="Assets/Settings/RunewoodPanelSettings.asset"
)
mcp__gladekit-unity__set_object_reference(
    gameobject_path="<parent>/QuestPopupUITK",
    component_type="UnityEngine.UIElements.UIDocument",
    property_name="sourceAsset",
    asset_path="Assets/UI/QuestPopupUITK/QuestPopupUITK.uxml"
)
```

- [ ] **Step 6: Assign template references on the script**

```
mcp__gladekit-unity__set_script_component_property(
    gameobject_path="<parent>/QuestPopupUITK",
    script_class="QuestPopupUITK",
    property_name="questRowTemplate",
    asset_path="Assets/UI/QuestPopupUITK/QuestRowTemplate.uxml"
)
mcp__gladekit-unity__set_script_component_property(
    gameobject_path="<parent>/QuestPopupUITK",
    script_class="QuestPopupUITK",
    property_name="milestoneChipTemplate",
    asset_path="Assets/UI/QuestPopupUITK/MilestoneChipTemplate.uxml"
)
```

- [ ] **Step 7: Save the scene**

```
mcp__gladekit-unity__save_scene()
```

- [ ] **Step 8: Verify console**

```
mcp__UnityMCP__read_console()
```

Expected: no errors. The popup is hidden (`display: none` on root), so nothing visible yet — that's correct.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: add QuestPopupUITK GameObject + UIDocument to scene"
```

---

### Task 7: Add per-state milestone chip styling

Adds claimed / next / locked / final variations to the chip styling.

**Files:**
- Modify: `Assets/UI/QuestPopupUITK/QuestPopupUITK.uss` — append

- [ ] **Step 1: Append chip-state rules to USS**

Append to the bottom of `QuestPopupUITK.uss`:

```css
/* ─────────────────────────────────────────────────────────────
   Milestone chip — per-state
   ───────────────────────────────────────────────────────────── */

.chip--claimed {
    background-color: rgb(30, 54, 20);
    border-color: rgb(70, 113, 39);
}
.chip--claimed .chip__icon  { color: rgb(139, 195, 74); }
.chip--claimed .chip__tier  { color: rgb(139, 195, 74); }
.chip--claimed .chip__gem   { color: rgb(126, 203, 255); }

.chip--next {
    background-color: rgb(51, 38, 10);
    border-color: rgb(255, 184, 0);
    border-width: 3px;
}
.chip--next .chip__icon  { color: rgb(255, 184, 0); }
.chip--next .chip__tier  { color: rgb(255, 255, 255); }
.chip--next .chip__gem   { color: rgb(126, 203, 255); }

.chip--locked {
    background-color: rgb(26, 20, 8);
    border-color: rgb(46, 46, 46);
}
.chip--locked .chip__icon  { color: rgb(85, 85, 85); }
.chip--locked .chip__tier  { color: rgb(85, 85, 85); }
.chip--locked .chip__gem   { color: rgb(68, 102, 136); }
```

- [ ] **Step 2: Refresh and check**

```
mcp__UnityMCP__refresh_unity()
mcp__UnityMCP__read_console()
```

- [ ] **Step 3: Commit**

```bash
git add "Assets/UI/QuestPopupUITK/QuestPopupUITK.uss"
git commit -m "feat: add per-state milestone chip styling"
```

---

### Task 8: Add per-state quest row styling

Adds in-progress / completed / new card variations and per-objective progress-bar colors.

**Files:**
- Modify: `Assets/UI/QuestPopupUITK/QuestPopupUITK.uss` — append

- [ ] **Step 1: Append row-state rules**

```css
/* ─────────────────────────────────────────────────────────────
   Quest row — per-state
   ───────────────────────────────────────────────────────────── */

.quest-row--in-progress {
    background-color: rgb(46, 36, 16);
    border-color: rgb(89, 69, 18);
}

.quest-row--completed {
    background-color: rgb(23, 51, 16);
    border-color: rgb(70, 113, 39);
}
.quest-row--completed .quest-name { color: rgb(139, 195, 74); }

.quest-row--new {
    background-color: rgb(46, 36, 16);
    border-color: rgb(115, 80, 31);
}

/* Per-objective progress bar colors */
.quest-row--obj-harvestcrops .quest-row__progress-fill { background-color: rgb(139, 195, 74); }
.quest-row--obj-plantseeds   .quest-row__progress-fill { background-color: rgb(139, 195, 74); }
.quest-row--obj-waterplants  .quest-row__progress-fill { background-color: rgb(74, 159, 223); }
.quest-row--obj-repeldeer    .quest-row__progress-fill { background-color: rgb(223, 134, 74); }
.quest-row--obj-repelcrows   .quest-row__progress-fill { background-color: rgb(223, 134, 74); }
.quest-row--obj-gathereggs   .quest-row__progress-fill { background-color: rgb(223, 200, 74); }
.quest-row--obj-gathergems   .quest-row__progress-fill { background-color: rgb(126, 203, 255); }

/* NEW state overrides */
.quest-row--new .quest-row__progress-fill { background-color: rgb(176, 128, 77); }
```

- [ ] **Step 2: Refresh and verify**

```
mcp__UnityMCP__refresh_unity()
mcp__UnityMCP__read_console()
```

- [ ] **Step 3: Commit**

```bash
git add "Assets/UI/QuestPopupUITK/QuestPopupUITK.uss"
git commit -m "feat: add per-state quest row styling + per-objective bar colors"
```

---

### Task 9: Wire debug Shift-click toggle in existing `QuestPopup.cs`

The one-line integration: holding Shift while clicking the quest button opens the UI Toolkit version instead of the regular one.

**Files:**
- Modify: `Assets/Scripts/QuestPopup.cs:152-154`

- [ ] **Step 1: Replace the click handler registration**

Find this block (around line 152-154):

```csharp
backdropButton.onClick.AddListener(Close);
closeButton.onClick.AddListener(Close);
questButton.onClick.AddListener(Open);
```

Replace the third line so it becomes:

```csharp
backdropButton.onClick.AddListener(Close);
closeButton.onClick.AddListener(Close);
questButton.onClick.AddListener(OnQuestButtonClicked);
```

- [ ] **Step 2: Add the handler method**

Add this method to the `QuestPopup` class (anywhere — near the existing `Open()` is natural):

```csharp
private void OnQuestButtonClicked()
{
    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    if (shift && QuestPopupUITK.Instance != null)
        QuestPopupUITK.Instance.Open();
    else
        Open();
}
```

- [ ] **Step 3: Verify compile**

```
mcp__UnityMCP__refresh_unity()
mcp__UnityMCP__read_console()
```

Expected: zero errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/QuestPopup.cs
git commit -m "feat: Shift-click on quest button opens QuestPopupUITK (debug toggle)"
```

---

### Task 10: First-run smoke test in Play mode

Verify the popup actually opens and renders without runtime errors. This is the first visible result.

**Files:**
- None (verification only)

- [ ] **Step 1: Enter Play mode**

```
mcp__UnityMCP__manage_editor(action="enter_play_mode")
```

Wait for compilation to settle:

```
mcp__UnityMCP__read_console()
```

Confirm no errors.

- [ ] **Step 2: Open the UITK popup**

Instruct the user (or, if available, simulate): hold Shift and click the quest button on the home screen. The UI Toolkit popup should appear with the fade+scale animation.

- [ ] **Step 3: Check console for runtime errors**

```
mcp__UnityMCP__read_console()
```

Confirm no `NullReferenceException`, no `[QuestPopupUITK] element ... not found` warnings.

- [ ] **Step 4: User screenshot for visual gut-check**

Ask the user to screenshot the popup and drop the path. Compare to `docs/superpowers/specs/assets/2026-05-11-questpopup-reference.png`. Note any deltas.

- [ ] **Step 5: Exit Play mode**

```
mcp__UnityMCP__manage_editor(action="exit_play_mode")
```

- [ ] **Step 6: Commit (if you adjusted anything as a quick fix during smoke test)**

```bash
git status
# If only verification, no commit needed
```

---

### Task 11: Visual iteration loop

Open-ended polish task. Iterate the USS to match the reference image more closely based on screenshots.

**Files:**
- Modify: `Assets/UI/QuestPopupUITK/QuestPopupUITK.uss` (repeatedly)
- Modify: `Assets/UI/QuestPopupUITK/QuestPopupUITK.uxml` (only if structural)

For each visual delta the user identifies:

- [ ] **Step 1: User screenshots current state, names what's wrong**

E.g. "the milestone chips are too narrow", "the title color is wrong", "the claim button needs more padding".

- [ ] **Step 2: Single `Edit` to USS**

Make the targeted change. Do not rewrite the file.

- [ ] **Step 3: Refresh Unity**

```
mcp__UnityMCP__refresh_unity()
```

- [ ] **Step 4: User re-screenshots / confirms**

- [ ] **Step 5: Commit after every 1-3 visual fixes**

```bash
git add "Assets/UI/QuestPopupUITK/QuestPopupUITK.uss"
git commit -m "style: tune <element name> to match reference"
```

This task is the **workflow validation gate** — if these iterations take a single `Edit` each, UI Toolkit has won the experiment. If they each require multiple MCP scene calls and the popup still drifts visually, we have data to roll back.

---

### Task 12: Final acceptance review

Walk the spec's §10 acceptance criteria and decide whether UI Toolkit is the path forward for future menus.

**Files:**
- None (review only)

- [ ] **Step 1: Functional equivalence check**

In Play mode: open the new popup. Claim a completable quest. Confirm milestone strip updates. Confirm footer shows correct time/slots. Confirm Shift-not-held still opens the original popup. Confirm both can coexist without errors.

- [ ] **Step 2: Line-count check**

Count lines in:
- `Assets/Scripts/UI/QuestPopupUITK.cs`
- `Assets/UI/QuestPopupUITK/QuestPopupUITK.uxml`
- `Assets/UI/QuestPopupUITK/QuestPopupUITK.uss`
- `Assets/UI/QuestPopupUITK/QuestRowTemplate.uxml`
- `Assets/UI/QuestPopupUITK/MilestoneChipTemplate.uxml`

Total target: <450 lines (vs ~650 in original `QuestPopup.cs` + `QuestRow.cs` + `MilestoneChip.cs`).

- [ ] **Step 3: Workflow check**

Count how many of the polish iterations in Task 11 were achieved with a single `Edit` and no MCP scene calls. Target: at least 2.

- [ ] **Step 4: Subjective check with user**

Ask: "Would you rather build the next menu (Settings or Research) with UI Toolkit, or stick with uGUI?" Record the answer in the spec or as a memory.

- [ ] **Step 5: Decision commit**

```bash
git status
# Likely nothing to commit unless the user wants a note in the spec
```

If positive: queue the next menu as a UI Toolkit task. If negative: roll back by deleting `QuestPopupUITK` GameObject from scene and reverting the one-line `QuestPopup.cs` change.

---

## Self-review notes

**Spec coverage:**
- §1 Goal — Tasks 5-9 build the parallel popup; Task 9 wires the debug toggle.
- §2 Non-goals — No tasks touch other menus, save format, or QuestManager.
- §2a Visual target — Tasks 4, 7, 8 plus iteration in Task 11.
- §3 File layout — Tasks 2, 3, 5 create all files in the right locations.
- §4 Architecture — Task 5 implements the singleton + element caching + event subscription.
- §5 UXML sketch — Task 3 ships exactly this structure.
- §6 USS styling targets — Tasks 4, 7, 8 cover all listed states.
- §7 Replacement table — Implicitly satisfied by the new code (no anchor math, no LayoutRebuilder, no LeanTween).
- §8 Risks — Font, sort order, picking-mode, sprites all addressed in Tasks 2, 3, 4.
- §9 Iteration workflow — Task 11 is the iteration loop.
- §10 Acceptance — Task 12 walks all four criteria.

**Type consistency:** `QuestPopupUITK.Instance` referenced consistently. `QuestData` fields (`displayName`, `description`, `coinReward`, `targetCount`, `objectiveType`) match the asset's actual properties. `ActiveQuest` fields (`questID`, `progress`, `isCompleted`) match existing usage in `QuestRow.cs`. `QuestManager` API names all verified against current `QuestManager.cs`.

**Placeholder scan:** No "TBD", no "handle edge cases", no "similar to Task N". Code blocks present at every code step.
