# Inventory Phase 1: Collect/Sell Toggle + Mega-List Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crops and eggs can be *collected* as counted, saved stacks instead of auto-sold; the Inventory popup becomes an interactive mega-list with bulk-sell rows.

**Architecture:** New `ItemInventoryManager` singleton (PantryManager pattern: int stacks + change events, no item objects) with caps and Coins pricing; a harvest/egg-claim diversion hook mirroring the existing Cannery intake hook; a code-built run-HUD toggle in `RunUI`; sell rows added to the existing `InventoryPopupUITK`. Pure math in EconomyCore with EditMode tests. Spec: `docs/superpowers/specs/2026-07-19-reputation-design.md` (§2 rules, §5 inventory).

**Tech Stack:** Unity C# (singleton managers, JsonUtility saves), UI Toolkit popup, uGUI run HUD, NUnit EditMode tests via the `Temp/run_editmode_tests.request` file bridge.

## Global Constraints

- **Never edit serialized Unity files** (`.unity`, `.prefab`, `.asset`, `.meta`) as text — scene/asset changes go through the gladekit/unity MCP tools.
- **No emoji in UITK text** — invisible on Android (memory rule). Icons are USS background-image classes.
- **Binding economy rules (spec §2):** collect forgoes Money AND the harvest coin trickle (Cannery precedent at `Plant.cs:308-345`); collected items sell for **Coins**, never Money; raw value stays below jar value (keep `rawSellRate` < 2.5, the lowest Cannery jar multiplier).
- `Debug.Log` only for important events; keep LogWarning/LogError.
- EditMode tests: `Assets/Tests/EditMode/`, run via `touch Temp/run_editmode_tests.request`, results in `Temp/editmode_test_results.txt`. Current suite: 154 passing — never finish a task with fewer.
- **Back up the play save before any play-mode session:** copy `gamedata.json` under `%USERPROFILE%\AppData\LocalLow` (dev-save-reset incident rule).
- Commit after each task; end commit messages with the Co-Authored-By + Claude-Session trailer used on this branch.

---

### Task 1: InventoryMath (EconomyCore pure math)

**Files:**
- Create: `Assets/Scripts/EconomyCore/InventoryMath.cs`
- Test: `Assets/Tests/EditMode/InventoryMathTests.cs`

**Interfaces:**
- Consumes: nothing (pure).
- Produces: `static int InventoryMath.RawCropCoinValue(int harvestValue, float rawSellRate)`; `static int InventoryMath.AddToCap(int current, int add, int cap, out int overflow)` → returns the amount actually stored (0..add). Task 2's manager and Task 5's sell rows call both.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

public class InventoryMathTests
{
    [Test]
    public void RawCropCoinValue_ScalesAndRounds()
    {
        Assert.AreEqual(15, InventoryMath.RawCropCoinValue(10, 1.5f));
        Assert.AreEqual(8,  InventoryMath.RawCropCoinValue(5, 1.5f));   // 7.5 rounds to 8
    }

    [Test]
    public void RawCropCoinValue_NeverBelowOne()
    {
        Assert.AreEqual(1, InventoryMath.RawCropCoinValue(1, 0.1f));
        Assert.AreEqual(1, InventoryMath.RawCropCoinValue(0, 1.5f));
    }

    [Test]
    public void AddToCap_StoresWhenUnderCap()
    {
        int stored = InventoryMath.AddToCap(10, 5, 500, out int overflow);
        Assert.AreEqual(5, stored);
        Assert.AreEqual(0, overflow);
    }

    [Test]
    public void AddToCap_SplitsAtCap()
    {
        int stored = InventoryMath.AddToCap(498, 5, 500, out int overflow);
        Assert.AreEqual(2, stored);
        Assert.AreEqual(3, overflow);
    }

    [Test]
    public void AddToCap_FullStackStoresNothing()
    {
        int stored = InventoryMath.AddToCap(500, 3, 500, out int overflow);
        Assert.AreEqual(0, stored);
        Assert.AreEqual(3, overflow);
    }

