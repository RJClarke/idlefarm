# QuestPopup — UI Toolkit Parallel Rebuild

**Date:** 2026-05-11
**Status:** Design — pending user review
**Author:** Brainstorming session

## 1. Goal

Validate UI Toolkit as a replacement (or complement) to uGUI for menu work on IdleFarm by rebuilding `QuestPopup` as a parallel, debug-toggled UI Toolkit version. The original `QuestPopup` stays fully intact and continues to be the default; the new version (`QuestPopupUITK`) opens via **Shift-click on the existing quest button** for direct A/B comparison.

The success criterion is workflow friction, not pixels: does the rebuilt version take meaningfully less effort to lay out, iterate on, and visually polish than the uGUI version? If yes, UI Toolkit becomes the preferred path for future menus (Settings, Research, Animals UI). If no, we revert with no production damage.

## 2. Non-goals

- Replacing the existing QuestPopup in production.
- Changing the quest data model, `QuestManager`, save format, or any other system the popup talks to.
- Migrating other menus (DrawerUI, BottomNav, DailyRewardPopup, RunStatsPopup, SeedSelectionPopup) — those are explicitly out of scope; this is an evaluation.
- Redoing visual design. The new version mirrors the existing dark-Runewood look closely enough for a fair A/B; visual redesign is a separate later task.

## 3. File layout

```
Assets/
├── UI/
│   └── QuestPopupUITK/
│       ├── QuestPopupUITK.uxml         ← popup structure (header, weekly strip, scroll list, footer)
│       ├── QuestPopupUITK.uss          ← all styling: colors, layout, transitions, hover states
│       ├── QuestRowTemplate.uxml       ← single quest-row template (cloned per active quest)
│       └── MilestoneChipTemplate.uxml  ← single milestone-chip template (cloned 8x)
├── Scripts/
│   └── UI/
│       └── QuestPopupUITK.cs           ← MonoBehaviour controller
└── Settings/
    └── RunewoodPanelSettings.asset     ← shared PanelSettings (sort order, scale mode, theme)
```

Existing files (untouched except for one debug-toggle line in `QuestPopup.cs`):
- `Assets/Scripts/QuestPopup.cs`
- `Assets/Scripts/QuestRow.cs`
- `Assets/Scripts/MilestoneChip.cs`
- `Assets/Prefabs/Quests/QuestRow.prefab`

## 4. Architecture

### Scene wiring

- New GameObject `QuestPopupUITK` added to the scene under the existing UI root.
- Has a `UIDocument` component referencing:
  - `Source Asset` → `QuestPopupUITK.uxml`
  - `Panel Settings` → `RunewoodPanelSettings.asset`
- Has the `QuestPopupUITK.cs` script attached.
- Root visual element starts with `display: none` so the popup is invisible until opened.

### Controller (`QuestPopupUITK.cs`)

A singleton MonoBehaviour, structurally similar to the original `QuestPopup`:

```csharp
public class QuestPopupUITK : MonoBehaviour
{
    public static QuestPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupContainer;
    private Button closeButton;
    private VisualElement backdrop;
    private Label weeklyCountLabel;
    private ProgressBar weeklyProgressBar;
    private VisualElement milestoneStrip;
    private ScrollView questList;
    private Label footerText;

    [SerializeField] private VisualTreeAsset questRowTemplate;
    [SerializeField] private VisualTreeAsset milestoneChipTemplate;

    private bool isOpen;

    // Lifecycle: OnEnable caches refs, subscribes to QuestManager events
    // Open()/Close() toggle "open" USS class — USS transition handles fade+scale
    // RefreshAll() rebuilds milestones, list, footer (same triggers as original)
}
```

Key responsibilities:
- **Element caching** via `root.Q<Type>("element-name")` in `OnEnable`. No inspector wiring.
- **Event subscription** to `QuestManager.Instance.OnQuestCompleted` and `OnQuestsDropped`, identical to original.
- **Open/close animation** by toggling a `.open` class on the popup container; USS transitions handle fade and scale.
- **Row rendering**: `questRowTemplate.Instantiate()` per active quest, populate fields via `Q<Label>(...).text = ...`, append to `questList`.
- **Milestone rendering**: clone `milestoneChipTemplate` 8 times; populate per tier.
- **Footer**: same time-remaining + slot count formatting as original.

### Debug toggle (existing `QuestPopup.cs` change)

In the `questButton.onClick` handler, single conditional:

```csharp
questButton.onClick.AddListener(() => {
    if (Input.GetKey(KeyCode.LeftShift) && QuestPopupUITK.Instance != null)
        QuestPopupUITK.Instance.Open();
    else
        Open();
});
```

One line to remove when promoting or rolling back. The Shift-click affordance is dev-only and undocumented in the UI; users will never hit it.

