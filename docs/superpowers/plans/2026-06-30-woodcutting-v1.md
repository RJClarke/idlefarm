# Woodcutting v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a bottom-right "Woods" area where the player chops trees for Wood during run downtime, then sells Wood for Cash (in-run only) or Gold (anytime), with an Axe upgraded at the Carpenter that unlocks harder trees and speeds chopping.

**Architecture:** All decision logic lives in a pure static `WoodcuttingMath` class (fully unit-tested, no Unity types). Wood is a new currency on the existing `CurrencyManager` (mirrors Compost). Axe level + config live on a new `WoodcuttingManager` singleton. Trees are world `TreeNode` MonoBehaviours reached via a new `CameraPanController.Location.Woods`. Selling is a UITK panel opened from a world "wood rack." Axe upgrades are a new "Tools" row in the existing `CarpenterPopupUITK`. Persistence piggybacks on `GameData` + `SaveManager` exactly like Compost.

**Tech Stack:** Unity 6000.x, C#, UI Toolkit (UITK), Unity Input System (new), NUnit EditMode tests, LeanTween, JsonUtility save.

## Global Constraints

- Unity 2D mobile, portrait 1080x1920, 32 PPU. World sprites participate in YSort.
- Input: **new Input System only** (`Keyboard.current`/`Mouse.current`/`Touchscreen.current`) — never legacy `Input`.
- Money exists **only during an active run**; Coins/Wood are permanent. `RunManager.Instance.IsRunActive` is the run-state source of truth.
- Wood is a **single resource** in v1; tree variety affects yield/speed only. No stump-chipping. Active-only (no auto-chop). Regrow speed is **not** an axe effect.
- Pure logic → `WoodcuttingMath` with EditMode tests. MonoBehaviour/UITK integration is verified manually in the editor (matches existing codebase practice; UI is not unit-tested here).
- Persistence pattern: add a field to `GameData`, assign it post-construction in `SaveManager.SaveGame`, restore it in `SaveManager.LoadGame` — mirroring `compost`.
- New scripts go under `Assets/Scripts/Woodcutting/`. Tests go under `Assets/Tests/EditMode/`.

---

### Task 1: WoodcuttingMath (pure logic + tests)

The whole decision layer, no Unity dependencies. This is the only fully TDD task; every later task consumes these functions.

**Files:**
- Create: `Assets/Scripts/Woodcutting/WoodcuttingMath.cs`
- Test: `Assets/Tests/EditMode/WoodcuttingMathTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum WoodcuttingMath.StackMode { One, Ten, All }`
  - `int WoodcuttingMath.EffectiveHitsToFell(int baseHits, int axeLevel, int reductionPerLevel, int minHits = 1)`
  - `bool WoodcuttingMath.CanFell(int requiredAxeLevel, int axeLevel)`
  - `float WoodcuttingMath.RegrowFraction(double elapsedSeconds, float regrowSeconds)`
  - `bool WoodcuttingMath.IsRegrown(double elapsedSeconds, float regrowSeconds)`
  - `int WoodcuttingMath.ResolveStackAmount(StackMode mode, int available)`
  - `int WoodcuttingMath.SellValue(int amount, int pricePerUnit)`
  - `bool WoodcuttingMath.CanUpgradeAxe(int axeLevel, int maxLevel, int coins, int coinCost, int wood, int woodCost)`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/WoodcuttingMathTests.cs`:

```csharp
using NUnit.Framework;
using StackMode = WoodcuttingMath.StackMode;

public class WoodcuttingMathTests
{
    [Test]
    public void EffectiveHitsToFell_ReducesPerAxeLevel_ClampedToMin()
    {
        // base 5 hits, no axe → 5
        Assert.AreEqual(5, WoodcuttingMath.EffectiveHitsToFell(5, 0, 1));
        // axe level 2, reduction 1/level → 3
        Assert.AreEqual(3, WoodcuttingMath.EffectiveHitsToFell(5, 2, 1));
        // never drops below minHits (default 1)
        Assert.AreEqual(1, WoodcuttingMath.EffectiveHitsToFell(5, 99, 1));
        // explicit minHits respected
        Assert.AreEqual(2, WoodcuttingMath.EffectiveHitsToFell(5, 99, 1, 2));
    }

    [Test]
    public void CanFell_RequiresAxeLevelAtOrAboveTreeRequirement()
    {
        Assert.IsTrue(WoodcuttingMath.CanFell(0, 0));   // softwood, bare hands
        Assert.IsFalse(WoodcuttingMath.CanFell(1, 0));  // hardwood, no axe
        Assert.IsTrue(WoodcuttingMath.CanFell(1, 1));   // hardwood, axe lvl 1
    }

    [Test]
    public void RegrowFraction_ZeroToOne_Clamped()
    {
        Assert.AreEqual(0f, WoodcuttingMath.RegrowFraction(0, 10f), 1e-4f);
        Assert.AreEqual(0.5f, WoodcuttingMath.RegrowFraction(5, 10f), 1e-4f);
        Assert.AreEqual(1f, WoodcuttingMath.RegrowFraction(20, 10f), 1e-4f);
    }

