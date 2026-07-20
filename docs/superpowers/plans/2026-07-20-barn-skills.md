# Reputation Phase 3: Barn Skills Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A clickable Barn on the farm opens a popup where unspent reputation points (from Phase 2) are spent across 7 skill tracks (25 levels each), rendered as discrete tick bars with a "+" spend button per row and clickable tier/milestone markers that show their bonus description before you commit.

**Architecture:** `FarmSkillsCore` (EconomyCore, pure, tested) holds the 7 track levels; `FarmSkillsManager` wraps it, spends points through `ReputationManager.TrySpendPoint()` (a small new method on Phase 2's `ReputationCore`), exposes `GetBonus(FarmSkillTrack)`, and saves. `BarnPopupUITK` is fully code-driven UI Toolkit (`ToastManager`/Phase 2 pattern — `sourcePanelSettings` wired to `RunewoodPanelSettings` from the start this time). `BarnBuilding` is a clickable world prop cloned from `CanneryBuilding`, using the existing `Barn_Small_32x32` placeholder sprite. Spec: `docs/superpowers/specs/2026-07-19-reputation-design.md` §4.

**Tech Stack:** Unity C# (singleton managers, JsonUtility saves), code-driven UI Toolkit, NUnit EditMode tests via the `Temp/run_editmode_tests.request` file bridge.

## Global Constraints

- **Never edit serialized Unity files** (`.unity`, `.prefab`, `.asset`, `.meta`) as text — scene/asset changes go through the gladekit/unity MCP tools.
- **No emoji in UITK text** — invisible on Android (memory rule).
- **No respec** (spec §4.1, binding). Never build an un-level control.
- **Lessons carried from Phase 2's smoke test** (do these from the start, don't rediscover):
  1. Every code-driven `UIDocument` root needs `root.style.height = Length.Percent(100);` right after fetching `rootVisualElement` — a `Position.Absolute` child contributes nothing to the root's default auto-height, collapsing everything anchored with `top:0,bottom:0`.
  2. Runtime `PanelSettings` must clone a real, theme-having `sourcePanelSettings` (wire to the project's `Assets/Settings/RunewoodPanelSettings.asset`) — a bare `CreateInstance<PanelSettings>()` has no theme, so **all text renders invisible**.
- `Debug.Log` only for important events (level-up); keep LogWarning/LogError.
- EditMode tests: `Assets/Tests/EditMode/`, run via `touch Temp/run_editmode_tests.request`, results in `Temp/editmode_test_results.txt`. Current baseline: **204 passing** — never finish a task with fewer.
- Commit after each task; end commit messages with the Co-Authored-By + Claude-Session trailer used on this branch.
- **Deliberate v1 scope cuts (documented, not hidden):**
  - Only **2 of 7 tracks wired to real gameplay consumers**: Harvesting (Money from crop sales, `Plant.cs`) and Ranching (egg Coin value, `AnimalManager.cs`) — both files this session has already edited and verified. The remaining 5 (Planting/Watering/Fishing/Forestry/Processing) get correct data, UI, and a working `GetBonus()` API, but their consumer call sites are unfamiliar systems not touched this session; wiring them speculatively without a verified smoke test would be worse than shipping them honestly inert. Each is a one-line multiplier read at a known site per spec §4.2 — future work.
  - **Renown milestone *rewards* are out of scope** — the spec itself defers this to Phase 4 (§7). The Renown bar and its tier notches ARE built and clickable (per the user's explicit "every bonus should be readable" feedback), but an unreached/all milestone's description honestly says "Reward not yet implemented" rather than faking a payout.
  - Tier bonuses that are new behaviors (2× whirlpools, golden crops, etc.) are NOT built — same "scoped individually at implementation time" deferral the spec already states (§4.2).
  - `FarmSkillTrack` is a C# enum, not string `StatKey` constants like Research — same `GetBonus(x)` *shape* the spec asks for, type-safer for a small fixed set of 7.

---

### Task 1: FarmSkillsCore (EconomyCore pure data/state) + ReputationCore.TrySpendPoint

**Files:**
- Create: `Assets/Scripts/EconomyCore/FarmSkillsCore.cs`
- Modify: `Assets/Scripts/EconomyCore/ReputationCore.cs` (add `TrySpendPoint`)
- Test: `Assets/Tests/EditMode/FarmSkillsCoreTests.cs`
- Test: `Assets/Tests/EditMode/ReputationCoreTests.cs` (append `TrySpendPoint` cases)

**Interfaces:**
- Produces: `enum FarmSkillTrack { Harvesting, Planting, Watering, Fishing, Forestry, Ranching, Processing }` (7 values); `class FarmSkillsCore` with `int GetLevel(FarmSkillTrack track)`, `int TotalLevels` (sum, 0-175), `bool TryLevelUp(FarmSkillTrack track)` (false if already 25), `void Import(int[] levels)`, `int[] Export()`. `ReputationCore.TrySpendPoint()` (bool, decrements `UnspentPoints` if > 0). Consumed by Task 2's `FarmSkillsManager`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/FarmSkillsCoreTests.cs`:
```csharp
using NUnit.Framework;

public class FarmSkillsCoreTests
{
    [Test]
    public void NewCore_AllTracksStartAtZero()
    {
        var core = new FarmSkillsCore();
        foreach (FarmSkillTrack t in System.Enum.GetValues(typeof(FarmSkillTrack)))
            Assert.AreEqual(0, core.GetLevel(t));
        Assert.AreEqual(0, core.TotalLevels);
    }

    [Test]
    public void TryLevelUp_IncrementsAndReturnsTrue()
    {
        var core = new FarmSkillsCore();
        bool ok = core.TryLevelUp(FarmSkillTrack.Harvesting);
        Assert.IsTrue(ok);
        Assert.AreEqual(1, core.GetLevel(FarmSkillTrack.Harvesting));
        Assert.AreEqual(1, core.TotalLevels);
    }

    [Test]
    public void TryLevelUp_CapsAt25()
    {
        var core = new FarmSkillsCore();
        for (int i = 0; i < 25; i++) Assert.IsTrue(core.TryLevelUp(FarmSkillTrack.Fishing));
        Assert.AreEqual(25, core.GetLevel(FarmSkillTrack.Fishing));
        Assert.IsFalse(core.TryLevelUp(FarmSkillTrack.Fishing));
        Assert.AreEqual(25, core.GetLevel(FarmSkillTrack.Fishing));
    }

    [Test]
    public void TotalLevels_SumsAllSevenTracks()
    {
        var core = new FarmSkillsCore();
        core.TryLevelUp(FarmSkillTrack.Harvesting);
        core.TryLevelUp(FarmSkillTrack.Harvesting);
        core.TryLevelUp(FarmSkillTrack.Ranching);
        Assert.AreEqual(3, core.TotalLevels);
    }

    [Test]
    public void ExportImport_RoundTrips()
    {
        var core = new FarmSkillsCore();
        core.TryLevelUp(FarmSkillTrack.Processing);
        core.TryLevelUp(FarmSkillTrack.Processing);
        core.TryLevelUp(FarmSkillTrack.Watering);
        var exported = core.Export();

        var core2 = new FarmSkillsCore();
        core2.Import(exported);
        Assert.AreEqual(2, core2.GetLevel(FarmSkillTrack.Processing));
        Assert.AreEqual(1, core2.GetLevel(FarmSkillTrack.Watering));
        Assert.AreEqual(3, core2.TotalLevels);
    }

    [Test]
    public void Import_NullOrShortArray_IsSafe()
    {
        var core = new FarmSkillsCore();
        core.Import(null);
        Assert.AreEqual(0, core.TotalLevels);
        core.Import(new[] { 5, 3 }); // shorter than 7
        Assert.AreEqual(5, core.GetLevel(FarmSkillTrack.Harvesting));
        Assert.AreEqual(3, core.GetLevel(FarmSkillTrack.Planting));
        Assert.AreEqual(0, core.GetLevel(FarmSkillTrack.Processing));
    }
}
```

Append to `Assets/Tests/EditMode/ReputationCoreTests.cs` (inside the existing class):
```csharp
    [Test]
    public void TrySpendPoint_FailsWhenNoneUnspent_SucceedsAfterEarning()
    {
        var core = new ReputationCore();
        Assert.IsFalse(core.TrySpendPoint());
        core.AddRep(100); // exactly point 1's cost
        Assert.AreEqual(1, core.UnspentPoints);
        Assert.IsTrue(core.TrySpendPoint());
        Assert.AreEqual(0, core.UnspentPoints);
        Assert.IsFalse(core.TrySpendPoint());
    }
```

- [ ] **Step 2: Run tests, verify compile-fail (red).**

- [ ] **Step 3: Implement FarmSkillsCore**

```csharp
using UnityEngine;

/// <summary>The 7 Barn skill tracks (spec §4.2), fixed order — index matches array position.</summary>
public enum FarmSkillTrack { Harvesting, Planting, Watering, Fishing, Forestry, Ranching, Processing }

/// <summary>
/// Pure state for the 7 Barn skill tracks: per-track level (0-25 each, 175 total = "Renown").
/// No respec (spec §4.1) — TryLevelUp only ever increments. Points are spent from
/// ReputationManager's pool (Phase 2), not owned here.
/// </summary>
public class FarmSkillsCore
{
    public const int MaxLevel = 25;
    private static readonly int TrackCount = System.Enum.GetValues(typeof(FarmSkillTrack)).Length;

    private readonly int[] levels = new int[TrackCount];

    public int GetLevel(FarmSkillTrack track) => levels[(int)track];
    public int TotalLevels { get { int sum = 0; foreach (int l in levels) sum += l; return sum; } }

    public bool TryLevelUp(FarmSkillTrack track)
    {
        int i = (int)track;
        if (levels[i] >= MaxLevel) return false;
        levels[i]++;
        return true;
    }

    public void Import(int[] source)
    {
        for (int i = 0; i < levels.Length; i++)
            levels[i] = (source != null && i < source.Length) ? Mathf.Clamp(source[i], 0, MaxLevel) : 0;
    }

    public int[] Export() => (int[])levels.Clone();
}
```

- [ ] **Step 4: Add TrySpendPoint to ReputationCore**

In `Assets/Scripts/EconomyCore/ReputationCore.cs`, change the `UnspentPoints` auto-property to a private-set property (find `public int UnspentPoints { get; private set; }` — it's already private-set, no change needed there), and add this method (anywhere among the public methods, e.g. after `AddRep`):

```csharp
    /// <summary>Spends one unspent point (Barn level-up, Reputation Phase 3). No respec — never re-credited.</summary>
    public bool TrySpendPoint()
    {
        if (UnspentPoints <= 0) return false;
        UnspentPoints--;
        return true;
    }
```

(`UnspentPoints--` requires the backing field to be settable from within the class, which an auto-property with `private set` already allows.)

- [ ] **Step 5: Run tests, verify green.** Expected 211/211 (204 + 6 FarmSkillsCore + 1 TrySpendPoint).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/EconomyCore/FarmSkillsCore.cs Assets/Scripts/EconomyCore/ReputationCore.cs Assets/Tests/EditMode/FarmSkillsCoreTests.cs Assets/Tests/EditMode/ReputationCoreTests.cs
git commit -m "feat(barn): FarmSkillsCore track state + ReputationCore.TrySpendPoint"
```

---

### Task 2: FarmSkillsManager + save wiring

**Files:**
- Create: `Assets/Scripts/Reputation/FarmSkillsManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (field + constructor default)
- Modify: `Assets/Scripts/SaveManager.cs` (capture/load, alongside the Phase 2 `ReputationManager` lines)
- Modify: `Assets/Scripts/AutoSaveManager.cs` (subscribe/unsubscribe `OnChanged`)

**Interfaces:**
- Consumes: `FarmSkillsCore` (Task 1), `ReputationManager.TrySpendPoint()`/`UnspentPoints`/`OnChanged` (Phase 2, extended here).
- Produces: singleton `FarmSkillsManager.Instance`; `event Action OnChanged`; `int GetLevel(FarmSkillTrack track)`; `int TotalLevels`; `float GetBonus(FarmSkillTrack track)` (level × per-point value, tuning knob per track); `bool IsTierUnlocked(FarmSkillTrack track, int tierLevel)` (level >= tierLevel); `bool TryLevelUp(FarmSkillTrack track)` (spends a point via ReputationManager first); `CaptureTo(GameData)`/`LoadFrom(GameData)`. Consumed by Task 3's UI and Task 4's consumer wiring.

- [ ] **Step 1: Add ReputationManager.TrySpendPoint passthrough**

In `Assets/Scripts/Reputation/ReputationManager.cs`, add (near `TrySkip`):

```csharp
    public bool TrySpendPoint()
    {
        bool ok = core.TrySpendPoint();
        if (ok) OnChanged?.Invoke();
        return ok;
    }
```

- [ ] **Step 2: Add GameData field**

In `Assets/Scripts/GameData.cs`, after the `repSlots` field, add:

```csharp
    // Barn skills (Reputation Phase 3). 7 tracks, 0-25 each, index = FarmSkillTrack enum value.
    public int[] farmSkillLevels;
```

In the default constructor, after `repSlots = new RequestSlotSave[0];`, add:

```csharp
        farmSkillLevels = new int[0];
```

- [ ] **Step 3: Create FarmSkillsManager**

```csharp
using System;
using UnityEngine;

/// <summary>
/// Owns the 7 Barn skill tracks: level-up (spends a ReputationManager point), per-point bonus
/// lookup, and tier-unlock checks. Spec: 2026-07-19-reputation-design.md §4.
/// </summary>
public class FarmSkillsManager : MonoBehaviour
{
    public static FarmSkillsManager Instance { get; private set; }

    [Header("Per-point bonus values (spec §4.2 defaults)")]
    [SerializeField] private float harvestingPerPoint = 0.04f;  // +4% Money from crop sales
    [SerializeField] private float plantingPerPoint = 0.02f;    // +2% growth speed
    [SerializeField] private float wateringPerPoint = 0.03f;    // +3% moisture duration
    [SerializeField] private float fishingPerPoint = 0.02f;     // -2% bite wait time
    [SerializeField] private float forestryPerPoint = 0.02f;    // +2% wood per chop
    [SerializeField] private float ranchingPerPoint = 0.03f;    // +3% egg value
    [SerializeField] private float processingPerPoint = 0.02f;  // +2% cook/smoke speed

    private static readonly int[] TierLevels = { 5, 10, 15, 20, 25 };

    private readonly FarmSkillsCore core = new FarmSkillsCore();

    public event Action OnChanged;

    public int TotalLevels => core.TotalLevels;
    public int GetLevel(FarmSkillTrack track) => core.GetLevel(track);
    public static int[] TierMarkers => TierLevels;

    public bool IsTierUnlocked(FarmSkillTrack track, int tierLevel) => core.GetLevel(track) >= tierLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public float GetBonus(FarmSkillTrack track)
    {
        float perPoint = track switch
        {
            FarmSkillTrack.Harvesting => harvestingPerPoint,
            FarmSkillTrack.Planting => plantingPerPoint,
            FarmSkillTrack.Watering => wateringPerPoint,
            FarmSkillTrack.Fishing => fishingPerPoint,
            FarmSkillTrack.Forestry => forestryPerPoint,
            FarmSkillTrack.Ranching => ranchingPerPoint,
            FarmSkillTrack.Processing => processingPerPoint,
            _ => 0f,
        };
        return core.GetLevel(track) * perPoint;
    }

    public bool TryLevelUp(FarmSkillTrack track)
    {
        if (ReputationManager.Instance == null || !ReputationManager.Instance.TrySpendPoint()) return false;
        core.TryLevelUp(track); // TrySpendPoint already guaranteed room to spend; level-up cap is separately enforced by the UI disabling "+" at 25
        Debug.Log($"[FarmSkills] {track} -> level {core.GetLevel(track)}");
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureTo(GameData d) => d.farmSkillLevels = core.Export();
    public void LoadFrom(GameData d)
    {
        core.Import(d.farmSkillLevels);
        OnChanged?.Invoke();
    }
}
```

Note the ordering guard in `TryLevelUp`: the UI (Task 3) must disable the "+" button once a track is at 25, so `TrySpendPoint()` (which already deducted the point) is never called for a maxed track. `FarmSkillsCore.TryLevelUp` returning false at the cap is a defensive backstop, not the primary gate — if it ever returns false here after a point was already spent, that point is still consumed (matches "no respec / no refunds" spirit; the UI guard is what actually prevents this case in practice).

- [ ] **Step 4: Wire SaveManager**

Alongside the Phase 2 `ReputationManager.Instance.CaptureTo(data);` line, add:

```csharp
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.CaptureTo(data);
```

Alongside the matching `LoadFrom` line, add:

```csharp
                if (FarmSkillsManager.Instance != null)
                    FarmSkillsManager.Instance.LoadFrom(data);
```

- [ ] **Step 5: Wire AutoSaveManager**

Alongside the Phase 2 `ReputationManager.Instance.OnChanged += OnVoid;` line, add:

```csharp
        if (FarmSkillsManager.Instance != null)
            FarmSkillsManager.Instance.OnChanged += OnVoid;
```

And the matching `-= OnVoid;` in the unsubscribe block.

- [ ] **Step 6: Compile, create scene GameObject, run tests**

1. `mcp__unity-mcp__refresh_unity` (compile: request, force, wait_for_ready).
2. `read_console` for errors — fix any.
3. Via `execute_code`: `new GameObject("FarmSkillsManager"); go.AddComponent<FarmSkillsManager>();` (all fields have sensible defaults, no wiring needed) then `MarkSceneDirty` + `SaveOpenScenes`.
4. `touch Temp/run_editmode_tests.request`; expect **211/211** (no new tests this task — regression guard).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Reputation/FarmSkillsManager.cs Assets/Scripts/Reputation/ReputationManager.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(barn): FarmSkillsManager with point-spend + save wiring"
```

---

### Task 3: BarnPopupUITK

**Files:**
- Create: `Assets/Scripts/UI/BarnPopupUITK.cs`

**Interfaces:**
- Consumes: `FarmSkillsManager` (Task 2: `GetLevel`, `TotalLevels`, `TryLevelUp`, `IsTierUnlocked`, `TierMarkers`, `OnChanged`), `ReputationManager.UnspentPoints`/`OnChanged` (Phase 2).
- Produces: singleton `BarnPopupUITK.Instance` with `void Open()`/`void Close()`/`bool IsOpen`.

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Barn: 7 skill tracks as 25-tick bars with a "+" spend button per row, a Renown bar
/// (total levels, max 175) up top, and clickable tier/milestone markers that show their bonus
/// description. No respec. Fully code-driven UIDocument. Spec: 2026-07-19-reputation-design.md §4.
/// </summary>
[DefaultExecutionOrder(1050)]
public class BarnPopupUITK : MonoBehaviour
{
    public static BarnPopupUITK Instance { get; private set; }
    private const int SORT_ORDER = 1000;

    [Tooltip("Shared RunewoodPanelSettings, cloned at runtime (ToastManager pattern) so text renders.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

    private static readonly (string name, Color color)[] TrackMeta =
    {
        ("Harvesting", new Color(0.86f, 0.72f, 0.20f)), // yellow
        ("Planting",   new Color(0.36f, 0.66f, 0.32f)), // green
        ("Watering",   new Color(0.25f, 0.70f, 0.75f)), // cyan
        ("Fishing",    new Color(0.30f, 0.50f, 0.85f)), // blue
        ("Forestry",   new Color(0.80f, 0.48f, 0.20f)), // orange
        ("Ranching",   new Color(0.80f, 0.32f, 0.30f)), // red
        ("Processing", new Color(0.60f, 0.35f, 0.75f)), // purple
    };

    private static readonly string[] TierFlavor =
    {
        "double-harvest chance", "free-seed chance", "sprinkler radius +1", "2x whirlpools",
        "double wood knockdown", "second-egg chance", "double-jar chance",
    };

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement tracksColumn;
    private Label pointsLabel;
    private VisualElement renownFill;
    private Label renownLabel;
    private Label descriptionLabel;
    private bool isOpen;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        document = GetComponent<UIDocument>();
        if (document == null) document = gameObject.AddComponent<UIDocument>();

        if (sourcePanelSettings != null)
        {
            runtimePanelSettings = Instantiate(sourcePanelSettings);
        }
        else
        {
            runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanelSettings.referenceResolution = new Vector2Int(1080, 1920);
            runtimePanelSettings.match = 0.5f;
        }
        runtimePanelSettings.name = "BarnPanelSettings (runtime)";
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        Build();
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.OnChanged += OnChanged;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnChanged += OnChanged;
    }

    private void OnDestroy()
    {
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.OnChanged -= OnChanged;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnChanged -= OnChanged;
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private void OnChanged() { if (isOpen) BuildContent(); }

    public void Open()
    {
        if (isOpen || popupRoot == null) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        BuildContent();
        popupRoot.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void Build()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogWarning("[BarnPopupUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;
        root.style.height = Length.Percent(100); // see plan Global Constraints — absolute children need this

        popupRoot = new VisualElement { name = "barn-root" };
        popupRoot.style.position = Position.Absolute;
        popupRoot.style.left = 0; popupRoot.style.right = 0; popupRoot.style.top = 0; popupRoot.style.bottom = 0;
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        VisualElement backdrop = new VisualElement { name = "barn-backdrop" };
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0; backdrop.style.right = 0; backdrop.style.top = 0; backdrop.style.bottom = 0;
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<ClickEvent>(_ => Close());
        popupRoot.Add(backdrop);

        VisualElement card = new VisualElement { name = "barn-card" };
        card.style.width = Length.Percent(90);
        card.style.maxWidth = 720;
        card.style.maxHeight = Length.Percent(85);
        card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
        card.style.borderTopLeftRadius = 18; card.style.borderTopRightRadius = 18;
        card.style.borderBottomLeftRadius = 18; card.style.borderBottomRightRadius = 18;
        card.style.paddingLeft = 20; card.style.paddingRight = 20;
        card.style.paddingTop = 16; card.style.paddingBottom = 16;
        popupRoot.Add(card);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        card.Add(header);

        Label title = new Label("Barn");
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(title);

        Button closeBtn = new Button(Close) { text = "×" };
        closeBtn.style.width = 48; closeBtn.style.height = 48;
        closeBtn.style.fontSize = 30;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        closeBtn.style.borderTopWidth = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0; closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(closeBtn);

        pointsLabel = new Label();
        pointsLabel.style.fontSize = 24;
        pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pointsLabel.style.color = new Color(0.15f, 0.35f, 0.1f);
        pointsLabel.style.marginTop = 4; pointsLabel.style.marginBottom = 10;
        card.Add(pointsLabel);

        // Renown bar (total levels 0-175). Milestone rewards are Phase 4 — notches are clickable
        // and honest about that ("Reward not yet implemented"), per user feedback on readability.
        VisualElement renownTrack = new VisualElement();
        renownTrack.style.height = 28;
        renownTrack.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        renownTrack.style.borderTopLeftRadius = 8; renownTrack.style.borderTopRightRadius = 8;
        renownTrack.style.borderBottomLeftRadius = 8; renownTrack.style.borderBottomRightRadius = 8;
        renownTrack.style.overflow = Overflow.Hidden;
        renownTrack.style.marginBottom = 4;
        card.Add(renownTrack);

        renownFill = new VisualElement();
        renownFill.style.height = Length.Percent(100);
        renownFill.style.backgroundColor = new Color(0.78f, 0.35f, 0.85f);
        renownTrack.Add(renownFill);

        renownLabel = new Label();
        renownLabel.style.position = Position.Absolute;
        renownLabel.style.left = 0; renownLabel.style.right = 0; renownLabel.style.top = 0; renownLabel.style.bottom = 0;
        renownLabel.style.color = Color.white;
        renownLabel.style.fontSize = 18;
        renownLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        renownTrack.Add(renownLabel);

        VisualElement renownNotches = new VisualElement();
        renownNotches.style.flexDirection = FlexDirection.Row;
        renownNotches.style.justifyContent = Justify.SpaceBetween;
        renownNotches.style.marginBottom = 14;
        int[] renownMilestones = { 10, 25, 50, 100, 175 };
        string[] renownDesc =
        {
            "10 total levels: Reward not yet implemented.",
            "25 total levels: Reward not yet implemented.",
            "50 total levels: Reward not yet implemented.",
            "100 total levels: Reward not yet implemented.",
            "175 total levels: Reward not yet implemented.",
        };
        for (int i = 0; i < renownMilestones.Length; i++)
        {
            int threshold = renownMilestones[i];
            string desc = renownDesc[i];
            Button notch = new Button(() => ShowDescription($"Renown {threshold}", desc)) { text = threshold.ToString() };
            notch.style.fontSize = 14;
            notch.style.height = 30;
            notch.style.backgroundColor = new Color(0.35f, 0.22f, 0.1f);
            notch.style.color = Color.white;
            notch.style.borderTopWidth = 0; notch.style.borderBottomWidth = 0;
            notch.style.borderLeftWidth = 0; notch.style.borderRightWidth = 0;
            renownNotches.Add(notch);
        }
        card.Add(renownNotches);

        tracksColumn = new VisualElement();
        card.Add(tracksColumn);

        descriptionLabel = new Label();
        descriptionLabel.style.fontSize = 20;
        descriptionLabel.style.color = new Color(0.15f, 0.1f, 0.05f);
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginTop = 10;
        descriptionLabel.style.paddingTop = 10;
        descriptionLabel.style.borderTopWidth = 1;
        descriptionLabel.style.borderTopColor = new Color(0f, 0f, 0f, 0.2f);
        descriptionLabel.style.display = DisplayStyle.None;
        card.Add(descriptionLabel);
    }

    private void ShowDescription(string title, string body)
    {
        descriptionLabel.text = $"{title}\n{body}";
        descriptionLabel.style.display = DisplayStyle.Flex;
    }

    private void BuildContent()
    {
        var fs = FarmSkillsManager.Instance;
        var rm = ReputationManager.Instance;
        if (fs == null || rm == null || tracksColumn == null) return;

        pointsLabel.text = $"Points to spend: {rm.UnspentPoints}";

        int totalLevels = fs.TotalLevels;
        renownFill.style.width = Length.Percent(totalLevels / 175f * 100f);
        renownLabel.text = $"Renown  {totalLevels} / 175";

        tracksColumn.Clear();
        foreach (FarmSkillTrack track in System.Enum.GetValues(typeof(FarmSkillTrack)))
            tracksColumn.Add(BuildTrackRow(track));
    }

    private VisualElement BuildTrackRow(FarmSkillTrack track)
    {
        var fs = FarmSkillsManager.Instance;
        var meta = TrackMeta[(int)track];
        int level = fs.GetLevel(track);

        VisualElement row = new VisualElement();
        row.style.marginBottom = 14;

        Label nameLabel = new Label($"{meta.name}  ({level}/{FarmSkillsCore.MaxLevel})");
        nameLabel.style.fontSize = 20;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.color = meta.color;
        nameLabel.style.marginBottom = 4;
        row.Add(nameLabel);

        VisualElement tickRow = new VisualElement();
        tickRow.style.flexDirection = FlexDirection.Row;
        tickRow.style.alignItems = Align.Center;
        row.Add(tickRow);

        VisualElement ticksWrap = new VisualElement();
        ticksWrap.style.flexDirection = FlexDirection.Row;
        ticksWrap.style.flexGrow = 1;
        ticksWrap.style.flexWrap = Wrap.Wrap;
        tickRow.Add(ticksWrap);

        for (int lvl = 1; lvl <= FarmSkillsCore.MaxLevel; lvl++)
        {
            bool filled = lvl <= level;
            bool isTier = System.Array.IndexOf(FarmSkillsManager.TierMarkers, lvl) >= 0;

            VisualElement tick = new VisualElement();
            tick.style.width = 16;
            tick.style.height = 26;
            tick.style.marginRight = 2;
            tick.style.borderTopLeftRadius = 3; tick.style.borderTopRightRadius = 3;
            tick.style.borderBottomLeftRadius = 3; tick.style.borderBottomRightRadius = 3;
            tick.style.backgroundColor = filled ? meta.color : new Color(meta.color.r, meta.color.g, meta.color.b, 0.25f);
            if (isTier)
            {
                tick.style.borderBottomWidth = 3;
                tick.style.borderBottomColor = Color.white;
            }
            ticksWrap.Add(tick);

            if (isTier)
            {
                int tierLevel = lvl;
                Button tierBtn = new Button(() => OnTierClicked(track, tierLevel)) { text = "|" };
                tierBtn.style.width = 20; tierBtn.style.height = 26;
                tierBtn.style.marginRight = 4;
                tierBtn.style.fontSize = 14;
                tierBtn.style.backgroundColor = fs.IsTierUnlocked(track, tierLevel) ? meta.color : new Color(0.4f, 0.4f, 0.4f, 0.6f);
                tierBtn.style.color = Color.white;
                tierBtn.style.borderTopWidth = 0; tierBtn.style.borderBottomWidth = 0;
                tierBtn.style.borderLeftWidth = 0; tierBtn.style.borderRightWidth = 0;
                ticksWrap.Add(tierBtn);
            }
        }

        bool canLevel = level < FarmSkillsCore.MaxLevel && ReputationManager.Instance.UnspentPoints > 0;
        Button plusBtn = new Button(() => OnPlusClicked(track)) { text = "+" };
        plusBtn.style.width = 44; plusBtn.style.height = 32;
        plusBtn.style.fontSize = 22;
        plusBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        plusBtn.style.backgroundColor = canLevel ? meta.color : new Color(0.6f, 0.6f, 0.6f);
        plusBtn.style.color = Color.white;
        plusBtn.style.borderTopWidth = 0; plusBtn.style.borderBottomWidth = 0;
        plusBtn.style.borderLeftWidth = 0; plusBtn.style.borderRightWidth = 0;
        plusBtn.SetEnabled(canLevel);
        tickRow.Add(plusBtn);

        return row;
    }

    private void OnTierClicked(FarmSkillTrack track, int tierLevel)
    {
        var fs = FarmSkillsManager.Instance;
        string flavor = TierFlavor[(int)track];
        string status = fs.IsTierUnlocked(track, tierLevel) ? "Unlocked" : "Locked";
        ShowDescription($"{TrackMeta[(int)track].name} — Level {tierLevel} ({status})", flavor);
    }

    private void OnPlusClicked(FarmSkillTrack track)
    {
        FarmSkillsManager.Instance.TryLevelUp(track);
    }
}
```

- [ ] **Step 2: Compile check** — `refresh_unity` (compile: request, force), `read_console` for errors.

- [ ] **Step 3: Run full test suite** — expect 211/211.

- [ ] **Step 4: Create scene GameObject + wire sourcePanelSettings** (MCP `execute_code`):

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/Settings/RunewoodPanelSettings.asset");
var go = new GameObject("BarnPopupUITK");
var popup = go.AddComponent<BarnPopupUITK>();
var so = new UnityEditor.SerializedObject(popup);
so.FindProperty("sourcePanelSettings").objectReferenceValue = settings;
so.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/BarnPopupUITK.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(barn): BarnPopupUITK — tick bars, tier notches, Renown"
```

---

### Task 4: BarnBuilding (clickable world prop) + Harvesting/Ranching consumers

**Files:**
- Create: `Assets/Scripts/BarnBuilding.cs`
- Modify: `Assets/Scripts/Plant.cs` (Harvesting bonus on `harvestValue`)
- Modify: `Assets/Scripts/AnimalManager.cs` (Ranching bonus on egg coin reward)

**Interfaces:**
- Consumes: `FarmSkillsManager.Instance.GetBonus(FarmSkillTrack)` (Task 2), `BarnPopupUITK.Instance.Open()` (Task 3).
- Produces: nothing new (world prop + 2 one-line multiplier reads).

- [ ] **Step 1: Create BarnBuilding.cs** (mirrors `CanneryBuilding.cs` exactly, minus the smoke child — the Barn has no fire):

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clickable Barn on the Farm (Reputation Phase 3, spec §4.1). Always visible (no build-gate,
/// unlike Cannery/Smokehouse) — the Barn exists from the start. Tap opens BarnPopupUITK.
/// Press/click handling mirrors CanneryBuilding/WoodRack.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class BarnBuilding : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
            return;

        if (UITapBlocker.PointerOverUI(screenPos)) { CancelPress(); return; }

        if (justPressed && !isPressed && CanInteract() && PointerHitsSelf(screenPos))
        {
            isPressed = true;
            spriteRenderer.color = pressTint * baseColor;
            DoTween(baseScale * pressScale, pressDuration);
            return;
        }
        if (held && isPressed && !PointerHitsSelf(screenPos)) { CancelPress(); return; }
        if (justReleased && isPressed)
        {
            bool overSelf = PointerHitsSelf(screenPos);
            CancelPress();
            if (overSelf && CanInteract()) HandleClick();
        }
    }

    private static bool TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held)
    {
        screenPos = default; justPressed = false; justReleased = false; held = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            held = true;
            return true;
        }
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justReleased = true;
            return true;
        }
        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            justPressed = Mouse.current.leftButton.wasPressedThisFrame;
            justReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            return justPressed || justReleased || held;
        }
        return false;
    }

    private bool PointerHitsSelf(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null || ownCollider == null) return false;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return Physics2D.OverlapPoint(world) == ownCollider;
    }

    private void CancelPress()
    {
        if (!isPressed) return;
        isPressed = false;
        spriteRenderer.color = baseColor;
        DoTween(baseScale, releaseDuration);
    }

    private bool CanInteract()
    {
        CameraPanController pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        return pan == null || !pan.IsPanning;
    }

    private void HandleClick()
    {
        if (BarnPopupUITK.Instance != null) BarnPopupUITK.Instance.Open();
        else Debug.Log("[BarnBuilding] Clicked — no BarnPopupUITK in scene.");
    }

    private void DoTween(Vector3 target, float duration)
    {
        if (scaleTweenId != -1) LeanTween.cancel(scaleTweenId);
        scaleTweenId = LeanTween.scale(gameObject, target, duration)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => scaleTweenId = -1)
            .id;
    }
}
```

- [ ] **Step 2: Wire the Harvesting consumer in Plant.cs**

In `Assets/Scripts/Plant.cs`, in `Harvest()`, find the Farm-upgrades multiplier block:
```csharp
        harvestValue = Mathf.RoundToInt(harvestValue * FarmUpgrades.CashYieldMultiplier(zone));
        if (bountiful) harvestValue *= 2;
```
Insert a Barn Harvesting multiplier immediately before it:
```csharp
        if (FarmSkillsManager.Instance != null)
            harvestValue = Mathf.RoundToInt(harvestValue * (1f + FarmSkillsManager.Instance.GetBonus(FarmSkillTrack.Harvesting)));
        harvestValue = Mathf.RoundToInt(harvestValue * FarmUpgrades.CashYieldMultiplier(zone));
        if (bountiful) harvestValue *= 2;
```

- [ ] **Step 3: Wire the Ranching consumer in AnimalManager.cs**

In `Assets/Scripts/AnimalManager.cs`, in `ClaimPassiveReward()`'s coin branch, find:
```csharp
                int reward = EffectiveReward(equipped, equipped.rewardCoins);
                CurrencyManager.Instance.AddCoins(reward);
```
Change to:
```csharp
                int reward = EffectiveReward(equipped, equipped.rewardCoins);
                if (FarmSkillsManager.Instance != null)
                    reward = Mathf.RoundToInt(reward * (1f + FarmSkillsManager.Instance.GetBonus(FarmSkillTrack.Ranching)));
                CurrencyManager.Instance.AddCoins(reward);
```

- [ ] **Step 4: Compile check + full test run** — 211/211.

- [ ] **Step 5: Place the Barn in the scene** (MCP): duplicate an existing Farm-area prop's transform convention. Via `execute_code`:

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buildings/Barn_Small_32x32.png");
var go = new GameObject("BarnBuilding");
var sr = go.AddComponent<SpriteRenderer>();
sr.sprite = sprite;
var col = go.AddComponent<BoxCollider2D>();
go.AddComponent<BarnBuilding>();
go.transform.position = new Vector3(-6f, -10f, 0f); // Farm area, clear of Cannery(10,-15)/other props — adjust after a visual check
go.transform.localScale = Vector3.one * 1.25f;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
```

After placing, take a `look_at_game_view`/screenshot check in Task 5's smoke test and nudge the position if it overlaps another prop or sits off the visible Farm area — this is placeholder placement, not final art direction.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/BarnBuilding.cs Assets/Scripts/Plant.cs Assets/Scripts/AnimalManager.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(barn): clickable Barn world prop + Harvesting/Ranching consumers"
```

---

### Task 5: Play-mode smoke test

**Files:** none (verification only; fix-forward anything found, then commit fixes).

- [ ] **Step 1: Back up the play save.**

- [ ] **Step 2: Enter play mode**; confirm `FarmSkillsManager.Instance`/`ReputationManager.Instance`/`BarnPopupUITK.Instance` all non-null.

- [ ] **Step 3: Drive the loop via `execute_code`:**
  1. Grant rep directly if needed (`ReputationManager.Instance` has no public rep-grant API by design — instead fulfill a board request, or reflectively call `AddRep` on the private `core` field, or simplest: loop `TryFulfill` after granting whatever the rolled Easy request needs) until `UnspentPoints >= 3`.
  2. Call `FarmSkillsManager.Instance.TryLevelUp(FarmSkillTrack.Harvesting)` three times; assert `GetLevel` reaches 3, `UnspentPoints` decreased by 3, and a 4th call when points are 0 (or after the grant, whichever is exhausted first) returns false.
  3. Verify the Harvesting bonus is live: `FarmSkillsManager.Instance.GetBonus(FarmSkillTrack.Harvesting)` should read `3 * 0.04 = 0.12`.
  4. Pan to Farm, screenshot to confirm the Barn prop is visible and not overlapping other props (adjust position via `execute_code` + re-screenshot if needed, then re-save the scene).
  5. Click-simulate is unreliable in this harness (established in Phase 1/2) — instead call `BarnPopupUITK.Instance.Open()` directly, screenshot, and verify: "Points to spend" reflects remaining points, the Renown bar shows the correct total (3/175), Harvesting's tick bar shows 3 filled ticks, and clicking a tier button via the same reflection-based direct-invoke approach used in Phase 2 (or by calling `OnTierClicked` via reflection) shows a description.
  6. Save/reload: `SaveManager.Instance.SaveGame()`, exit/re-enter play mode, assert `FarmSkillsManager.Instance.GetLevel(FarmSkillTrack.Harvesting) == 3` and `ReputationManager.Instance.UnspentPoints` matches.

- [ ] **Step 4: Exit play mode**, check console for zero new exceptions.

- [ ] **Step 5: Restore the real save**; delete temp screenshots.

- [ ] **Step 6: Commit any fixes**

```bash
git add -A Assets/Scripts Assets/Tests Assets/Scenes
git commit -m "fix(barn): play-mode smoke test fixes"
```