## 5. UXML structure (sketch)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="backdrop" class="backdrop">
        <ui:VisualElement name="popup-container" class="popup-container">

            <ui:VisualElement name="header" class="header">
                <ui:Label text="Quests" class="title" />
                <ui:Button name="close-button" text="✕" class="close-button" />
            </ui:VisualElement>

            <ui:VisualElement name="weekly-strip" class="weekly-strip">
                <ui:Label name="weekly-count" class="weekly-count" />
                <ui:ProgressBar name="weekly-progress" class="weekly-progress" />
                <ui:VisualElement name="milestone-strip" class="milestone-strip" />
            </ui:VisualElement>

            <ui:ScrollView name="quest-list" class="quest-list" />

            <ui:Label name="footer-text" class="footer" />

        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

## 6. USS styling targets

All visual values mirror the existing Runewood palette already in `QuestRow.cs` / `MilestoneChip.cs`:

- Backdrop: `rgba(0, 0, 0, 0.5)`, full-screen flex-centered
- Popup container: dark brown background (~`#21190C`), Runewood border via 9-sliced sprite, `border-radius: 8px`, `padding: 12px 10px`
- Quest row backgrounds: in-progress `#2E2410`, completed `#173310`, new `#2E2410` (matches existing constants)
- Quest row borders: in-progress `#594512`, completed `#478D29`, new `#73501F`
- Progress bar fills: harvest `#8BC34A`, water `#4A9FDF`, new `#B0804D`
- Open animation: `transition: opacity 0.3s ease-out, scale 0.3s ease-out;` triggered by `.open` class
- Closed state: `opacity: 0; scale: 0.8 0.8;`
- Open state: `opacity: 1; scale: 1 1;`

## 7. What goes away vs. current code

| Current (uGUI) pain | UI Toolkit replacement |
|---|---|
| `SetRect()` / `Stretch()` anchor helpers in `QuestRow.cs` | Flexbox in USS |
| `ApplyLayoutFix()` runtime padding patches | Static USS rules |
| Manual scrollbar construction (`QuestPopup.cs` lines 84–148) | Built-in `<ui:ScrollView>` |
| `LayoutRebuilder.ForceRebuildLayoutImmediate` | Automatic layout |
| LeanTween fade/scale animation | USS `transition` |
| Inspector-wired references that drop on prefab edits | `root.Q<>(name)` lookups for runtime elements; only two static `VisualTreeAsset` template refs remain (row, chip), and those are assets — not scene-instantiated children that can drift |
| `QuestRow.Awake` reparenting and anchor math | One UXML template, declared once |

Approximate line count: `QuestPopup.cs` + `QuestRow.cs` + `MilestoneChip.cs` total ~650 lines; expected `QuestPopupUITK.cs` ~150 lines plus UXML/USS.

## 8. Risks & gotchas

- **Font asset compatibility**: Unity 6.3 UI Toolkit supports TMP `FontAsset` via `-unity-font-definition`. Existing `NotoSans-Regular SDF` should plug in directly; emoji fallback list on the FontAsset is preserved. Verify on first render.
- **Sort order vs. existing Canvas**: `PanelSettings.sortingOrder` must exceed the home-screen Canvas sort order, or popup renders behind. Configure on the PanelSettings asset.
- **Input bleed-through**: UI Toolkit `picking-mode` defaults to capturing pointer events. Backdrop must capture (to handle backdrop-click close); transparent decoration elements should set `picking-mode: ignore`.
- **9-slice sprites in UI Toolkit**: borders use `background-image: url(...)` plus `-unity-slice-left/right/top/bottom`. Existing `UI_Wood_Border` sprites should work as-is.
- **One-time MCP scene wiring**: adding the `QuestPopupUITK` GameObject + `UIDocument` component requires `gladekit-unity` calls. After that, all iteration is text-file edits.
- **Rollback path**: deleting the `QuestPopupUITK` GameObject and reverting the one-line `QuestPopup.cs` toggle removes the feature with zero residual changes.

## 9. Iteration workflow

After initial implementation:

1. User opens the popup via Shift-click on quest button in the Editor.
2. User screenshots to `Assets/Screenshots/` (existing habit).
3. User pastes screenshot path into chat.
4. Assistant reads the screenshot, identifies what's off, edits `QuestPopupUITK.uss` (or UXML if structural).
5. Unity hot-reloads on file save; user re-opens popup to compare.
6. Loop until satisfied.

Compared to the current uGUI workflow (every tweak = several MCP calls, scene save, recompile cycle), each visual iteration is one `Edit` call.

## 10. Acceptance / "is this a win?"

We declare UI Toolkit a workflow win for this project if, after the rebuild:

1. The new QuestPopup is functionally equivalent (opens, lists quests, claims work, milestones claim, footer updates) — **must pass**.
2. The new code is at least 30% fewer lines for the same surface area — **target met if <450 lines total across .cs/.uxml/.uss**.
3. At least two pure-visual iterations were completed via single `Edit` calls without any MCP round-trips — **demonstrates the workflow improvement**.
4. User's subjective rating: "easier to work with, want to use for next menu" vs. "no improvement / worse" — **gates whether we expand to other menus**.

If 1–3 pass and 4 is positive, the next menu (likely Settings or Research) is built UI-Toolkit-first.