    [Test]
    public void RegrowFraction_ZeroDuration_IsImmediatelyFull()
    {
        Assert.AreEqual(1f, WoodcuttingMath.RegrowFraction(0, 0f), 1e-4f);
    }

    [Test]
    public void IsRegrown_TrueAtOrPastDuration()
    {
        Assert.IsFalse(WoodcuttingMath.IsRegrown(9.9, 10f));
        Assert.IsTrue(WoodcuttingMath.IsRegrown(10, 10f));
        Assert.IsTrue(WoodcuttingMath.IsRegrown(50, 10f));
    }

    [Test]
    public void ResolveStackAmount_ClampsToAvailable()
    {
        Assert.AreEqual(1, WoodcuttingMath.ResolveStackAmount(StackMode.One, 50));
        Assert.AreEqual(10, WoodcuttingMath.ResolveStackAmount(StackMode.Ten, 50));
        Assert.AreEqual(5, WoodcuttingMath.ResolveStackAmount(StackMode.Ten, 5)); // fewer than 10
        Assert.AreEqual(50, WoodcuttingMath.ResolveStackAmount(StackMode.All, 50));
        Assert.AreEqual(0, WoodcuttingMath.ResolveStackAmount(StackMode.All, 0));
    }

    [Test]
    public void SellValue_MultipliesAmountByPrice_NeverNegative()
    {
        Assert.AreEqual(0, WoodcuttingMath.SellValue(0, 5));
        Assert.AreEqual(50, WoodcuttingMath.SellValue(10, 5));
        Assert.AreEqual(0, WoodcuttingMath.SellValue(-3, 5));
    }