    [Test]
    public void AddToCap_GuardsNegativeAdd()
    {
        int stored = InventoryMath.AddToCap(10, -5, 500, out int overflow);
        Assert.AreEqual(0, stored);
        Assert.AreEqual(0, overflow);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run (Bash): `touch "Temp/run_editmode_tests.request"` then poll `Temp/editmode_test_results.txt`.
Expected: compile error — `InventoryMath` does not exist. (A compile failure is this bridge's "red".)

- [ ] **Step 3: Write the implementation**

```csharp
using UnityEngine;

/// <summary>
/// Pure math for the item inventory (Reputation Phase 1). Raw crop pricing must stay below the
/// Cannery jar multipliers (2.5+) so processed always beats raw (spec §2).
/// </summary>
public static class InventoryMath
{
    /// <summary>Coins paid for one raw collected crop.</summary>
    public static int RawCropCoinValue(int harvestValue, float rawSellRate)
        => Mathf.Max(1, Mathf.RoundToInt(harvestValue * rawSellRate));

    /// <summary>
    /// Add <paramref name="add"/> onto <paramref name="current"/> under <paramref name="cap"/>.
    /// Returns the amount actually stored; <paramref name="overflow"/> is what didn't fit.
    /// </summary>
    public static int AddToCap(int current, int add, int cap, out int overflow)
    {
        if (add <= 0) { overflow = 0; return 0; }
        int room = Mathf.Max(0, cap - current);
        int stored = Mathf.Min(add, room);
        overflow = add - stored;
        return stored;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `touch "Temp/run_editmode_tests.request"`; read results file.
Expected: 160/160 (154 existing + 6 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/InventoryMath.cs Assets/Tests/EditMode/InventoryMathTests.cs
git commit -m "feat(inventory): InventoryMath raw pricing + cap math"
```
(Also `git add` the two generated `.meta` files after Unity refreshes.)

---

### Task 2: ItemInventoryManager + save wiring

**Files:**
- Create: `Assets/Scripts/Inventory/ItemInventoryManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (fields + constructor + new serializable class)
- Modify: `Assets/Scripts/SaveManager.cs` (capture ~line 158, load ~line 249)
- Modify: `Assets/Scripts/AutoSaveManager.cs` (subscribe ~line 77, unsubscribe ~line 133)
- Test: `Assets/Tests/EditMode/ItemInventorySaveTests.cs`

**Interfaces:**
- Consumes: `InventoryMath.AddToCap`, `InventoryMath.RawCropCoinValue` (Task 1).
- Produces (used by Tasks 3–5): singleton `ItemInventoryManager.Instance`; `bool CollectMode { get; set; }` (persists, fires `event Action<bool> OnCollectModeChanged`); `event Action OnChanged`; `int GetCrop(string cropName)`; `IReadOnlyDictionary<string,int> Crops`; `int AddCrop(string cropName, int amount, out int overflow)` → stored count; `bool TrySpendCrop(string cropName, int amount)`; `int Eggs`; `int AddEggs(int amount, out int overflow)`; `bool TrySpendEggs(int amount)`; `int RawCoinValue(CropData crop)`; `int EggSellCoins`; `int CropCapPerType`; `int EggCap`; `CaptureTo(GameData)`/`LoadFrom(GameData)`. GameData: `ItemStackEntry[] cropStacks`, `int eggStack`, `bool collectModeOn`.

- [ ] **Step 1: Write the failing round-trip test**

Managers don't run Awake in EditMode, so call CaptureTo/LoadFrom directly on a fresh component:

```csharp
using NUnit.Framework;
using UnityEngine;

public class ItemInventorySaveTests
{
    [Test]
    public void CaptureLoad_RoundTripsStacksAndToggle()
    {
        var go = new GameObject("test-inv");
        try
        {
            var inv = go.AddComponent<ItemInventoryManager>();
            inv.AddCrop("Blueberry", 40, out _);
            inv.AddCrop("Tomato", 3, out _);
            inv.AddEggs(7, out _);
            inv.CollectMode = true;

            var data = new GameData();
            inv.CaptureTo(data);

            var inv2 = go.AddComponent<ItemInventoryManager>();
            inv2.LoadFrom(data);

            Assert.AreEqual(40, inv2.GetCrop("Blueberry"));
            Assert.AreEqual(3,  inv2.GetCrop("Tomato"));
            Assert.AreEqual(0,  inv2.GetCrop("Strawberry"));
            Assert.AreEqual(7,  inv2.Eggs);
            Assert.IsTrue(inv2.CollectMode);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void LoadFrom_LegacySaveWithNullStacks_IsEmpty()
    {
        var go = new GameObject("test-inv");
        try
        {
            var inv = go.AddComponent<ItemInventoryManager>();
            inv.AddCrop("Blueberry", 5, out _);
            var legacy = new GameData { cropStacks = null };
            inv.LoadFrom(legacy);
            Assert.AreEqual(0, inv.GetCrop("Blueberry"));
            Assert.AreEqual(0, inv.Eggs);
            Assert.IsFalse(inv.CollectMode);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void AddCrop_RespectsCapAndReportsOverflow()
    {
        var go = new GameObject("test-inv");
        try
        {
            var inv = go.AddComponent<ItemInventoryManager>();
            inv.AddCrop("Blueberry", 499, out _);
            int stored = inv.AddCrop("Blueberry", 5, out int overflow); // default cap 500
            Assert.AreEqual(1, stored);
            Assert.AreEqual(4, overflow);
            Assert.AreEqual(500, inv.GetCrop("Blueberry"));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TrySpend_FailsWhenShort_SucceedsWhenStocked()
    {
        var go = new GameObject("test-inv");
        try
        {
            var inv = go.AddComponent<ItemInventoryManager>();
            inv.AddCrop("Tomato", 10, out _);
            Assert.IsFalse(inv.TrySpendCrop("Tomato", 11));
            Assert.AreEqual(10, inv.GetCrop("Tomato"));
            Assert.IsTrue(inv.TrySpendCrop("Tomato", 10));
            Assert.AreEqual(0, inv.GetCrop("Tomato"));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure** (`ItemInventoryManager`, `GameData.cropStacks` missing).

- [ ] **Step 3: Add GameData fields**

In `Assets/Scripts/GameData.cs`, after the `pantryRawFish`/`pantrySmokedFish` block (line ~56), add:

```csharp
    // Item inventory (Reputation Phase 1). Counted crop stacks keyed by cropName, plus the
    // egg stack and the run-HUD Collect/Sell toggle. Null/empty on legacy saves → empty inventory.
    public ItemStackEntry[] cropStacks;
    public int eggStack;
    public bool collectModeOn;
```

In the default constructor (after `smokehouseSlots = new CannerySlot[0];`):

```csharp
        cropStacks = new ItemStackEntry[0];
```

At file bottom (after `UpgradeLevelEntry`), add:

```csharp
/// <summary>Serializable itemId → count entry (JsonUtility can't serialize Dictionary).</summary>
[Serializable]
public class ItemStackEntry
{
    public string itemId;
    public int count;
}
```

- [ ] **Step 4: Create the manager**

`Assets/Scripts/Inventory/ItemInventoryManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counted stacks of collected farm output (crops by cropName, plus eggs) — the PantryManager
/// pattern: ints + change events, no item objects. Owns the run-HUD Collect/Sell mode.
/// Spec: 2026-07-19-reputation-design.md §5. Caps are raised later by Renown silo addons.
/// </summary>
public class ItemInventoryManager : MonoBehaviour
{
    public static ItemInventoryManager Instance { get; private set; }

    [Header("Tuning")]
    [SerializeField] private int cropCapPerType = 500;
    [SerializeField] private int eggCap = 200;
    [Tooltip("Coins per raw crop = harvestValue × this. Keep BELOW 2.5 (lowest jar multiplier) so processed beats raw.")]
    [SerializeField] private float rawSellRate = 1.5f;
    [SerializeField] private int eggSellCoins = 25;

    private readonly Dictionary<string, int> crops = new Dictionary<string, int>();
    private int eggs;
    private bool collectMode;

    public event Action OnChanged;
    public event Action<bool> OnCollectModeChanged;

    public int CropCapPerType => cropCapPerType;
    public int EggCap => eggCap;
    public int EggSellCoins => eggSellCoins;
    public int Eggs => eggs;
    public IReadOnlyDictionary<string, int> Crops => crops;

    public bool CollectMode
    {
        get => collectMode;
        set
        {
            if (collectMode == value) return;
            collectMode = value;
            OnCollectModeChanged?.Invoke(collectMode);
            OnChanged?.Invoke();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int GetCrop(string cropName)
        => !string.IsNullOrEmpty(cropName) && crops.TryGetValue(cropName, out int n) ? n : 0;

    public int RawCoinValue(CropData crop)
        => crop == null ? 0 : InventoryMath.RawCropCoinValue(crop.harvestValue, rawSellRate);

    public int AddCrop(string cropName, int amount, out int overflow)
    {
        overflow = 0;
        if (string.IsNullOrEmpty(cropName)) return 0;
        int stored = InventoryMath.AddToCap(GetCrop(cropName), amount, cropCapPerType, out overflow);
        if (stored > 0) { crops[cropName] = GetCrop(cropName) + stored; OnChanged?.Invoke(); }
        return stored;
    }

    public bool TrySpendCrop(string cropName, int amount)
    {
        if (amount <= 0 || GetCrop(cropName) < amount) return false;
        crops[cropName] -= amount;
        OnChanged?.Invoke();
        return true;
    }

    public int AddEggs(int amount, out int overflow)
    {
        int stored = InventoryMath.AddToCap(eggs, amount, eggCap, out overflow);
        if (stored > 0) { eggs += stored; OnChanged?.Invoke(); }
        return stored;
    }

    public bool TrySpendEggs(int amount)
    {
        if (amount <= 0 || eggs < amount) return false;
        eggs -= amount;
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureTo(GameData d)
    {
        var list = new List<ItemStackEntry>(crops.Count);
        foreach (var kv in crops)
            if (kv.Value > 0) list.Add(new ItemStackEntry { itemId = kv.Key, count = kv.Value });
        d.cropStacks = list.ToArray();
        d.eggStack = eggs;
        d.collectModeOn = collectMode;
    }

    public void LoadFrom(GameData d)
    {
        crops.Clear();
        if (d.cropStacks != null)
            foreach (var e in d.cropStacks)
                if (e != null && !string.IsNullOrEmpty(e.itemId) && e.count > 0)
                    crops[e.itemId] = Mathf.Min(e.count, cropCapPerType);
        eggs = Mathf.Clamp(d.eggStack, 0, eggCap);
        collectMode = d.collectModeOn;
        OnCollectModeChanged?.Invoke(collectMode);
        OnChanged?.Invoke();
    }
}
```

- [ ] **Step 5: Wire SaveManager**

In `Assets/Scripts/SaveManager.cs`, in the capture block after `if (PantryManager.Instance != null) PantryManager.Instance.CaptureTo(data);` (line ~158) add:

```csharp
        if (ItemInventoryManager.Instance != null) ItemInventoryManager.Instance.CaptureTo(data);
```

In the load block after `PantryManager.Instance.LoadFrom(data);` (line ~249) add:

```csharp
                if (ItemInventoryManager.Instance != null)
                    ItemInventoryManager.Instance.LoadFrom(data);
```

- [ ] **Step 6: Wire AutoSaveManager**

In `Assets/Scripts/AutoSaveManager.cs` `TrySubscribe()` after the `PantryManager` lines (~77-78):

```csharp
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnChanged += OnVoid;
```

And the matching `-= OnVoid;` in the unsubscribe block (~133-134).

- [ ] **Step 7: Add the scene GameObject** (MCP, not file edits)

Using gladekit tools in scene `Assets/Scenes/FarmMain.unity`:
1. `create_game_object` name `ItemInventoryManager` at root.
2. `add_component` → `ItemInventoryManager`.
3. `save_scene`.

- [ ] **Step 8: Run tests** — expect 164/164 (160 + 4 new).

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/Inventory/ItemInventoryManager.cs Assets/Tests/EditMode/ItemInventorySaveTests.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(inventory): ItemInventoryManager stacks + save/autosave wiring"
```
(Include new `.meta` files.)

---

### Task 3: Harvest + egg-claim diversion

**Files:**
- Modify: `Assets/Scripts/Plant.cs:306-345` (Harvest payout block)
- Modify: `Assets/Scripts/AnimalManager.cs:388-395` (coin branch of ClaimPassiveReward)

**Interfaces:**
- Consumes: `ItemInventoryManager.Instance.CollectMode`, `.AddCrop`, `.AddEggs` (Task 2); `FloatingTextManager.ShowText(string, Color, Vector3)` (exists, `FloatingTextManager.cs:120`).
- Produces: the diversion behavior Tasks 4–6 exercise. No new API.

- [ ] **Step 1: Edit Plant.Harvest**

Replace lines 306–316 (`// Cannery intake …` through the money-grant `if` block) with:

```csharp
        // Cannery intake (Pantry Economy §4a): a diverted harvest becomes jar progress
        // instead of cash + banked coins. Stats/refund/regrow below are unaffected.
        bool divertedToCannery = CanneryManager.Instance != null && CanneryManager.Instance.TryIntake(cropData);
        if (divertedToCannery)
            FloatingTextManager.ShowCanneryIntake(transform.position);

        // Collect mode (Reputation Phase 1): the harvest becomes an inventory item instead of
        // cash + banked coins — the same forgone-income rule as the Cannery. A full stack falls
        // back to a normal sale so the harvest is never silently lost.
        bool divertedToInventory = false;
        if (!divertedToCannery && ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.CollectMode)
        {
            divertedToInventory = ItemInventoryManager.Instance.AddCrop(cropData.cropName, 1, out _) > 0;
            if (divertedToInventory)
                FloatingTextManager.ShowText($"+1 {cropData.cropName}", new Color(0.55f, 0.8f, 0.35f), transform.position);
        }
        bool paidOut = !divertedToCannery && !divertedToInventory;

        if (paidOut && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMoney(harvestValue);
            FloatingTextManager.ShowMoney(harvestValue, transform.position);
        }
```

Then in the coin-trickle block (line ~320), change the condition
`if (!divertedToCannery && CurrencyManager.Instance != null && cropData.coinValue > 0)` to
`if (paidOut && CurrencyManager.Instance != null && cropData.coinValue > 0)`.

Then in the zone-stats call (line ~342-345), change both ternaries from `divertedToCannery ? 0 : …` to `paidOut ? … : 0`:

```csharp
        if (RunStats.Instance != null)
            RunStats.Instance.AddZoneHarvest(zone, cropData,
                paidOut ? harvestValue : 0,
                paidOut ? coinGain : 0);
```

- [ ] **Step 2: Edit AnimalManager.ClaimPassiveReward**

Replace the coin branch (lines 388–395) with:

```csharp
        else
        {
            // Collect mode (Reputation Phase 1): bank the egg as an inventory item instead of
            // coins. Full egg stack falls back to the normal coin payout.
            if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.CollectMode
                && ItemInventoryManager.Instance.AddEggs(1, out _) > 0)
            {
                Debug.Log("Claimed egg into inventory (+1 egg)");
                if (visual != null) visual.RemoveEgg();
                FloatingTextManager.ShowText("+1 Egg", new Color(0.55f, 0.8f, 0.35f), rewardWorldPos);
            }
            else
            {
                int reward = EffectiveReward(equipped, equipped.rewardCoins);
                CurrencyManager.Instance.AddCoins(reward);
                Debug.Log($"Claimed egg! +{reward} coins");
                if (visual != null) visual.RemoveEgg();
                FloatingTextManager.ShowCoins(reward, rewardWorldPos);
            }
        }
```

- [ ] **Step 3: Compile check** — gladekit `compile_scripts` (or unity-mcp `refresh_unity` + `read_console`). Expected: zero errors.

- [ ] **Step 4: Run full test suite** — expect 164/164 (no new tests; guards against regressions).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Plant.cs Assets/Scripts/AnimalManager.cs
git commit -m "feat(inventory): collect-mode diversion for harvests and egg claims"
```

---

### Task 4: Collect/Sell toggle in the run HUD

**Files:**
- Modify: `Assets/Scripts/RunUI.cs` (new builder + visibility in `UpdateButtonStates` at line ~105; call builder from `Start` beside `BuildBackToFarmButton()` at line ~40)

**Interfaces:**
- Consumes: `ItemInventoryManager.Instance.CollectMode` setter + `OnCollectModeChanged` (Task 2).
- Produces: none (self-contained UI).

- [ ] **Step 1: Add the builder + handlers**

Add fields and methods to `RunUI` (below `BuildBackToFarmButton`, following its clone-a-button pattern — the toggle clones **Start Run** and reuses Start's slot, which is empty during a run at the Farm since Start hides in-run and Back to Farm only shows away from the Farm):

```csharp
    // ── Collect/Sell toggle (Reputation Phase 1) ──────────────────────────
    private Button collectToggleBtn;
    private TextMeshProUGUI collectToggleLabel;
    private Image collectToggleBg;

    private void BuildCollectToggle()
    {
        if (startRunButton == null) return;
        RectTransform startRT = startRunButton.GetComponent<RectTransform>();

        GameObject clone = Instantiate(startRunButton.gameObject, startRT.parent);
        clone.name = "CollectToggleButton";

        collectToggleBtn = clone.GetComponent<Button>();
        collectToggleBtn.onClick.RemoveAllListeners();
        collectToggleBtn.onClick.AddListener(OnCollectToggleClicked);
        collectToggleBg = clone.GetComponent<Image>();
        collectToggleLabel = clone.GetComponentInChildren<TextMeshProUGUI>(true);

        // Slimmer than the Start CTA, same anchor slot (free during an in-run Farm view).
        RectTransform rt = clone.GetComponent<RectTransform>();
        rt.anchorMin = startRT.anchorMin;
        rt.anchorMax = startRT.anchorMax;
        rt.pivot     = startRT.pivot;
        rt.sizeDelta = new Vector2(startRT.sizeDelta.x * 0.6f, startRT.sizeDelta.y * 0.8f);
        rt.anchoredPosition = startRT.anchoredPosition;

        RefreshCollectToggleVisual();
        clone.SetActive(false);

        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnCollectModeChanged += OnCollectModeChanged;
    }

    private void OnCollectToggleClicked()
    {
        if (ItemInventoryManager.Instance == null) return;
        ItemInventoryManager.Instance.CollectMode = !ItemInventoryManager.Instance.CollectMode;
    }

    private void OnCollectModeChanged(bool _) => RefreshCollectToggleVisual();

    private void RefreshCollectToggleVisual()
    {
        bool collect = ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.CollectMode;
        if (collectToggleLabel != null)
        {
            collectToggleLabel.text = collect ? "Collecting" : "Auto-Sell";
            collectToggleLabel.alignment = TextAlignmentOptions.Center;
        }
        if (collectToggleBg != null)
            collectToggleBg.color = collect
                ? new Color(0.36f, 0.62f, 0.32f)   // green: banking items
                : new Color(0.85f, 0.72f, 0.25f);  // yellow: cash mode
    }
```

- [ ] **Step 2: Call it and wire visibility + cleanup**

In `Start()` after `BuildBackToFarmButton();` add `BuildCollectToggle();`.

In `UpdateButtonStates()` after the `backToFarmButton` line (~125) add:

```csharp
        // Collect/Sell toggle: only meaningful while harvesting, so in-run at the Farm only.
        if (collectToggleBtn != null) collectToggleBtn.gameObject.SetActive(inRun && atFarm);
```

Add an `OnDestroy` (RunUI has none today):

```csharp
    private void OnDestroy()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnCollectModeChanged -= OnCollectModeChanged;
    }
```

- [ ] **Step 3: Compile check** — zero errors expected.

- [ ] **Step 4: Run test suite** — 164/164.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/RunUI.cs
git commit -m "feat(inventory): run-HUD Collect/Sell toggle"
```

---

### Task 5: Inventory mega-list with sell rows

**Files:**
- Modify: `Assets/Scripts/UI/InventoryPopupUITK.cs` (rows, sell panel, subscriptions)
- Modify: `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss` (append sell styles)
- Modify: `Assets/Scripts/Cannery/CanneryManager.cs` (one-line jar-count accessor)
- Create: `Assets/Editor/InventoryDevMenu.cs` (grant/toggle cheats for testing)

**Interfaces:**
- Consumes: `ItemInventoryManager` full API (Task 2); `PantryManager.GetRaw/GetSmoked(tier)`, `FishTiers`; `WoodRackPopupUITK.Instance.CashPricePerWood` + `WoodcuttingMath.SellValue(amount, price)`; `CurrencyManager` add/spend; `RunManager.Instance.IsRunActive`.
- Produces: `CanneryManager.ReadyJarCount` (int property, also usable by Phase 2's board UI).

- [ ] **Step 1: Add the jar-count accessor**

In `CanneryManager` next to `TrySellJar` (line ~240):

```csharp
    public int ReadyJarCount => state.readyJars.Count;
```

- [ ] **Step 2: Rework BuildRows into sections**

In `InventoryPopupUITK`, add a serialized database reference and replace `BuildRows`/`AddRow`/`SyncValues` with sectioned building. Full replacement for the class body below the `Close()` method:

```csharp
    [Header("Data")]
    [SerializeField] private CropDatabase cropDatabase;

    // One expanded sell panel at a time; rebuilt on any change while open.
    private string expandedRowId;

    private void BuildRows()
    {
        if (list == null) return;
        list.Clear();

        var cm = CurrencyManager.Instance;
        var inv = ItemInventoryManager.Instance;
        if (cm == null) return;

        AddSection("Currencies");
        AddPlainRow("inv-icon--coins", "Coins", cm.Coins);
        AddPlainRow("inv-icon--gems", "Gems", cm.Gems);
        AddPlainRow("inv-icon--money", "Cash", cm.Money);

        AddSection("Resources");
        // Wood sells for Cash at the rack's price — in-run only, exactly like the rack.
        int woodPrice = WoodRackPopupUITK.Instance != null ? WoodRackPopupUITK.Instance.CashPricePerWood : 0;
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        AddSellRow("wood", "inv-icon--wood", "Wood", cm.Wood,
            enabled: inRun && woodPrice > 0 && cm.Wood > 0,
            payoutLabel: n => $"+{WoodcuttingMath.SellValue(n, woodPrice)} Cash",
            onSell: n => { if (cm.SpendWood(n)) cm.AddMoney(WoodcuttingMath.SellValue(n, woodPrice)); });
        // Compost has no sell price in v1 — the button ships visible but disabled (user decision).
        AddSellRow("compost", "inv-icon--compost", "Compost", cm.Compost,
            enabled: false, payoutLabel: null, onSell: null);

        if (inv != null)
        {
            AddSection("Harvest");
            if (cropDatabase != null)
                foreach (CropData crop in cropDatabase.allCrops)
                {
                    if (crop == null) continue;
                    int count = inv.GetCrop(crop.cropName);
                    if (count <= 0) continue;
                    int per = inv.RawCoinValue(crop);
                    string id = "crop:" + crop.cropName;
                    AddSellRow(id, "inv-icon--coins", crop.cropName, count,
                        enabled: true,
                        payoutLabel: n => $"+{per * n} Coins",
                        onSell: n => { if (inv.TrySpendCrop(crop.cropName, n)) cm.AddCoins(per * n); });
                }
            if (inv.Eggs > 0)
                AddSellRow("eggs", "inv-icon--coins", "Eggs", inv.Eggs,
                    enabled: true,
                    payoutLabel: n => $"+{inv.EggSellCoins * n} Coins",
                    onSell: n => { if (inv.TrySpendEggs(n)) cm.AddCoins(inv.EggSellCoins * n); });
        }

        var pantry = PantryManager.Instance;
        if (pantry != null && (pantry.TotalRaw > 0 || pantry.TotalSmoked > 0))
        {
            AddSection("Pantry");
            for (int tier = 1; tier <= FishTiers.Count; tier++)
            {
                if (pantry.GetRaw(tier) > 0)
                    AddPlainRow("inv-icon--money", FishTiers.Name(tier), pantry.GetRaw(tier));
                if (pantry.GetSmoked(tier) > 0)
                    AddPlainRow("inv-icon--money", FishTiers.SmokedName(tier), pantry.GetSmoked(tier));
            }
        }
        if (CanneryManager.Instance != null && CanneryManager.Instance.ReadyJarCount > 0)
        {
            if (pantry == null || (pantry.TotalRaw == 0 && pantry.TotalSmoked == 0)) AddSection("Pantry");
            AddPlainRow("inv-icon--money", "Jars", CanneryManager.Instance.ReadyJarCount);
        }
    }

    private void AddSection(string title)
    {
        var lbl = new Label(title);
        lbl.AddToClassList("inv-section");
        list.Add(lbl);
    }

    private void AddPlainRow(string iconClass, string name, int value)
    {
        var row = new VisualElement(); row.AddToClassList("inv-row");
        var icon = new VisualElement(); icon.AddToClassList("inv-icon"); icon.AddToClassList(iconClass);
        icon.pickingMode = PickingMode.Ignore;
        var nameLbl = new Label(name); nameLbl.AddToClassList("inv-name");
        var valLbl = new Label(value.ToString("N0")); valLbl.AddToClassList("inv-value");
        row.Add(icon); row.Add(nameLbl); row.Add(valLbl);
        list.Add(row);
    }

    private void AddSellRow(string rowId, string iconClass, string name, int count,
        bool enabled, System.Func<int, string> payoutLabel, System.Action<int> onSell)
    {
        var row = new VisualElement(); row.AddToClassList("inv-row");
        var icon = new VisualElement(); icon.AddToClassList("inv-icon"); icon.AddToClassList(iconClass);
        icon.pickingMode = PickingMode.Ignore;
        var nameLbl = new Label(name); nameLbl.AddToClassList("inv-name");
        var valLbl = new Label(count.ToString("N0")); valLbl.AddToClassList("inv-value");
        var sellBtn = new Button(() =>
        {
            expandedRowId = expandedRowId == rowId ? null : rowId;
            BuildRows();
        }) { text = "Sell" };
        sellBtn.AddToClassList("inv-sell-btn");
        sellBtn.SetEnabled(enabled);
        row.Add(icon); row.Add(nameLbl); row.Add(valLbl); row.Add(sellBtn);
        list.Add(row);

        if (expandedRowId != rowId || !enabled || onSell == null) return;

        // Inline sell panel: slider + quick amounts + confirm (spec §5.3).
        var panel = new VisualElement(); panel.AddToClassList("inv-sell-panel");
        var slider = new SliderInt(1, Mathf.Max(1, count)) { value = Mathf.Max(1, count / 2) };
        slider.AddToClassList("inv-sell-slider");
        var quickRow = new VisualElement(); quickRow.AddToClassList("inv-quick-row");
        var confirm = new Button();
        confirm.AddToClassList("inv-confirm-btn");

        void SyncConfirm()
        {
            int n = Mathf.Clamp(slider.value, 1, count);
            confirm.text = payoutLabel != null ? $"Sell {n}  ({payoutLabel(n)})" : $"Sell {n}";
        }
        slider.RegisterValueChangedCallback(_ => SyncConfirm());

        void AddQuick(string label, System.Func<int> pick)
        {
            var b = new Button(() => { slider.value = Mathf.Clamp(pick(), 1, count); SyncConfirm(); }) { text = label };
            b.AddToClassList("inv-quick-btn");
            quickRow.Add(b);
        }
        AddQuick("10", () => 10);
        AddQuick("Half", () => count / 2);
        AddQuick("Max", () => count);

        confirm.clicked += () =>
        {
            int n = Mathf.Clamp(slider.value, 1, count);
            onSell(n);
            expandedRowId = null;
            // OnChanged fires from the spend and rebuilds; this is a fallback for wood (Currency events).
            BuildRows();
        };

        SyncConfirm();
        panel.Add(slider); panel.Add(quickRow); panel.Add(confirm);
        list.Add(panel);
    }

    private void SyncValues() { if (isOpen) BuildRows(); }
```

Notes for the implementer: keep the existing `Open`/`Close`/`OnAnyChanged` methods; `OnAnyChanged` already calls `SyncValues()`, which now rebuilds. Delete the old `coinsVal…compostVal` label fields and the old `AddRow`. API references are verified: `CurrencyManager.SpendWood(int)` (`CurrencyManager.cs:303`), `FishTiers.Name(int)` and `FishTiers.SmokedName(int)` (`FishTiers.cs:15-18`).

- [ ] **Step 3: Subscribe item/pantry events**

In `TrySubscribeEvents()` add (with matching unsubscribes in `UnsubscribeEvents()`):

```csharp
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnChanged += OnInvChanged;
        if (PantryManager.Instance != null)
            PantryManager.Instance.OnChanged += OnInvChanged;
```

And the handler: `private void OnInvChanged() { if (isOpen) BuildRows(); }`

- [ ] **Step 4: Append USS styles**

Append to `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss`:

```css
.inv-section {
    -unity-font-style: bold;
    font-size: 26px;
    color: rgb(92, 70, 40);
    margin-top: 14px;
    margin-bottom: 4px;
}

.inv-sell-btn {
    width: 90px;
    height: 44px;
    margin-left: 10px;
    background-color: rgb(122, 92, 55);
    color: rgb(245, 235, 215);
    border-radius: 8px;
    border-width: 0;
}

.inv-sell-btn:disabled {
    opacity: 0.35;
}

.inv-sell-panel {
    background-color: rgba(0, 0, 0, 0.08);
    border-radius: 8px;
    padding: 10px;
    margin-bottom: 6px;
}

.inv-quick-row {
    flex-direction: row;
    justify-content: space-between;
    margin-top: 6px;
}

.inv-quick-btn {
    flex-grow: 1;
    height: 40px;
    margin-right: 6px;
    background-color: rgb(160, 132, 92);
    color: rgb(245, 235, 215);
    border-radius: 8px;
    border-width: 0;
}

.inv-confirm-btn {
    height: 48px;
    margin-top: 8px;
    background-color: rgb(96, 128, 66);
    color: rgb(245, 235, 215);
    border-radius: 8px;
    border-width: 0;
    -unity-font-style: bold;
}
```

- [ ] **Step 5: Wire the CropDatabase reference** (MCP): gladekit `find_asset` for `CropDatabase`, then `set_object_reference` on the scene's `InventoryPopupUITK` component `cropDatabase` field. `save_scene`.

- [ ] **Step 6: Dev menu for testing**

`Assets/Editor/InventoryDevMenu.cs`:

```csharp
using UnityEditor;
using UnityEngine;

public static class InventoryDevMenu
{
    [MenuItem("Tools/Inventory/Grant 50 Of First 3 Crops")]
    private static void GrantCrops()
    {
        var inv = ItemInventoryManager.Instance;
        if (inv == null) { Debug.LogWarning("[InvDev] Needs play mode + manager."); return; }
        string[] names = { "Strawberry", "Blueberry", "Tomato" };
        foreach (string n in names) { inv.AddCrop(n, 50, out _); }
        Debug.Log("[InvDev] Granted 50x Strawberry/Blueberry/Tomato");
    }

    [MenuItem("Tools/Inventory/Grant 20 Eggs")]
    private static void GrantEggs()
    {
        if (ItemInventoryManager.Instance == null) { Debug.LogWarning("[InvDev] Needs play mode."); return; }
        ItemInventoryManager.Instance.AddEggs(20, out _);
        Debug.Log("[InvDev] Granted 20 eggs");
    }

    [MenuItem("Tools/Inventory/Toggle Collect Mode")]
    private static void ToggleCollect()
    {
        var inv = ItemInventoryManager.Instance;
        if (inv == null) { Debug.LogWarning("[InvDev] Needs play mode."); return; }
        inv.CollectMode = !inv.CollectMode;
        Debug.Log($"[InvDev] CollectMode = {inv.CollectMode}");
    }
}
```


- [ ] **Step 7: Compile check + full test run** — zero errors, 164/164.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/UI/InventoryPopupUITK.cs Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss Assets/Scripts/Cannery/CanneryManager.cs Assets/Editor/InventoryDevMenu.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(inventory): mega-list sell rows + collect dev menu"
```

---

### Task 6: Play-mode smoke test

**Files:** none (verification only; fix-forward anything found, then commit fixes).

- [ ] **Step 1: Back up the play save.** Run unity-mcp `execute_code` → `Debug.Log(Application.persistentDataPath)`, then PowerShell: `Copy-Item "<that path>\gamedata.json" "<that path>\gamedata.backup-2026-07-19.json"` (skip silently if no save exists).

- [ ] **Step 2: Enter play mode** via Bash: `touch "Temp/enter_play_mode.request"` (or gladekit play-mode tool).

- [ ] **Step 3: Drive the loop.** Via `execute_menu_item` / `execute_code`:
  1. Tools ▸ Inventory ▸ Toggle Collect Mode (expect log `CollectMode = True`).
  2. Start a run (`RunManager.Instance.StartRun()` via execute_code if no run active), wait for a harvest OR call `ItemInventoryManager.Instance.AddCrop("Blueberry", 40, out _)` to simulate.
  3. Assert via execute_code: `Debug.Log(ItemInventoryManager.Instance.GetCrop("Blueberry"))` → 40; `CurrencyManager` money unchanged by the adds.
  4. Open the inventory popup (`InventoryPopupUITK.Instance.Open()`); screenshot via `ScreenCapture.CaptureScreenshot` (execute_code — the UI overlay isn't visible to look_at_game_view).
  5. Sell 10 Blueberries through code path: verify Coins increased by `10 × RawCoinValue` and stack shows 30.
  6. Save/reload check: `SaveManager.Instance.SaveGame()`, exit play, re-enter, assert `GetCrop("Blueberry") == 30` and `CollectMode == true`.

- [ ] **Step 4: Exit play mode** (`touch "Temp/exit_play_mode.request"`). Zero exceptions in console expected.

- [ ] **Step 5: Verify visually.** Read the screenshot; check sections/sell button/disabled compost row render correctly. Fix and re-verify anything broken.

- [ ] **Step 6: Commit any fixes**

```bash
git add -A Assets/Scripts Assets/UI
git commit -m "fix(inventory): play-mode smoke test fixes"
```