    [Test]
    public void CanUpgradeAxe_RequiresUnderMaxAndAffordBoth()
    {
        // under max, can afford both → true
        Assert.IsTrue(WoodcuttingMath.CanUpgradeAxe(0, 3, coins: 100, coinCost: 100, wood: 50, woodCost: 50));
        // at max → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(3, 3, 1000, 100, 1000, 50));
        // not enough coins → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(0, 3, 99, 100, 1000, 50));
        // not enough wood → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(0, 3, 1000, 100, 49, 50));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Test Runner (EditMode) or MCP `run_tests` filtering `WoodcuttingMathTests`.
Expected: FAIL — `WoodcuttingMath` does not exist / does not compile.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Woodcutting/WoodcuttingMath.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Pure decision logic for the Woodcutting system. No Unity object dependencies so it is
/// fully unit-testable. All MonoBehaviours (TreeNode, WoodcuttingManager, sell UI) route
/// their math through here.
/// </summary>
public static class WoodcuttingMath
{
    public enum StackMode { One, Ten, All }

    /// <summary>Taps needed to fell a tree, reduced by axe level, never below minHits.</summary>
    public static int EffectiveHitsToFell(int baseHits, int axeLevel, int reductionPerLevel, int minHits = 1)
    {
        int reduced = baseHits - Mathf.Max(0, axeLevel) * Mathf.Max(0, reductionPerLevel);
        return Mathf.Max(minHits, reduced);
    }

    /// <summary>True if the current axe can fell a tree with the given axe-level requirement.</summary>
    public static bool CanFell(int requiredAxeLevel, int axeLevel) => axeLevel >= requiredAxeLevel;

    /// <summary>0..1 regrowth progress. A zero (or negative) duration is treated as instantly full.</summary>
    public static float RegrowFraction(double elapsedSeconds, float regrowSeconds)
    {
        if (regrowSeconds <= 0f) return 1f;
        return Mathf.Clamp01((float)(elapsedSeconds / regrowSeconds));
    }

    public static bool IsRegrown(double elapsedSeconds, float regrowSeconds) => RegrowFraction(elapsedSeconds, regrowSeconds) >= 1f;

    /// <summary>How many units a stack button sells, clamped to what the player owns.</summary>
    public static int ResolveStackAmount(StackMode mode, int available)
    {
        if (available <= 0) return 0;
        switch (mode)
        {
            case StackMode.One: return Mathf.Min(1, available);
            case StackMode.Ten: return Mathf.Min(10, available);
            default: return available; // All
        }
    }

    /// <summary>Total proceeds for selling `amount` units at `pricePerUnit`. Never negative.</summary>
    public static int SellValue(int amount, int pricePerUnit) => Mathf.Max(0, amount) * Mathf.Max(0, pricePerUnit);

    /// <summary>Whether an axe upgrade is allowed: under max level and both currencies affordable.</summary>
    public static bool CanUpgradeAxe(int axeLevel, int maxLevel, int coins, int coinCost, int wood, int woodCost)
    {
        if (axeLevel >= maxLevel) return false;
        return coins >= coinCost && wood >= woodCost;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run `WoodcuttingMathTests`. Expected: all 8 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Woodcutting/WoodcuttingMath.cs" "Assets/Tests/EditMode/WoodcuttingMathTests.cs"
git commit -m "feat(woodcutting): WoodcuttingMath pure logic + tests"
```

---

### Task 2: Wood currency + persistence

Add Wood to `CurrencyManager` (mirror of Compost) and persist it through `GameData`/`SaveManager`.

**Files:**
- Modify: `Assets/Scripts/CurrencyManager.cs` (add a Wood region near the Compost region ~line 252-278)
- Modify: `Assets/Scripts/GameData.cs` (add field + default)
- Modify: `Assets/Scripts/SaveManager.cs` (save ~after line 131 block, load ~after line 177)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `int CurrencyManager.Wood { get; }`
  - `event Action<int> CurrencyManager.OnWoodChanged`
  - `void CurrencyManager.AddWood(int amount)`
  - `bool CurrencyManager.SpendWood(int amount)`
  - `bool CurrencyManager.CanAffordWood(int amount)`
  - `void CurrencyManager.SetWood(int amount)`
  - `int GameData.wood`

- [ ] **Step 1: Add the Wood field/event/property to CurrencyManager**

In `CurrencyManager.cs`, next to the compost field (line 19) add:

```csharp
    [SerializeField] private int currentWood = 0; // Woodcutting resource
```

Next to `OnCompostChanged` (line 29):

```csharp
    public event Action<int> OnWoodChanged;
```

Next to `Compost` property (line 35):

```csharp
    public int Wood => currentWood;
```

- [ ] **Step 2: Add the Wood management region**

After the Compost `#endregion` (line 278) add:

```csharp
    #region Wood (Woodcutting resource)

    public void AddWood(int amount)
    {
        if (amount <= 0) return;
        currentWood += amount;
        OnWoodChanged?.Invoke(currentWood);
    }

    public bool SpendWood(int amount)
    {
        if (amount <= 0) return true;
        if (currentWood < amount) return false;
        currentWood -= amount;
        OnWoodChanged?.Invoke(currentWood);
        return true;
    }

    public bool CanAffordWood(int amount) => currentWood >= amount;

    public void SetWood(int amount)
    {
        currentWood = Mathf.Max(0, amount);
        OnWoodChanged?.Invoke(currentWood);
    }

    #endregion
```

- [ ] **Step 3: Add the GameData field**

In `GameData.cs`, after `public int compost;` (line 12) add:

```csharp
    public int wood;
```

In the default constructor after `compost = 0;` (line 77) add:

```csharp
        wood = 0;
```

- [ ] **Step 4: Wire SaveManager save + load**

In `SaveManager.cs` save method, after the run-snapshot block (near line 131) add:

```csharp
        data.wood = CurrencyManager.Instance != null ? CurrencyManager.Instance.Wood : 0;
```

In the load method, right after `CurrencyManager.Instance.SetCompost(data.compost);` (line 177) add:

```csharp
                CurrencyManager.Instance.SetWood(data.wood);
```

- [ ] **Step 5: Verify compilation**

Via MCP `read_console` (or Unity console): confirm no compile errors after domain reload.

- [ ] **Step 6: Manual verification**

In Play mode: `CurrencyManager.Instance.AddWood(25)` (temporary context menu or debug call) → save → stop → play → confirm Wood loads as 25.
Expected: Wood persists across a save/load cycle.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/CurrencyManager.cs" "Assets/Scripts/GameData.cs" "Assets/Scripts/SaveManager.cs"
git commit -m "feat(woodcutting): Wood currency on CurrencyManager + save/load persistence"
```

---

### Task 3: WoodcuttingManager (axe level + config) + persistence

Owns the axe level, its tuning config, and the upgrade transaction. Persists axe level like Wood.

**Files:**
- Create: `Assets/Scripts/Woodcutting/WoodcuttingManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (add `axeLevel` field + default)
- Modify: `Assets/Scripts/SaveManager.cs` (save + load `axeLevel`)

**Interfaces:**
- Consumes: `WoodcuttingMath.CanUpgradeAxe`, `WoodcuttingMath.EffectiveHitsToFell`, `WoodcuttingMath.CanFell`; `CurrencyManager` Wood/Coins spend + affordability.
- Produces:
  - `WoodcuttingManager.Instance`
  - `int WoodcuttingManager.AxeLevel { get; }`
  - `int WoodcuttingManager.MaxAxeLevel { get; }`
  - `int WoodcuttingManager.HitsReductionPerLevel { get; }`
  - `event Action<int> WoodcuttingManager.OnAxeLevelChanged`
  - `int WoodcuttingManager.NextUpgradeCoinCost()` / `int NextUpgradeWoodCost()`
  - `bool WoodcuttingManager.CanUpgradeAxe()`
  - `bool WoodcuttingManager.TryUpgradeAxe()`
  - `void WoodcuttingManager.SetAxeLevel(int level)` (load)
  - `int GameData.axeLevel`

- [ ] **Step 1: Create WoodcuttingManager**

Create `Assets/Scripts/Woodcutting/WoodcuttingManager.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// Owns woodcutting meta-state: the axe level, its tuning, and the upgrade transaction.
/// Wood the resource lives on CurrencyManager; this is the domain state around it.
/// Axe level persists via GameData.axeLevel (SaveManager).
/// </summary>
public class WoodcuttingManager : MonoBehaviour
{
    public static WoodcuttingManager Instance { get; private set; }

    [Header("Axe Tuning")]
    [SerializeField] private int maxAxeLevel = 3;
    [SerializeField] private int hitsReductionPerLevel = 1;
    [Tooltip("Coin cost per next axe level, indexed by current level (0 -> level 1, ...).")]
    [SerializeField] private int[] axeCoinCosts = { 250, 750, 2000 };
    [Tooltip("Wood cost per next axe level, indexed by current level.")]
    [SerializeField] private int[] axeWoodCosts = { 20, 60, 150 };

    private int axeLevel;

    public int AxeLevel => axeLevel;
    public int MaxAxeLevel => maxAxeLevel;
    public int HitsReductionPerLevel => hitsReductionPerLevel;
    public event Action<int> OnAxeLevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int NextUpgradeCoinCost()
    {
        if (axeLevel >= maxAxeLevel || axeCoinCosts.Length == 0) return int.MaxValue;
        return axeCoinCosts[Mathf.Min(axeLevel, axeCoinCosts.Length - 1)];
    }

    public int NextUpgradeWoodCost()
    {
        if (axeLevel >= maxAxeLevel || axeWoodCosts.Length == 0) return int.MaxValue;
        return axeWoodCosts[Mathf.Min(axeLevel, axeWoodCosts.Length - 1)];
    }

    public bool CanUpgradeAxe()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return false;
        return WoodcuttingMath.CanUpgradeAxe(axeLevel, maxAxeLevel, cm.Coins, NextUpgradeCoinCost(), cm.Wood, NextUpgradeWoodCost());
    }

    /// <summary>Spends Coins + Wood and raises the axe level by one. Returns false if not allowed.</summary>
    public bool TryUpgradeAxe()
    {
        if (!CanUpgradeAxe()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextUpgradeCoinCost();
        int woodCost = NextUpgradeWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund coins if wood spend fails
        axeLevel++;
        OnAxeLevelChanged?.Invoke(axeLevel);
        return true;
    }

    public void SetAxeLevel(int level)
    {
        axeLevel = Mathf.Clamp(level, 0, maxAxeLevel);
        OnAxeLevelChanged?.Invoke(axeLevel);
    }
}
```

- [ ] **Step 2: Add GameData.axeLevel**

In `GameData.cs` after `public int wood;` add:

```csharp
    public int axeLevel;
```

In the default constructor after `wood = 0;` add:

```csharp
        axeLevel = 0;
```

- [ ] **Step 3: Wire SaveManager**

In `SaveManager.cs` save method, after the `data.wood = ...` line add:

```csharp
        data.axeLevel = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.AxeLevel : 0;
```

In load, after `CurrencyManager.Instance.SetWood(data.wood);` add:

```csharp
                if (WoodcuttingManager.Instance != null) WoodcuttingManager.Instance.SetAxeLevel(data.axeLevel);
```

- [ ] **Step 4: Verify compilation**

MCP `read_console`: no errors after reload.

- [ ] **Step 5: Create the manager GameObject**

In `SampleScene`, create an empty GameObject `WoodcuttingManager`, add the `WoodcuttingManager` component, and place it near the other managers. Save the scene.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Woodcutting/WoodcuttingManager.cs" "Assets/Scripts/GameData.cs" "Assets/Scripts/SaveManager.cs" "Assets/Scenes/SampleScene.unity"
git commit -m "feat(woodcutting): WoodcuttingManager axe state + upgrade transaction + persistence"
```

---

### Task 4: WoodTreeData ScriptableObject + tree assets

Data definition for a tree type, plus two concrete assets (softwood + hardwood).

**Files:**
- Create: `Assets/Scripts/Woodcutting/WoodTreeData.cs`
- Create asset: `Assets/Data/Woodcutting/Softwood.asset`
- Create asset: `Assets/Data/Woodcutting/Hardwood.asset`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `WoodTreeData` fields: `string treeName`, `int baseHitsToFell`, `int woodYield`, `float regrowSeconds`, `int requiredAxeLevel`, `Sprite standingSprite`, `Sprite stumpSprite`.

- [ ] **Step 1: Create the ScriptableObject**

Create `Assets/Scripts/Woodcutting/WoodTreeData.cs`:

```csharp
using UnityEngine;

/// <summary>Defines one tree type the player can chop in the Woods.</summary>
[CreateAssetMenu(fileName = "WoodTreeData", menuName = "IdleFarm/Wood Tree Data")]
public class WoodTreeData : ScriptableObject
{
    [Header("Identity")]
    public string treeName = "Softwood";

    [Header("Chopping")]
    [Tooltip("Base taps to fell before axe reduction.")]
    public int baseHitsToFell = 5;
    [Tooltip("Wood awarded when the tree falls.")]
    public int woodYield = 50;
    [Tooltip("Seconds for a stump to regrow into a full tree.")]
    public float regrowSeconds = 45f;
    [Tooltip("Minimum axe level required to fell this tree (0 = bare hands).")]
    public int requiredAxeLevel = 0;

    [Header("Visuals")]
    public Sprite standingSprite;
    public Sprite stumpSprite;
}
```

- [ ] **Step 2: Verify compilation, then create assets**

After compile, create the folder `Assets/Data/Woodcutting/` and two assets via MCP `manage_scriptable_object` (or Assets > Create > IdleFarm > Wood Tree Data):
- `Softwood.asset`: treeName "Softwood", baseHitsToFell 5, woodYield 50, regrowSeconds 45, requiredAxeLevel 0.
- `Hardwood.asset`: treeName "Hardwood", baseHitsToFell 9, woodYield 110, regrowSeconds 90, requiredAxeLevel 1.

Assign `standingSprite`/`stumpSprite` if tree art exists; otherwise leave null for now (Task 6 uses a placeholder). See the art dependency in the spec §10.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Woodcutting/WoodTreeData.cs" "Assets/Data/Woodcutting"
git commit -m "feat(woodcutting): WoodTreeData SO + Softwood/Hardwood assets"
```

---

### Task 5: Camera "Woods" location + nav

Add `Woods` to the camera location enum so the world can pan bottom-right to the forest, and add a convenience pan method + a nav button.

**Files:**
- Modify: `Assets/Scripts/CameraPanController.cs` (enum ~line 11, default offsets ~line 26-31, convenience methods ~line 92-94)
- Modify: `Assets/Scenes/SampleScene.unity` (nav button + camera offset — done in editor)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `CameraPanController.Location.Woods`
  - `void CameraPanController.PanToWoods()`

- [ ] **Step 1: Add the enum value**

In `CameraPanController.cs` change (line 11):

```csharp
    public enum Location { Farm, Greenhouse, Market, Woods }
```

- [ ] **Step 2: Add a default offset entry**

In the `locations` initializer (after the Market entry, ~line 30) add:

```csharp
        new LocationOffset { location = Location.Woods, offset = new Vector2(12f, -8f) },
```

(Placeholder offset — bottom-right of the farm. Fine-tune in the inspector during scene assembly, Task 9.)

- [ ] **Step 3: Add the convenience method**

After `PanToMarket()` (line 94) add:

```csharp
    public void PanToWoods() => PanTo(Location.Woods);
```

- [ ] **Step 4: Verify compilation**

MCP `read_console`: no errors. Note `LocationModeController` already treats any non-Market location as "not at market," so no change needed there.

- [ ] **Step 5: Add the nav button (editor)**

In `SampleScene`, duplicate an existing world/HUD nav button (e.g. the Market nav button), rename to `WoodsNavButton`, set its icon to `Assets/Sprites/UI/Icons/Cute/RpgResources/Axe.png`, and wire its click to `CameraPanController.PanToWoods()`. Position it bottom-right. Add a "back to farm" affordance consistent with how Market/Greenhouse return (reuse existing pattern). Save the scene.

- [ ] **Step 6: Manual verification**

Play → tap the Woods nav button → camera pans bottom-right → tap back → returns to Farm.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/CameraPanController.cs" "Assets/Scenes/SampleScene.unity"
git commit -m "feat(woodcutting): add Woods camera location + nav"
```

---

### Task 6: TreeNode (chop → fell → award → regrow)

The interactive world tree. Tap to chop; fells after effective hits; awards Wood; shows stump; regrows on a timer using UtcNow so offline time counts.

**Files:**
- Create: `Assets/Scripts/Woodcutting/TreeNode.cs`

**Interfaces:**
- Consumes: `WoodTreeData`; `WoodcuttingManager.Instance` (AxeLevel, HitsReductionPerLevel); `WoodcuttingMath.EffectiveHitsToFell/CanFell/IsRegrown`; `CurrencyManager.Instance.AddWood`.
- Produces: `TreeNode` MonoBehaviour (scene-placed; no static API).

- [ ] **Step 1: Create TreeNode**

Create `Assets/Scripts/Woodcutting/TreeNode.cs`. Mirror the pointer-reading approach used in `ShopBuilding.cs` (`TryReadPointer` / `PointerHitsSelf`) — copy those helpers so input matches the rest of the game:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A choppable tree in the Woods. Tap to chop; after the effective hit count it falls,
/// awards Wood, and shows a stump that regrows after WoodTreeData.regrowSeconds
/// (UtcNow-based so offline time counts). Only interactable when the camera is at Woods.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class TreeNode : MonoBehaviour
{
    [SerializeField] private WoodTreeData data;
    [SerializeField] private float shakePixels = 3f;

    private SpriteRenderer sr;
    private Collider2D ownCollider;
    private int hitsSoFar;
    private bool isStump;
    private long stumpSinceUtcTicks;
    private Vector3 basePos;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        basePos = transform.localPosition;
        ShowStanding();
    }

    private void Update()
    {
        if (isStump) { TryRegrow(); return; }
        if (!AtWoods()) return;
        if (!TryReadTap(out Vector2 screenPos)) return;
        if (!PointerHitsSelf(screenPos)) return;
        Chop();
    }

    private void Chop()
    {
        int axe = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.AxeLevel : 0;
        int reduction = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.HitsReductionPerLevel : 0;

        if (!WoodcuttingMath.CanFell(data.requiredAxeLevel, axe))
        {
            // Locked tree: small feedback, no progress. (Toast/hint optional.)
            return;
        }

        hitsSoFar++;
        LeanTween.moveLocalX(gameObject, basePos.x + shakePixels / 32f, 0.04f).setLoopPingPong(1);

        int needed = WoodcuttingMath.EffectiveHitsToFell(data.baseHitsToFell, axe, reduction);
        if (hitsSoFar >= needed) Fell();
    }

    private void Fell()
    {
        if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddWood(data.woodYield);
        // TODO(art): floating +N text via existing floating-number system.
        hitsSoFar = 0;
        isStump = true;
        stumpSinceUtcTicks = System.DateTime.UtcNow.Ticks;
        ShowStump();
    }

    private void TryRegrow()
    {
        double elapsed = (System.DateTime.UtcNow.Ticks - stumpSinceUtcTicks) / (double)System.TimeSpan.TicksPerSecond;
        if (WoodcuttingMath.IsRegrown(elapsed, data.regrowSeconds)) { isStump = false; ShowStanding(); }
    }

    private void ShowStanding()
    {
        if (data != null && data.standingSprite != null) sr.sprite = data.standingSprite;
    }

    private void ShowStump()
    {
        if (data != null && data.stumpSprite != null) sr.sprite = data.stumpSprite;
    }

    private static bool AtWoods()
    {
        var pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        return pan != null && !pan.IsPanning && pan.CurrentLocation == CameraPanController.Location.Woods;
    }

    // --- pointer helpers (mirrors ShopBuilding) ---
    private static bool TryReadTap(out Vector2 screenPos)
    {
        screenPos = default;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
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
}
```

- [ ] **Step 2: Verify compilation**

MCP `read_console`: no errors.

- [ ] **Step 3: Place trees in the scene**

At the Woods offset area, create 3-5 GameObjects with `SpriteRenderer` + `BoxCollider2D` + `TreeNode`, assign `Softwood`/`Hardwood` data, and a `YSort` component (per project convention). Use a placeholder sprite if tree art isn't sliced yet. Save the scene.

- [ ] **Step 4: Manual verification**

Play → pan to Woods → tap a Softwood tree `baseHitsToFell` times → it becomes a stump, Wood increases by `woodYield` → wait `regrowSeconds` → it returns to standing. Tap a Hardwood tree with axe level 0 → no progress (locked).

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Woodcutting/TreeNode.cs" "Assets/Scenes/SampleScene.unity"
git commit -m "feat(woodcutting): TreeNode chop/fell/award/regrow"
```

---

### Task 7: Wood rack sell panel (UITK)

A clickable world "wood rack" opens a UITK panel: current Wood, Sell-for-Cash (run-gated) / Sell-for-Gold buttons, and x1/x10/All stack controls.

**Files:**
- Create: `Assets/Scripts/Woodcutting/WoodRack.cs` (world-object click → opens panel; mirrors `ShopBuilding` interaction)
- Create: `Assets/Scripts/UI/WoodRackPopupUITK.cs` (the panel controller; mirrors `CarpenterPopupUITK`)
- Create: `Assets/UI/WoodRackPopup.uxml` + reuse existing shop USS, or create `Assets/UI/WoodRackPopup.uss`

**Interfaces:**
- Consumes: `CurrencyManager` (Wood/AddMoney/AddCoins/SpendWood, OnWoodChanged); `RunManager.Instance.IsRunActive`, `RunManager` OnRunStarted/OnRunEnded; `WoodcuttingMath.ResolveStackAmount/SellValue`.
- Produces:
  - `WoodRackPopupUITK.Instance`, `void Open()`, `void Close()`, `bool IsOpen`
  - `int WoodRackPopupUITK.CashPricePerWood` / `int GoldPricePerWood` (serialized tuning)

- [ ] **Step 1: Create the panel controller**

Create `Assets/Scripts/UI/WoodRackPopupUITK.cs`, following the lifecycle pattern of `CarpenterPopupUITK` (Awake singleton, OnEnable CacheElements/WireCallbacks/TrySubscribe, Open/Close with the `open` class + display toggle, MarkDirty→Refresh debounce). Key logic:

```csharp
// Fields (serialized tuning):
[SerializeField] private int cashPricePerWood = 4;   // Money per wood, in-run
[SerializeField] private int goldPricePerWood = 1;   // Coins per wood, anytime
private WoodcuttingMath.StackMode stackMode = WoodcuttingMath.StackMode.One;

public int CashPricePerWood => cashPricePerWood;
public int GoldPricePerWood => goldPricePerWood;

// Subscribe in TrySubscribeEvents():
//   CurrencyManager.Instance.OnWoodChanged += _ => MarkDirty();
//   RunManager.Instance.OnRunStarted += _ => MarkDirty();
//   RunManager.Instance.OnRunEnded  += _ => MarkDirty();   // match RunManager's actual event signatures

private void SellForCash()
{
    var cm = CurrencyManager.Instance;
    bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
    if (cm == null || !inRun) return;
    int amount = WoodcuttingMath.ResolveStackAmount(stackMode, cm.Wood);
    if (amount <= 0) return;
    if (!cm.SpendWood(amount)) return;
    cm.AddMoney(WoodcuttingMath.SellValue(amount, cashPricePerWood));
}

private void SellForGold()
{
    var cm = CurrencyManager.Instance;
    if (cm == null) return;
    int amount = WoodcuttingMath.ResolveStackAmount(stackMode, cm.Wood);
    if (amount <= 0) return;
    if (!cm.SpendWood(amount)) return;
    cm.AddCoins(WoodcuttingMath.SellValue(amount, goldPricePerWood));
}
```

In `Refresh()`: set the Wood-count label; set the Cash button `SetEnabled(RunManager.Instance != null && RunManager.Instance.IsRunActive)`, and when disabled show hint text "Start a run to sell for Cash"; the Gold button is always enabled; three stack toggle buttons set `stackMode` and re-Refresh; show each button's projected proceeds via `SellValue(ResolveStackAmount(...), price)`.

- [ ] **Step 2: Create the UXML/USS**

Create `Assets/UI/WoodRackPopup.uxml` with: a `popup-root`, `header-title` Label ("Wood Rack"), a `close-button`, a Wood-count Label (`wood-count`), three stack-toggle Buttons (`stack-1`/`stack-10`/`stack-all`), a `sell-cash` Button, a `sell-gold` Button, and a `cash-hint` Label. Style with the `UI_Runewood` frames/buttons (reference existing shop USS classes like `market-row`, or add a small `WoodRackPopup.uss`). Set the `UIDocument` source to this UXML on the popup GameObject.

- [ ] **Step 3: Create the WoodRack world object**

Create `Assets/Scripts/Woodcutting/WoodRack.cs`, mirroring `ShopBuilding`'s pointer press/click but with `requiredLocation = Woods` and `HandleClick()` calling `WoodRackPopupUITK.Instance.Open()`. Place a wood-rack sprite object near the trees with `SpriteRenderer` + `BoxCollider2D` + `WoodRack`.

- [ ] **Step 4: Verify compilation**

MCP `read_console`: no errors. Confirm the actual `RunManager` event names used in Step 1 compile (adjust to the real `OnRunStarted`/`OnRunEnded` signatures).

- [ ] **Step 5: Create the popup GameObject**

Add a `WoodRackPopup` GameObject with `UIDocument` (shared PanelSettings, per project convention) + `WoodRackPopupUITK`. Save the scene.

- [ ] **Step 6: Manual verification**

- Not in a run: open the rack → Gold sell works and reduces Wood / increases Coins; Cash button is disabled with the hint.
- In a run: Cash sell works and increases Money; supply-throttle holds (you can only sell what you chopped).
- Stack x1/x10/All sell the expected amounts, clamped to owned Wood.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/Woodcutting/WoodRack.cs" "Assets/Scripts/UI/WoodRackPopupUITK.cs" "Assets/UI/WoodRackPopup.uxml" "Assets/UI" "Assets/Scenes/SampleScene.unity"
git commit -m "feat(woodcutting): wood rack sell panel (Cash run-gated / Gold anytime, stack controls)"
```

---

### Task 8: Carpenter "Tools" section — axe upgrade row

Add an axe-upgrade row to the existing Carpenter popup that spends Coins + Wood via `WoodcuttingManager`.

**Files:**
- Modify: `Assets/Scripts/UI/CarpenterPopupUITK.cs` (add a Tools row in `Refresh()`)

**Interfaces:**
- Consumes: `WoodcuttingManager.Instance` (AxeLevel, MaxAxeLevel, NextUpgradeCoinCost/WoodCost, CanUpgradeAxe, TryUpgradeAxe, OnAxeLevelChanged); `CurrencyManager.OnWoodChanged`.
- Produces: nothing new (UI only).

- [ ] **Step 1: Subscribe to axe + wood changes**

In `CarpenterPopupUITK.TrySubscribeEvents()` add (guarded like the existing subscriptions):

```csharp
        if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnWoodChanged += OnWoodOrAxeChanged;
        if (WoodcuttingManager.Instance != null) WoodcuttingManager.Instance.OnAxeLevelChanged += OnWoodOrAxeChanged;
```

Add matching unsubscribes in `UnsubscribeEvents()`, and:

```csharp
    private void OnWoodOrAxeChanged(int _) => MarkDirty();
```

- [ ] **Step 2: Add the axe row builder and call it**

In `Refresh()` after `BuildGreenhouseRow();` add `BuildAxeUpgradeRow();`, and add the method (mirror `BuildGreenhouseRow`'s structure — build `market-row` with text block + right block):

```csharp
    private void BuildAxeUpgradeRow()
    {
        var wm = WoodcuttingManager.Instance;
        if (wm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label($"Upgrade Axe (Lv {wm.AxeLevel}/{wm.MaxAxeLevel})");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Fell harder trees and chop faster.");
        desc.AddToClassList("market-row-desc");
        textBlock.Add(title);
        textBlock.Add(desc);

        VisualElement rightBlock = new VisualElement();
        rightBlock.AddToClassList("market-row-right");
        Label status = new Label();
        status.AddToClassList("market-row-status");
        Label cost = new Label();
        cost.AddToClassList("market-row-cost");
        rightBlock.Add(status);
        rightBlock.Add(cost);

        row.Add(textBlock);
        row.Add(rightBlock);

        bool maxed = wm.AxeLevel >= wm.MaxAxeLevel;
        if (maxed)
        {
            row.AddToClassList("market-row--owned");
            status.text = "✓ Max";
            cost.text = "";
        }
        else
        {
            cost.text = $"{FormatCoinCost(wm.NextUpgradeCoinCost())} + {wm.NextUpgradeWoodCost()} wood";
            if (wm.CanUpgradeAxe())
            {
                row.AddToClassList("market-row--buy");
                status.text = "UPGRADE";
                row.RegisterCallback<ClickEvent>(_ => { WoodcuttingManager.Instance.TryUpgradeAxe(); });
                WirePressedFeedback(row, "market-row--pressed");
            }
            else
            {
                row.AddToClassList("market-row--cant-afford");
                status.text = "🔒 LOCKED";
            }
        }

        rowsList.Add(row);
    }
```

- [ ] **Step 3: Verify compilation**

MCP `read_console`: no errors.

- [ ] **Step 4: Manual verification**

Play → chop enough Softwood to afford axe level 1 → open Carpenter → the axe row shows cost (coins + wood); tap UPGRADE → Coins + Wood deducted, axe level increments → Hardwood trees now choppable and Softwood needs fewer taps. At max level the row shows "✓ Max."

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/UI/CarpenterPopupUITK.cs"
git commit -m "feat(woodcutting): Carpenter Tools axe-upgrade row"
```

---

### Task 9: Scene assembly, tuning pass & end-to-end playtest

Wire everything into a coherent Woods area and confirm the full loop.

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (final layout, offsets, tuning)

- [ ] **Step 1: Compose the Woods area**

Position the trees, wood rack, and any forest backdrop props at the Woods camera offset. Fine-tune `CameraPanController` Woods offset so the framing sits bottom-right with the farm partly in view. Confirm `YSort` on all world props.

- [ ] **Step 2: Tuning pass**

Set placeholder numbers to feel right: Softwood/Hardwood `baseHitsToFell`/`woodYield`/`regrowSeconds`, `cashPricePerWood` so a full forest ≈ "a few more minutes" of run fuel, `goldPricePerWood` slower, axe costs. Record chosen values in the spec's Open Questions §1-2 if they settle.

- [ ] **Step 3: Full-loop playtest checklist**

- [ ] Nav button pans to Woods; back returns to Farm; weather/sway visibly still apply in the Woods.
- [ ] Chopping fells trees, awards Wood, stumps regrow (including after a save/stop/replay gap → offline regrow).
- [ ] Wood persists across save/load; axe level persists across save/load.
- [ ] Gold sell works out of run; Cash sell only in a run; stack x1/x10/All correct.
- [ ] Axe upgrade unlocks Hardwood + reduces Softwood taps; maxes out cleanly.
- [ ] No console errors/warnings introduced.

- [ ] **Step 4: Run full EditMode test suite**

Run all EditMode tests; confirm `WoodcuttingMathTests` (8) pass and no existing tests regressed.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scenes/SampleScene.unity"
git commit -m "feat(woodcutting): assemble Woods area + tuning pass"
```

---

## Notes carried from the spec
- **Art dependency (open):** no clean world tree sprite with standing/stump states was found (only `Plants/Tree_Bonsai.png`). Placeholder sprites unblock all code; final art (softwood + hardwood standing + stump) can be sliced from `Assets/Sprites/Environment/CL_MainLev.png` or sourced later, then assigned on the `WoodTreeData` assets.
- **Future consumers (not in this plan):** Construction (spend Wood to build) and Preserves/Crafting (furnace burns Wood) plug into the `CurrencyManager` Wood API and `OnWoodChanged` without touching Woodcutting internals.
- **Deferred:** passive/auto-chop, market price-decay on Cash sales, multiple distinct wood types, stump-chipping, achievement-gated axe costs.
