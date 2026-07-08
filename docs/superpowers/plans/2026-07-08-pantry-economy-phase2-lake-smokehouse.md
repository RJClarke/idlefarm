# Pantry Economy Phase 2 — Lake, Fishing Pole & Smokehouse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the second wood sink — a **Lake** where you fish (semi-active), a **fishing pole** bought/upgraded at the Carpenter, and a **Smokehouse** that burns Wood to smoke caught fish into a rarity-gated jackpot ladder. Introduces the global **Pantry** (counted fish stacks) the Phase 1 spec deferred.

**Architecture:** Reuse the Phase 1 firebox core verbatim — `ProcessingMath` (EconomyCore) already simulates burn/pause/finish on `CanneryState`/`CannerySlot`/`ReadyJar`. The Smokehouse consumes those same types via file-local `using` aliases (no rename of shipped Phase 1 code — see Global Constraints), differing only in what fills a slot (a fish, not accumulating crop units) and where finished goods go (Pantry counts, not a shelf). Fishing gets its own pure math (`FishingMath`) + a thin `FishingManager` MonoBehaviour mirroring `WoodcuttingManager`, and a `LakeNode` world interaction mirroring `TreeNode`/`WoodRack`. A new `PantryManager` holds fungible fish counts in the `CurrencyManager`/Compost pattern. All timers are UtcNow-anchored → offline catch-up for free.

**Tech Stack:** Unity 6000.3 (6000.3.9f1), C#, UI Toolkit, NUnit EditMode tests, LeanTween, new Input System, Unity MCP (gladekit) for all scene work.

**Spec:** `docs/superpowers/specs/2026-07-07-pantry-economy-design.md` (§1, §3, §3a, §5, §5a, §6, §8, §10, §11)

## Global Constraints

- **Never edit `.unity`, `.prefab`, `.asset`, `.meta` files as text** — scene/asset work goes through Unity MCP tools; `.cs`, `.uxml`, `.uss` files are fine to create/edit directly.
- **New Input System only** — `Mouse.current` / `Touchscreen.current` / `Keyboard.current`, never `Input.*`.
- Pure logic goes in the **EconomyCore** assembly (`Assets/Scripts/EconomyCore/`); MonoBehaviours in Assembly-CSharp (`Assets/Scripts/...`). Do NOT create new asmdefs.
- EditMode tests live in `Assets/Tests/EditMode/` (asmdef `IdleFarm.EditModeTests`, already references EconomyCore).
- USS: Unity 6000.3 does **not** support `:last-child`.
- **Do NOT rename the Phase 1 shared processing types** (`CannerySlot`, `CanneryState`, `ReadyJar`). They are shipped and their names appear in `GameData` field names (`cannerySlots`, `canneryReadyJars`) that a blanket rename would corrupt, breaking save compatibility. The Smokehouse reads them as the generic processing model via file-local `using` aliases. The only change to `ProcessingMath.cs` is **additive** (Task 1).
- All tuning numbers below are starting knobs; serialize them in the inspector where possible so they can be tuned without recompiling. Economic values come straight from spec §3 (the premium curve is the contract, not the exact numbers).
- Debug.Log only for important events (purchases, catches, batch completion); LogWarning/LogError freely.
- After each script change: run `mcp__gladekit-unity__compile_scripts` (or `mcp__UnityMCP__read_console` after `refresh_unity`) until no errors. Domain reload may drop the MCP bridge briefly ("ReadError"/"connection attempts failed") — retry after ~10s.
- Run EditMode tests via a Unity MCP test tool if one responds (`mcp__gladekit-unity__run_tests` or `mcp__UnityMCP__run_tests`; extended tools are callable even when unlisted). If none responds, use the file-triggered bridge: touch `Temp/run_editmode_tests.request`, read `Temp/editmode_test_results.txt` (`Assets/Editor/EditModeTestBridge.cs`, added in Phase 1). Compilation cleanliness is NOT a substitute for the math tests.
- Commit after each task with the trailer:
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- **Deviation from spec §1 (recorded, not a bug):** the Pantry holds fish as fungible per-tier **counts** (all Perch are identical), not a per-item shelf like the Cannery's jars (jars carry a crop name, so they stay on the shelf). Raw fish sell and smoked fish sell are both counted-stack sells in the Smokehouse UI, matching spec §1 exactly. The Smokehouse's `state.readyJars` list is only ever a transient scratch buffer inside a single simulate call — finished smoked fish are drained into Pantry counts immediately and never persisted on a shelf.
- **Deferred within Phase 2 (documented, keeps scope bounded):** the press/hold **cast distance meter** (spec §3a) is aesthetic-only and shipped here as a simple tap-to-cast; the charge-meter visual is a later polish pass. Everything else in §3/§3a is in scope.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/EconomyCore/ProcessingMath.cs` | Modify | Add `tier`/`sourceId` to `ReadyJar`; Simulate copies them on finish (routes smoked fish by tier) |
| `Assets/Tests/EditMode/ProcessingMathTests.cs` | Modify | Test the finished-good carries tier/sourceId |
| `Assets/Scripts/EconomyCore/FishTiers.cs` | Create | Pure: fish tier count + display names |
| `Assets/Scripts/EconomyCore/FishingMath.cs` | Create | Pure: bite-time roll, weighted rarity roll, pole purchase/upgrade gating |
| `Assets/Tests/EditMode/FishTiersTests.cs` | Create | Unit tests |
| `Assets/Tests/EditMode/FishingMathTests.cs` | Create | Unit tests |
| `Assets/Scripts/Pantry/PantryManager.cs` | Create | Singleton: fungible raw/smoked fish counts + events + save capture/load |
| `Assets/Scripts/Fishing/FishingManager.cs` | Create | Singleton: pole level, cast state machine (UtcNow bite), pole buy/upgrade, save capture/load |
| `Assets/Scripts/Fishing/LakeNode.cs` | Create | Clickable Lake water: cast / collect / no-pole hint + bite indicator (WorldHintPopup) |
| `Assets/Scripts/Fishing/LakeNavButton.cs` | Create | Home-screen nav button that pans the camera to the Lake (mirrors WoodsNavButton) |
| `Assets/Scripts/Smokehouse/SmokehouseManager.cs` | Create | Singleton: firebox (shared ProcessingMath), fish→slot load, drain finished→Pantry, fuel, slots, sell raw/smoked, save capture/load |
| `Assets/Scripts/Smokehouse/SmokehouseBuilding.cs` | Create | Clickable world prop + smoke toggle (CanneryBuilding pattern) |
| `Assets/Scripts/UI/SmokehousePopupUITK.cs` | Create | UITK popup controller (CanneryPopupUITK pattern) |
| `Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uxml` | Create | Popup layout |
| `Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uss` | Create | Popup styles (copied from Cannery USS + additions) |
| `Assets/Scripts/CameraPanController.cs` | Modify | Add `Location.Lake` + a default offset entry |
| `Assets/Scripts/BuildingState.cs` | Modify | Add `SmokehouseKey` |
| `Assets/Scripts/GameData.cs` | Modify | Add pantry / fishing / smokehouse save fields + ctor defaults |
| `Assets/Scripts/SaveManager.cs` | Modify | Capture/load pantry, fishing, smokehouse |
| `Assets/Scripts/AutoSaveManager.cs` | Modify | Subscribe the three new `OnChanged` events |
| `Assets/Scripts/UI/CarpenterPopupUITK.cs` | Modify | Pole buy/upgrade rows (Tools) + "Build Smokehouse" row (Construction) |
| Scene `SampleScene` | Modify via MCP | Lake location + water + LakeNode; Smokehouse prop + smoke; PantryManager/FishingManager/SmokehouseManager GOs; Smokehouse popup UIDocument; Lake nav button |

---

### Task 1: ProcessingMath — carry tier + source id on finished goods

**Files:**
- Modify: `Assets/Scripts/EconomyCore/ProcessingMath.cs`
- Modify: `Assets/Tests/EditMode/ProcessingMathTests.cs`

**Interfaces:**
- Consumes: existing Phase 1 types.
- Produces: `ReadyJar.tier : int`, `ReadyJar.sourceId : string` (both populated by `Simulate` from the finishing slot). The Smokehouse (Task 8) reads `tier` to bump the right Pantry smoked-fish bucket.

**Why:** The Smokehouse routes finished goods into per-tier Pantry counts, so a finished good must remember its tier. This is purely additive — new serialized fields on an existing `[Serializable]` type are backward-compatible with existing JSON saves (JsonUtility defaults missing fields to 0/null), and the Cannery simply ignores them (jars are identified by `cropName`).

- [ ] **Step 1: Write the failing test**

Append inside the `ProcessingMathTests` class in `Assets/Tests/EditMode/ProcessingMathTests.cs`:

```csharp
    [Test]
    public void Simulate_FinishedGood_CarriesTierAndSourceId()
    {
        var st = MakeState(1);
        LoadSlot(st.slots[0], "fish_perch", 1, 1, cookRemaining: 10);
        st.slots[0].tier = 2;                 // pretend a Bass
        st.slots[0].jarValue = 1400;
        st.fuelWood = 100;
        int finished = ProcessingMath.Simulate(st, 10, baseWoodPerHour: 0f, perSlotWoodPerHour: 3600f);
        Assert.AreEqual(1, finished);
        Assert.AreEqual(1, st.readyJars.Count);
        Assert.AreEqual(2, st.readyJars[0].tier);
        Assert.AreEqual("fish_perch", st.readyJars[0].sourceId);
        Assert.AreEqual(1400, st.readyJars[0].value);
    }
```

- [ ] **Step 2: Run tests to verify the new one fails**

Expected: FAIL — `ReadyJar` has no `tier`/`sourceId` (compile error is the fail signal).

- [ ] **Step 3: Add the fields + populate on finish**

In `Assets/Scripts/EconomyCore/ProcessingMath.cs`, extend the `ReadyJar` type:

```csharp
/// <summary>A finished processed good on the ready shelf / awaiting deposit, waiting to be sold or
/// banked for Coins. `tier`/`sourceId` let a consumer (e.g. the Smokehouse) route it by kind.</summary>
[Serializable]
public class ReadyJar
{
    public string cropName;
    public int value;
    public int tier;        // 1..3 — copied from the finishing slot (0 on legacy jars)
    public string sourceId; // slot.cropId — the crop/fish that produced this good
}
```

In `Simulate`, in the finish block where the `ReadyJar` is created, copy the two new fields:

```csharp
                if (s.cookSecondsRemaining <= 1e-6)
                {
                    st.readyJars.Add(new ReadyJar { cropName = s.cropName, value = s.jarValue, tier = s.tier, sourceId = s.cropId });
                    finished++;
                    s.cropId = null; s.cropName = null; s.tier = 0;
                    s.unitsLoaded = 0; s.unitsRequired = 0;
                    s.cookSecondsRemaining = 0; s.jarValue = 0;
                }
```

- [ ] **Step 4: Compile clean + run all ProcessingMath tests** → all PASS (Phase 1's 10 + this 1).

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/EconomyCore/ProcessingMath.cs Assets/Tests/EditMode/ProcessingMathTests.cs
git commit -m "feat(smokehouse): carry tier + sourceId on finished processing goods"
```

---

### Task 2: FishTiers — pure fish tier metadata

**Files:**
- Create: `Assets/Scripts/EconomyCore/FishTiers.cs`
- Create: `Assets/Tests/EditMode/FishTiersTests.cs`

**Interfaces (Produces):**
- `FishTiers.Count : int` (= 3)
- `FishTiers.Name(int tier) : string` — 1→"Perch", 2→"Bass", 3→"Northern Pike", clamped
- `FishTiers.SmokedName(int tier) : string` — "Smoked " + Name

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/FishTiersTests.cs`:

```csharp
using NUnit.Framework;

public class FishTiersTests
{
    [Test]
    public void Count_IsThree() => Assert.AreEqual(3, FishTiers.Count);

    [Test]
    public void Name_MapsTiers_AndClamps()
    {
        Assert.AreEqual("Perch", FishTiers.Name(1));
        Assert.AreEqual("Bass", FishTiers.Name(2));
        Assert.AreEqual("Northern Pike", FishTiers.Name(3));
        Assert.AreEqual("Perch", FishTiers.Name(0));   // clamped up
        Assert.AreEqual("Northern Pike", FishTiers.Name(99)); // clamped down
    }

    [Test]
    public void SmokedName_PrefixesSmoked()
    {
        Assert.AreEqual("Smoked Perch", FishTiers.SmokedName(1));
        Assert.AreEqual("Smoked Northern Pike", FishTiers.SmokedName(3));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail** (missing `FishTiers` → compile error).

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/EconomyCore/FishTiers.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Pure metadata for the three fish tiers (spec §3). No Unity object dependencies. Economic
/// values (odds, bite rate, raw/smoked value, smoke hours) are inspector knobs on the managers;
/// only the stable count + display names live here so every consumer agrees on them.
/// </summary>
public static class FishTiers
{
    public const int Count = 3;

    private static readonly string[] Names = { "Perch", "Bass", "Northern Pike" };

    /// <summary>Display name for a 1-based tier, clamped to the valid range.</summary>
    public static string Name(int tier) => Names[Mathf.Clamp(tier, 1, Count) - 1];

    /// <summary>Display name of the smoked product for a 1-based tier.</summary>
    public static string SmokedName(int tier) => "Smoked " + Name(tier);
}
```

- [ ] **Step 4: Compile clean + run the three tests** → PASS.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/EconomyCore/FishTiers.cs Assets/Tests/EditMode/FishTiersTests.cs
git commit -m "feat(fishing): FishTiers pure metadata (count + display names)"
```

---

### Task 3: FishingMath — pure bite/rarity/pole math

**Files:**
- Create: `Assets/Scripts/EconomyCore/FishingMath.cs`
- Create: `Assets/Tests/EditMode/FishingMathTests.cs`

**Interfaces (Produces):**
- `FishingMath.RollBiteSeconds(double avgSeconds, float rand01) : double` — spread 0.5×–1.5× the average (rand01 clamped 0..1)
- `FishingMath.RollFishTier(float[] weights, float rand01) : int` — weighted pick → 1-based tier; null/empty → 1
- `FishingMath.CanBuyPole(bool hasPole, int coins, int coinCost) : bool`
- `FishingMath.CanUpgradePole(int poleLevel, int maxLevel, int coins, int coinCost, int wood, int woodCost) : bool`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/FishingMathTests.cs`:

```csharp
using NUnit.Framework;

public class FishingMathTests
{
    [Test]
    public void RollBiteSeconds_SpreadsAroundAverage()
    {
        Assert.AreEqual(600.0,  FishingMath.RollBiteSeconds(1200, 0f),   1e-6); // 0.5x
        Assert.AreEqual(1200.0, FishingMath.RollBiteSeconds(1200, 0.5f), 1e-6); // 1.0x
        Assert.AreEqual(1800.0, FishingMath.RollBiteSeconds(1200, 1f),   1e-6); // 1.5x
        // rand clamps
        Assert.AreEqual(600.0,  FishingMath.RollBiteSeconds(1200, -3f),  1e-6);
        Assert.AreEqual(1800.0, FishingMath.RollBiteSeconds(1200, 9f),   1e-6);
    }

    [Test]
    public void RollFishTier_PicksByCumulativeWeight()
    {
        // base pole odds: 98 / 1.9 / 0.1  → cumulative 0.98, 0.999, 1.0
        var w = new[] { 98f, 1.9f, 0.1f };
        Assert.AreEqual(1, FishingMath.RollFishTier(w, 0f));      // deep in perch
        Assert.AreEqual(1, FishingMath.RollFishTier(w, 0.5f));    // still perch
        Assert.AreEqual(2, FishingMath.RollFishTier(w, 0.985f));  // into bass band
        Assert.AreEqual(3, FishingMath.RollFishTier(w, 0.9995f)); // pike
        Assert.AreEqual(3, FishingMath.RollFishTier(w, 1f));      // top → last tier
    }

    [Test]
    public void RollFishTier_HandlesNullOrEmpty()
    {
        Assert.AreEqual(1, FishingMath.RollFishTier(null, 0.5f));
        Assert.AreEqual(1, FishingMath.RollFishTier(new float[0], 0.5f));
    }

    [Test]
    public void PoleGating_MirrorsAxe()
    {
        Assert.IsTrue(FishingMath.CanBuyPole(false, 80, 75));
        Assert.IsFalse(FishingMath.CanBuyPole(true, 999, 75));  // already owned
        Assert.IsFalse(FishingMath.CanBuyPole(false, 74, 75));  // short coins

        Assert.IsTrue(FishingMath.CanUpgradePole(0, 3, 300, 300, 50, 50));
        Assert.IsFalse(FishingMath.CanUpgradePole(3, 3, 9999, 300, 9999, 50)); // at max
        Assert.IsFalse(FishingMath.CanUpgradePole(0, 3, 299, 300, 9999, 50));  // short coins
        Assert.IsFalse(FishingMath.CanUpgradePole(0, 3, 9999, 300, 49, 50));   // short wood
    }
}
```

- [ ] **Step 2: Run tests to verify they fail** (missing `FishingMath`).

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/EconomyCore/FishingMath.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Pure decision logic for fishing (spec §3a). No Unity object dependencies so it is fully
/// unit-testable; FishingManager injects UnityEngine.Random values as rand01 arguments.
/// </summary>
public static class FishingMath
{
    /// <summary>
    /// Time until the next bite, spread 0.5×–1.5× around the average so bites feel organic while
    /// staying bounded (spec §3a: ~20 min average, throughput capped by real time).
    /// </summary>
    public static double RollBiteSeconds(double avgSeconds, float rand01)
        => avgSeconds * (0.5 + Mathf.Clamp01(rand01));

    /// <summary>
    /// Weighted pick of a fish tier (1-based) from relative weights, using a 0..1 roll. Higher tiers
    /// sit at the tail of the cumulative distribution, so only a high roll lands a rare fish. A
    /// null/empty weight set falls back to tier 1.
    /// </summary>
    public static int RollFishTier(float[] weights, float rand01)
    {
        if (weights == null || weights.Length == 0) return 1;
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return 1;

        float target = Mathf.Clamp01(rand01) * total;
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (target < cumulative) return i + 1;
        }
        return weights.Length; // rand01 == 1 → last tier
    }

    /// <summary>First pole is Coins-only (you can't fish without one — same chicken-and-egg as the axe).</summary>
    public static bool CanBuyPole(bool hasPole, int coins, int coinCost) => !hasPole && coins >= coinCost;

    /// <summary>Pole upgrade allowed: under max level and both currencies affordable.</summary>
    public static bool CanUpgradePole(int poleLevel, int maxLevel, int coins, int coinCost, int wood, int woodCost)
    {
        if (poleLevel >= maxLevel) return false;
        return coins >= coinCost && wood >= woodCost;
    }
}
```

- [ ] **Step 4: Compile clean + run the FishingMath tests** → PASS.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/EconomyCore/FishingMath.cs Assets/Tests/EditMode/FishingMathTests.cs
git commit -m "feat(fishing): FishingMath pure bite/rarity roll + pole gating"
```

---

### Task 4: PantryManager + persistence

**Files:**
- Create: `Assets/Scripts/Pantry/PantryManager.cs`
- Modify: `Assets/Scripts/GameData.cs`
- Modify: `Assets/Scripts/SaveManager.cs`
- Modify: `Assets/Scripts/AutoSaveManager.cs`

**Interfaces:**
- Consumes: `FishTiers`.
- Produces (used by Tasks 5, 8, 11):
  - `PantryManager.Instance`
  - `int GetRaw(int tier)`, `int GetSmoked(int tier)`
  - `void AddRaw(int tier)`, `void AddSmoked(int tier)`
  - `bool SpendRaw(int tier)`, `bool SpendSmoked(int tier)` (1 unit each; false if none)
  - `int TotalRaw`, `int TotalSmoked`
  - `event Action OnChanged`
  - `void CaptureTo(GameData d)`, `void LoadFrom(GameData d)`

- [ ] **Step 1: Add GameData fields**

In `Assets/Scripts/GameData.cs`, after the `canneryLastSimUtcTicks` field:

```csharp
    // Pantry (Pantry Economy Phase 2). Fungible fish counts, index 0 = tier 1 (Perch).
    public int[] pantryRawFish;
    public int[] pantrySmokedFish;
```

In the default constructor, after `canneryReadyJars = new ReadyJar[0];`:

```csharp
        pantryRawFish = new int[FishTiers.Count];
        pantrySmokedFish = new int[FishTiers.Count];
```

- [ ] **Step 2: Create the manager**

Create `Assets/Scripts/Pantry/PantryManager.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// The Pantry: fungible counted stacks of raw and smoked fish (spec §1). Mirrors the
/// CurrencyManager/Compost pattern — integers + change events, no item objects. Fish are the
/// inter-building resource: caught at the Lake, stored here, pulled into the Smokehouse, and the
/// smoked output lands back here as counts. Tiers are 1-based in the API; stored 0-based.
/// </summary>
public class PantryManager : MonoBehaviour
{
    public static PantryManager Instance { get; private set; }

    private readonly int[] raw = new int[FishTiers.Count];
    private readonly int[] smoked = new int[FishTiers.Count];

    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static int Idx(int tier) => Mathf.Clamp(tier, 1, FishTiers.Count) - 1;

    public int GetRaw(int tier) => raw[Idx(tier)];
    public int GetSmoked(int tier) => smoked[Idx(tier)];

    public int TotalRaw { get { int n = 0; for (int i = 0; i < raw.Length; i++) n += raw[i]; return n; } }
    public int TotalSmoked { get { int n = 0; for (int i = 0; i < smoked.Length; i++) n += smoked[i]; return n; } }

    public void AddRaw(int tier)    { raw[Idx(tier)]++;    OnChanged?.Invoke(); }
    public void AddSmoked(int tier) { smoked[Idx(tier)]++; OnChanged?.Invoke(); }

    public bool SpendRaw(int tier)
    {
        int i = Idx(tier);
        if (raw[i] <= 0) return false;
        raw[i]--;
        OnChanged?.Invoke();
        return true;
    }

    public bool SpendSmoked(int tier)
    {
        int i = Idx(tier);
        if (smoked[i] <= 0) return false;
        smoked[i]--;
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureTo(GameData d)
    {
        d.pantryRawFish = (int[])raw.Clone();
        d.pantrySmokedFish = (int[])smoked.Clone();
    }

    public void LoadFrom(GameData d)
    {
        CopyInto(d.pantryRawFish, raw);
        CopyInto(d.pantrySmokedFish, smoked);
        OnChanged?.Invoke();
    }

    private static void CopyInto(int[] src, int[] dst)
    {
        for (int i = 0; i < dst.Length; i++)
            dst[i] = (src != null && i < src.Length) ? Mathf.Max(0, src[i]) : 0;
    }
}
```

- [ ] **Step 3: Wire SaveManager**

In `SaveGame()`, after the `CanneryManager.Instance.CaptureTo(data)` line (~141):

```csharp
        if (PantryManager.Instance != null) PantryManager.Instance.CaptureTo(data);
```

In `LoadGame()`, after the `CanneryManager.Instance.LoadFrom(data)` block (~202):

```csharp
                if (PantryManager.Instance != null)
                    PantryManager.Instance.LoadFrom(data);
```

- [ ] **Step 4: Wire AutoSaveManager**

In `TrySubscribe()` after the `CanneryManager` subscription:

```csharp
        if (PantryManager.Instance != null)
            PantryManager.Instance.OnChanged += OnVoid;
```

In `Unsubscribe()` after the `CanneryManager` unsubscription:

```csharp
        if (PantryManager.Instance != null)
            PantryManager.Instance.OnChanged -= OnVoid;
```

> **Note:** confirm the void-handler name AutoSaveManager uses for parameterless events (`OnVoid` in Phase 1's plan). If it differs (e.g. `OnVoidChanged`), match the existing name. Grep `AutoSaveManager.cs` for how `CanneryManager.Instance.OnChanged +=` was wired and copy that exact handler.

- [ ] **Step 5: Compile clean; run all EditMode tests** (still green — manager has no unit tests; its data is trivial).

- [ ] **Step 6: Commit**

```
git add Assets/Scripts/Pantry/PantryManager.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs
git commit -m "feat(pantry): PantryManager fungible fish counts + persistence"
```

---

### Task 5: FishingManager + persistence (pole, cast state machine, bite roll)

**Files:**
- Create: `Assets/Scripts/Fishing/FishingManager.cs`
- Modify: `Assets/Scripts/GameData.cs`
- Modify: `Assets/Scripts/SaveManager.cs`
- Modify: `Assets/Scripts/AutoSaveManager.cs`

**Interfaces:**
- Consumes: `FishingMath`, `FishTiers`, `PantryManager`, `CurrencyManager`, `WorldHintPopup`.
- Produces (used by Tasks 6, 7, 11):
  - `FishingManager.Instance`
  - `bool HasPole`, `int PoleLevel`, `int MaxPoleLevel`, `int FirstPoleCoinCost`
  - `bool CanBuyPole()`, `bool TryBuyPole()`, `bool CanUpgradePole()`, `bool TryUpgradePole()`
  - `int NextUpgradeCoinCost()`, `int NextUpgradeWoodCost()`
  - `void SetHasPole(bool)`, `void SetPoleLevel(int)`
  - `enum CastState { Idle, Waiting, Bite }`, `CastState State`, `bool HasBite`, `int PendingTier`
  - `bool Cast()`, `int Collect()` (returns caught tier or 0)
  - `void ShowNoPoleHint(Vector3 worldPos)`
  - `event Action OnChanged` (durable: state/pole change), `event Action<int> OnPoleLevelChanged` (Carpenter UI refresh)
  - `void CaptureTo(GameData d)`, `void LoadFrom(GameData d)`

- [ ] **Step 1: Add GameData fields**

In `Assets/Scripts/GameData.cs`, after the pantry fields:

```csharp
    // Fishing (Pantry Economy Phase 2). Pole meta + the single in-flight line's cast state.
    public int poleLevel;
    public bool hasPole;
    public int fishingState;              // 0 Idle / 1 Waiting / 2 Bite
    public long fishingCastUtcTicks;
    public long fishingBiteReadyUtcTicks;
    public int fishingPendingTier;        // tier on the line when state == Bite
```

(No ctor defaults needed — int/bool/long default to 0/false, which is Idle with no pole.)

- [ ] **Step 2: Create the manager**

Create `Assets/Scripts/Fishing/FishingManager.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// Owns fishing meta-state: the pole, its tuning, and the single in-flight line's cast → wait →
/// bite cycle (spec §3a). Mirrors WoodcuttingManager. Bite timing is UtcNow-anchored so a line cast
/// before closing lands its bite offline. One line at a time; the fish waits forever once it bites.
/// Caught fish go to the PantryManager. All rarity/timing rolls run through FishingMath.
/// </summary>
public class FishingManager : MonoBehaviour
{
    public static FishingManager Instance { get; private set; }

    public enum CastState { Idle, Waiting, Bite }

    [Serializable]
    public class PoleTier
    {
        [Tooltip("Relative catch weights by fish tier: index 0 = Perch, 1 = Bass, 2 = Pike.")]
        public float[] weights = { 98f, 1.9f, 0.1f };
        [Tooltip("Average seconds to a bite at this pole level (spec §3a: ~20 min = 1200s at base).")]
        public float biteAvgSeconds = 1200f;
    }

    [Header("Pole Tiers (index = pole level; 0 = first pole)")]
    [Tooltip("Each level shifts rarity odds and bite rate. Rare fish stay possible at level 0.")]
    [SerializeField] private PoleTier[] poleTiers =
    {
        new PoleTier { weights = new[] { 98f,  1.9f, 0.1f }, biteAvgSeconds = 1200f },
        new PoleTier { weights = new[] { 95f,  4.5f, 0.5f }, biteAvgSeconds = 1050f },
        new PoleTier { weights = new[] { 90f,  8.5f, 1.5f }, biteAvgSeconds = 900f  },
        new PoleTier { weights = new[] { 82f, 15f,   3f   }, biteAvgSeconds = 780f  },
    };

    [Header("Pole Purchase / Upgrade (spec §3a)")]
    [Tooltip("Coins-only cost of the first pole (mirrors the first axe).")]
    [SerializeField] private int firstPoleCoinCost = 75;
    [Tooltip("Coin cost of the next pole level, indexed by current level (0 -> level 1, ...).")]
    [SerializeField] private int[] poleCoinCosts = { 300, 900, 2500 };
    [Tooltip("Wood cost of the next pole level, indexed by current level.")]
    [SerializeField] private int[] poleWoodCosts = { 50, 140, 320 };

    [Header("Hints")]
    [SerializeField] private string noPoleHintText = "You need to buy a fishing pole first.";

    private int poleLevel;
    private bool hasPole;
    private CastState state = CastState.Idle;
    private long castUtcTicks;
    private long biteReadyUtcTicks;
    private int pendingTier;
    private WorldHintPopup activeHint;

    public event Action OnChanged;             // durable: state/pole change, load
    public event Action<int> OnPoleLevelChanged; // Carpenter UI refresh (mirrors OnAxeLevelChanged)

    public bool HasPole => hasPole;
    public int PoleLevel => poleLevel;
    public int MaxPoleLevel => Mathf.Max(0, poleTiers.Length - 1);
    public int FirstPoleCoinCost => firstPoleCoinCost;
    public CastState State => state;
    public bool HasBite => state == CastState.Bite;
    public int PendingTier => pendingTier;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (state != CastState.Waiting) return;
        long now = DateTime.UtcNow.Ticks;
        if (biteReadyUtcTicks > 0 && now >= biteReadyUtcTicks)
            TransitionToBite();
    }

    private PoleTier CurrentTier()
    {
        if (poleTiers == null || poleTiers.Length == 0) return new PoleTier();
        return poleTiers[Mathf.Clamp(poleLevel, 0, poleTiers.Length - 1)];
    }

    private void TransitionToBite()
    {
        pendingTier = FishingMath.RollFishTier(CurrentTier().weights, UnityEngine.Random.value);
        state = CastState.Bite;
        Debug.Log($"[Fishing] Bite: {FishTiers.Name(pendingTier)} on the line.");
        OnChanged?.Invoke();
    }

    // ── Cast / collect (spec §3a) ────────────────────────────────────────

    /// <summary>Cast the line if idle and a pole is owned. Rolls the bite time now (UtcNow-anchored).</summary>
    public bool Cast()
    {
        if (!hasPole || state != CastState.Idle) return false;
        long now = DateTime.UtcNow.Ticks;
        double biteSecs = FishingMath.RollBiteSeconds(CurrentTier().biteAvgSeconds, UnityEngine.Random.value);
        castUtcTicks = now;
        biteReadyUtcTicks = now + (long)(biteSecs * TimeSpan.TicksPerSecond);
        state = CastState.Waiting;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Collect a fish on the line into the Pantry. Returns the caught tier (0 if none).</summary>
    public int Collect()
    {
        if (state != CastState.Bite) return 0;
        int tier = pendingTier;
        if (PantryManager.Instance != null) PantryManager.Instance.AddRaw(tier);
        pendingTier = 0;
        castUtcTicks = 0;
        biteReadyUtcTicks = 0;
        state = CastState.Idle;
        Debug.Log($"[Fishing] Collected {FishTiers.Name(tier)}.");
        OnChanged?.Invoke();
        return tier;
    }

    public void ShowNoPoleHint(Vector3 worldPos)
    {
        if (activeHint != null) Destroy(activeHint.gameObject);
        activeHint = WorldHintPopup.Create(worldPos, noPoleHintText);
    }

    // ── First pole (Coins only) ──────────────────────────────────────────

    public bool CanBuyPole()
    {
        var cm = CurrencyManager.Instance;
        return cm != null && FishingMath.CanBuyPole(hasPole, cm.Coins, firstPoleCoinCost);
    }

    public bool TryBuyPole()
    {
        if (!CanBuyPole()) return false;
        var cm = CurrencyManager.Instance;
        if (!cm.SpendCoins(firstPoleCoinCost)) return false;
        hasPole = true;
        Debug.Log("[Fishing] First pole purchased.");
        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
        return true;
    }

    // ── Pole upgrade (Coins + Wood) ──────────────────────────────────────

    public int NextUpgradeCoinCost()
    {
        if (poleLevel >= MaxPoleLevel || poleCoinCosts.Length == 0) return int.MaxValue;
        return poleCoinCosts[Mathf.Min(poleLevel, poleCoinCosts.Length - 1)];
    }

    public int NextUpgradeWoodCost()
    {
        if (poleLevel >= MaxPoleLevel || poleWoodCosts.Length == 0) return int.MaxValue;
        return poleWoodCosts[Mathf.Min(poleLevel, poleWoodCosts.Length - 1)];
    }

    public bool CanUpgradePole()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || !hasPole) return false;
        return FishingMath.CanUpgradePole(poleLevel, MaxPoleLevel, cm.Coins, NextUpgradeCoinCost(), cm.Wood, NextUpgradeWoodCost());
    }

    public bool TryUpgradePole()
    {
        if (!CanUpgradePole()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextUpgradeCoinCost();
        int woodCost = NextUpgradeWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund like axe upgrade
        poleLevel++;
        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
        return true;
    }

    public void SetHasPole(bool value) { hasPole = value; OnPoleLevelChanged?.Invoke(poleLevel); }
    public void SetPoleLevel(int level) { poleLevel = Mathf.Clamp(level, 0, MaxPoleLevel); OnPoleLevelChanged?.Invoke(poleLevel); }

    // ── Save / load ──────────────────────────────────────────────────────

    public void CaptureTo(GameData d)
    {
        d.poleLevel = poleLevel;
        d.hasPole = hasPole;
        d.fishingState = (int)state;
        d.fishingCastUtcTicks = castUtcTicks;
        d.fishingBiteReadyUtcTicks = biteReadyUtcTicks;
        d.fishingPendingTier = pendingTier;
    }

    public void LoadFrom(GameData d)
    {
        poleLevel = Mathf.Clamp(d.poleLevel, 0, MaxPoleLevel);
        hasPole = d.hasPole;
        state = (CastState)Mathf.Clamp(d.fishingState, 0, 2);
        castUtcTicks = d.fishingCastUtcTicks;
        biteReadyUtcTicks = d.fishingBiteReadyUtcTicks;
        pendingTier = d.fishingPendingTier;

        // Offline catch-up: a line left waiting whose bite time has passed is now biting.
        if (state == CastState.Waiting && biteReadyUtcTicks > 0 && DateTime.UtcNow.Ticks >= biteReadyUtcTicks)
            TransitionToBite();

        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
    }
}
```

- [ ] **Step 3: Wire SaveManager**

In `SaveGame()`, after the `PantryManager` capture line:

```csharp
        if (FishingManager.Instance != null) FishingManager.Instance.CaptureTo(data);
```

In `LoadGame()`, after the `PantryManager` load block:

```csharp
                if (FishingManager.Instance != null)
                    FishingManager.Instance.LoadFrom(data);
```

- [ ] **Step 4: Wire AutoSaveManager** (after the PantryManager subscription/unsubscription, same `OnVoid` handler):

```csharp
        if (FishingManager.Instance != null)
            FishingManager.Instance.OnChanged += OnVoid;
```
```csharp
        if (FishingManager.Instance != null)
            FishingManager.Instance.OnChanged -= OnVoid;
```

- [ ] **Step 5: Compile clean; run all EditMode tests** (still green).

- [ ] **Step 6: Commit**

```
git add Assets/Scripts/Fishing/FishingManager.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs
git commit -m "feat(fishing): FishingManager pole + cast/bite state machine + persistence"
```

---

### Task 6: Carpenter — Buy/Upgrade Pole rows (Tools)

**Files:**
- Modify: `Assets/Scripts/UI/CarpenterPopupUITK.cs`

**Interfaces:**
- Consumes: `FishingManager` (Task 5).
- Produces: nothing new (UI only).

**Behavior:** mirrors the axe rows exactly. In `Refresh()`'s "Tools" section, after the axe row, show a "Buy Pole" row until owned, then an "Upgrade Pole" row. Subscribe to `FishingManager.OnPoleLevelChanged` so the row refreshes on purchase.

- [ ] **Step 1: Subscribe to pole changes**

In `TrySubscribeEvents()`, after the `WoodcuttingManager.Instance.OnAxeLevelChanged += OnCurrencyChanged;` line:

```csharp
        if (FishingManager.Instance != null)
            FishingManager.Instance.OnPoleLevelChanged += OnCurrencyChanged;
```

In `UnsubscribeEvents()`, after the matching axe unsubscription:

```csharp
        if (FishingManager.Instance != null)
            FishingManager.Instance.OnPoleLevelChanged -= OnCurrencyChanged;
```

- [ ] **Step 2: Add the pole rows to `Refresh()`**

In `Refresh()`, in the "Tools" section, after the axe `if/else` block:

```csharp
        if (FishingManager.Instance != null)
        {
            if (!FishingManager.Instance.HasPole) BuildBuyPoleRow();
            else BuildPoleUpgradeRow();
        }
```

- [ ] **Step 3: Add the builder methods** (after `BuildAxeUpgradeRow()`):

```csharp
    private void BuildBuyPoleRow()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label("Buy Fishing Pole");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Your first pole. Needed to fish the Lake for Perch, Bass, and the rare Pike.");
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

        cost.text = FormatCoinCost(fm.FirstPoleCoinCost);
        if (fm.CanBuyPole())
        {
            row.AddToClassList("market-row--buy");
            status.text = "BUY";
            row.RegisterCallback<ClickEvent>(_ => { if (FishingManager.Instance != null) FishingManager.Instance.TryBuyPole(); });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
        }

        rowsList.Add(row);
    }

    private void BuildPoleUpgradeRow()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        // Levels read 1-based to the player: a bought pole is "Lv 1".
        Label title = new Label($"Upgrade Pole (Lv {fm.PoleLevel + 1}/{fm.MaxPoleLevel + 1})");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Bite faster and hook rarer fish.");
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

        bool maxed = fm.PoleLevel >= fm.MaxPoleLevel;
        if (maxed)
        {
            row.AddToClassList("market-row--owned");
            status.text = "✓ Max";
            cost.text = "";
        }
        else
        {
            cost.text = $"{FormatCoinCost(fm.NextUpgradeCoinCost())} + {fm.NextUpgradeWoodCost()} wood";
            if (fm.CanUpgradePole())
            {
                row.AddToClassList("market-row--buy");
                status.text = "UPGRADE";
                row.RegisterCallback<ClickEvent>(_ => { if (FishingManager.Instance != null) FishingManager.Instance.TryUpgradePole(); });
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

- [ ] **Step 4: Compile clean.**

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/UI/CarpenterPopupUITK.cs
git commit -m "feat(fishing): Carpenter Buy/Upgrade Pole rows (Tools)"
```

---

### Task 7: Camera Location.Lake + LakeNode + LakeNavButton (code)

**Files:**
- Modify: `Assets/Scripts/CameraPanController.cs`
- Create: `Assets/Scripts/Fishing/LakeNode.cs`
- Create: `Assets/Scripts/Fishing/LakeNavButton.cs`

**Interfaces:**
- Consumes: `CameraPanController.Location.Lake`, `FishingManager` (Task 5), `WorldHintPopup`, `PantryManager`.
- Produces: `CameraPanController.PanToLake()`, `Location.Lake`.

**Adding `Lake` to the enum is safe:** MusicManager treats any non-Market location as farm music (Lake gets farm music — fine), and LocationModeController only special-cases Market. No switch statement needs a new case; no `default` throws. Verified against all `Location.*` consumers.

- [ ] **Step 1: Extend the Location enum + offset**

In `CameraPanController.cs`, change the enum:

```csharp
    public enum Location { Farm, Greenhouse, Market, Woods, Lake }
```

Add a default offset entry to the `locations` initializer array (Lake sits directly right of the farm, farther out than Market per spec §8 — tune the exact vector in the scene task):

```csharp
        new LocationOffset { location = Location.Lake,       offset = new Vector2(24f, 0f) },
```

Add the convenience method after `PanToWoods()`:

```csharp
    public void PanToLake() => PanTo(Location.Lake);
```

- [ ] **Step 2: Create LakeNode**

Create `Assets/Scripts/Fishing/LakeNode.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Lake's clickable water (spec §3a). Tapping casts the line, or collects a fish once one is
/// biting, or shows the "buy a pole first" hint. A bite indicator (a small fish icon speech bubble)
/// hovers over the water while a fish is on the line. Interactable only when the camera is settled
/// at the Lake. Pointer handling mirrors WoodRack/CanneryBuilding.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class LakeNode : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.97f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Header("Bite Indicator")]
    [Tooltip("Local offset from the lake origin where the bite bubble + cast feedback appear.")]
    [SerializeField] private Vector3 bobberOffset = new Vector3(0f, 1.5f, 0f);

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;
    private WorldHintPopup biteIndicator;
    private bool biteShown;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        SyncBiteIndicator();

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

    private void SyncBiteIndicator()
    {
        var fm = FishingManager.Instance;
        bool biting = fm != null && fm.HasBite;
        if (biting && !biteShown)
        {
            // A small fish icon, no words (spec §3a). WorldHintPopup renders TMP text; an emoji is
            // the placeholder icon until dedicated bubble art lands.
            if (biteIndicator != null) Destroy(biteIndicator.gameObject);
            biteIndicator = WorldHintPopup.Create(transform.position + bobberOffset, "🐟");
            biteShown = true;
        }
        else if (!biting && biteShown)
        {
            if (biteIndicator != null) Destroy(biteIndicator.gameObject);
            biteIndicator = null;
            biteShown = false;
        }
    }

    private void HandleClick()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;

        if (!fm.HasPole) { fm.ShowNoPoleHint(transform.position + bobberOffset); return; }

        if (fm.HasBite)
        {
            int tier = fm.Collect();
            if (tier > 0) WorldHintPopup.Create(transform.position + bobberOffset, $"🐟 {FishTiers.Name(tier)}!");
            return;
        }

        if (fm.State == FishingManager.CastState.Idle)
        {
            if (fm.Cast()) WorldHintPopup.Create(transform.position + bobberOffset, "Cast!");
            return;
        }

        // Waiting: gentle reminder, no state change.
        WorldHintPopup.Create(transform.position + bobberOffset, "Waiting for a bite…");
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
        if (pan == null) return true;
        return !pan.IsPanning && pan.CurrentLocation == CameraPanController.Location.Lake;
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

- [ ] **Step 3: Create LakeNavButton**

Open `Assets/Scripts/UI/WoodsNavButton.cs`, read it, and create `Assets/Scripts/Fishing/LakeNavButton.cs` as a byte-for-byte structural clone that calls `PanToLake()` instead of `PanToWoods()` and checks `Location.Lake`. Below is the implementation assuming WoodsNavButton's shape (a uGUI Button that pans on click and reflects the current location); adapt field/method names to match the actual WoodsNavButton you read:

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home-screen button that pans the camera to the Lake. Mirrors WoodsNavButton — if that class has
/// extra behavior (selected-state tint, hide-during-run), copy it here verbatim against Location.Lake.
/// </summary>
[RequireComponent(typeof(Button))]
public class LakeNavButton : MonoBehaviour
{
    [SerializeField] private CameraPanController panController;

    private void Awake()
    {
        if (panController == null && Camera.main != null)
            panController = Camera.main.GetComponent<CameraPanController>();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (panController != null) panController.PanTo(CameraPanController.Location.Lake);
    }
}
```

> **Note:** if `WoodsNavButton` derives from a shared base or wires via UnityEvent in the scene rather than code, follow that same pattern instead of the skeleton above. The requirement is "reach the Lake the same way you reach the Woods."

- [ ] **Step 4: Compile clean.**

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/CameraPanController.cs Assets/Scripts/Fishing/LakeNode.cs Assets/Scripts/Fishing/LakeNavButton.cs
git commit -m "feat(fishing): Lake camera location + LakeNode cast/collect + nav button"
```

---

### Task 8: SmokehouseManager + persistence (shared firebox, fish→smoke→pantry)

**Files:**
- Create: `Assets/Scripts/Smokehouse/SmokehouseManager.cs`
- Modify: `Assets/Scripts/BuildingState.cs`
- Modify: `Assets/Scripts/GameData.cs`
- Modify: `Assets/Scripts/SaveManager.cs`
- Modify: `Assets/Scripts/AutoSaveManager.cs`

**Interfaces:**
- Consumes: `ProcessingMath` + `CanneryState`/`CannerySlot`/`ReadyJar` (via aliases), `PantryManager`, `CurrencyManager`, `BuildingState`, `ToastManager`, `FishTiers`.
- Produces (used by Task 9, 11):
  - `SmokehouseManager.Instance`
  - `bool IsBuilt`, `bool FireLit`, `CanneryState State`, `int FurnaceCapacity`, `float BaseBurnPerHour`, `float PerSlotBurnPerHour`
  - `int SlotsOwned`, `int MaxPurchasableSlots`, `int TotalMaxSlots`, `int NextSlotCoinCost()`, `int NextSlotWoodCost()`, `bool CanBuySlot()`, `bool TryBuySlot()`
  - `bool TryAddFuel(int)`, `int StokeToFinishCost()`, `void StokeToFinish()`, `void FillFurnace()`
  - `int RawValue(int tier)`, `int SmokedValue(int tier)`, `int SmokeHours(int tier)`
  - `bool TryLoadFish(int tier)` (pull 1 raw fish from Pantry into an empty slot)
  - `bool TrySellRaw(int tier)`, `bool TrySellSmoked(int tier)`
  - `event Action OnChanged`
  - `void CaptureTo(GameData d)`, `void LoadFrom(GameData d)`

**Design:** the Smokehouse is the Cannery's twin over the same firebox. A slot holds exactly ONE fish (`unitsRequired = unitsLoaded = 1`, so it cooks immediately). On finish, `ProcessingMath.Simulate` deposits a `ReadyJar` carrying the fish tier; the manager drains those into `PantryManager` smoked counts (never a persisted shelf). Raw + smoked selling reads Pantry counts (Gold only, spec §1). Smoke times/values are rarity-gated per tier (spec §3): 4h/8h/12h, smoked 300/1400/5000g.

- [ ] **Step 1: Add the building key**

In `BuildingState.cs`, after `CanneryKey`:

```csharp
    public const string SmokehouseKey = "building_smokehouse_built";
```

- [ ] **Step 2: Add GameData fields**

In `GameData.cs`, after the fishing fields:

```csharp
    // Smokehouse (Pantry Economy Phase 2). Built-flag in BuildingState (PlayerPrefs); firebox here.
    // Finished smoked fish are drained into Pantry counts, so no ready-shelf is persisted.
    public double smokehouseFuelWood;
    public CannerySlot[] smokehouseSlots;
    public long smokehouseLastSimUtcTicks;
```

In the default constructor, after the pantry defaults:

```csharp
        smokehouseSlots = new CannerySlot[0];
```

- [ ] **Step 3: Create the manager**

Create `Assets/Scripts/Smokehouse/SmokehouseManager.cs`:

```csharp
using System;
using UnityEngine;

// The Smokehouse reuses the Phase 1 firebox core as the generic processing model. These aliases keep
// the code readable without renaming the shipped Cannery* types (whose names appear in save fields).
using ProcessingState = CanneryState;
using ProcessingSlot = CannerySlot;

/// <summary>
/// Owns the Smokehouse: fish slots over the shared firebox, fuel, slot purchases, and raw/smoked
/// fish sales (spec §3). Fish come from the Pantry; smoked output returns to the Pantry as counts.
/// All firebox math is ProcessingMath (shared with the Cannery); this is the Unity-side state +
/// transaction layer, mirroring CanneryManager. UtcNow-anchored → offline catch-up for free.
/// </summary>
public class SmokehouseManager : MonoBehaviour
{
    public static SmokehouseManager Instance { get; private set; }

    [Header("Firebox Tuning (spec §2)")]
    [SerializeField] private float baseBurnPerHour = 5f;
    [SerializeField] private float perSlotBurnPerHour = 20f;
    [SerializeField] private int furnaceCapacity = 1600;

    [Header("Fish Tables (index 0 = tier 1 Perch). Spec §3.")]
    [Tooltip("Raw fish sell value (Gold).")]
    [SerializeField] private int[] rawValue = { 100, 400, 2000 };
    [Tooltip("Smoked fish sell value (Gold).")]
    [SerializeField] private int[] smokedValue = { 300, 1400, 5000 };
    [Tooltip("Hours to smoke one fish of this tier.")]
    [SerializeField] private int[] smokeHours = { 4, 8, 12 };

    [Header("Slots (spec §5a: front-loaded; last 2 research-gated)")]
    [SerializeField] private int startingSlots = 1;
    [Tooltip("Slots beyond this are research-gated (Phase 3), not purchasable.")]
    [SerializeField] private int maxPurchasableSlots = 6;
    [SerializeField] private int totalMaxSlots = 8;
    [SerializeField] private int[] slotCoinCosts = { 600, 1500, 4000, 9000, 18000 };
    [SerializeField] private int[] slotWoodCosts = { 120, 300, 700, 1400, 2600 };

    private readonly ProcessingState state = new ProcessingState();
    private long lastSimUtcTicks;

    public event Action OnChanged;

    public bool IsBuilt => BuildingState.IsBuilt(BuildingState.SmokehouseKey);
    public CanneryState State => state;
    public bool FireLit => IsBuilt && state.fuelWood > 0;
    public int FurnaceCapacity => furnaceCapacity;
    public float BaseBurnPerHour => baseBurnPerHour;
    public float PerSlotBurnPerHour => perSlotBurnPerHour;
    public int SlotsOwned => state.slots.Length;
    public int MaxPurchasableSlots => maxPurchasableSlots;
    public int TotalMaxSlots => totalMaxSlots;

    public int RawValue(int tier) => rawValue[TierIdx(tier)];
    public int SmokedValue(int tier) => smokedValue[TierIdx(tier)];
    public int SmokeHours(int tier) => smokeHours[TierIdx(tier)];

    private static int TierIdx(int tier) => Mathf.Clamp(tier, 1, FishTiers.Count) - 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSlotArray(startingSlots);
    }

    private void Update()
    {
        if (!IsBuilt) return;
        long now = DateTime.UtcNow.Ticks;
        if (lastSimUtcTicks == 0) { lastSimUtcTicks = now; return; }
        double elapsed = (now - lastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
        if (elapsed < 0) { lastSimUtcTicks = now; return; }
        if (elapsed < 0.25) return;
        lastSimUtcTicks = now;
        int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
        int drained = DrainFinishedToPantry();
        if (drained > 0)
        {
            Debug.Log($"[Smokehouse] {drained} fish finished smoking.");
            ToastManager.Show("Smoked fish ready!", "Visit the Smokehouse to sell.");
            OnChanged?.Invoke();
        }
    }

    private void EnsureSlotArray(int count)
    {
        int target = Mathf.Clamp(count, startingSlots, totalMaxSlots);
        if (state.slots.Length >= target) return;
        var next = new ProcessingSlot[target];
        for (int i = 0; i < target; i++)
            next[i] = i < state.slots.Length && state.slots[i] != null ? state.slots[i] : new ProcessingSlot();
        state.slots = next;
    }

    /// <summary>Move every finished good out of the scratch shelf into Pantry smoked counts.</summary>
    private int DrainFinishedToPantry()
    {
        if (state.readyJars.Count == 0) return 0;
        int n = state.readyJars.Count;
        if (PantryManager.Instance != null)
            for (int i = 0; i < n; i++)
                PantryManager.Instance.AddSmoked(state.readyJars[i].tier);
        state.readyJars.Clear();
        return n;
    }

    // ── Load a fish into the smoker (spec §3) ────────────────────────────

    /// <summary>Pull one raw fish of a tier from the Pantry into the first empty slot, cooking now.</summary>
    public bool TryLoadFish(int tier)
    {
        if (!IsBuilt || PantryManager.Instance == null) return false;
        if (PantryManager.Instance.GetRaw(tier) <= 0) return false;

        int idx = -1;
        for (int i = 0; i < state.slots.Length; i++)
            if (ProcessingMath.SlotIsEmpty(state.slots[i])) { idx = i; break; }
        if (idx < 0) return false;

        if (!PantryManager.Instance.SpendRaw(tier)) return false;

        var s = state.slots[idx];
        int t = Mathf.Clamp(tier, 1, FishTiers.Count);
        s.cropId = "fish_" + t;
        s.cropName = FishTiers.SmokedName(t);
        s.tier = t;
        s.unitsRequired = 1;
        s.unitsLoaded = 1;                                   // one fish fills a slot immediately
        s.jarValue = SmokedValue(t);
        s.cookSecondsRemaining = SmokeHours(t) * 3600.0;     // cooking starts now
        OnChanged?.Invoke();
        return true;
    }

    // ── Fuel (identical to CanneryManager) ───────────────────────────────

    public bool TryAddFuel(int amount)
    {
        var cm = CurrencyManager.Instance;
        if (!IsBuilt || cm == null || amount <= 0) return false;
        int space = Mathf.Max(0, furnaceCapacity - Mathf.CeilToInt((float)state.fuelWood));
        int toAdd = Mathf.Min(amount, Mathf.Min(space, cm.Wood));
        if (toAdd <= 0) return false;
        if (!cm.SpendWood(toAdd)) return false;
        state.fuelWood += toAdd;
        OnChanged?.Invoke();
        return true;
    }

    public int StokeToFinishCost()
        => Mathf.CeilToInt((float)ProcessingMath.WoodToFinishLoaded(state, baseBurnPerHour, perSlotBurnPerHour));

    public void StokeToFinish() => TryAddFuel(StokeToFinishCost());

    public void FillFurnace() => TryAddFuel(furnaceCapacity - Mathf.CeilToInt((float)state.fuelWood));

    // ── Slot purchase (in-building, spec §5) ─────────────────────────────

    private int NextSlotCostIndex() => SlotsOwned - startingSlots;

    public int NextSlotCoinCost()
    {
        int i = NextSlotCostIndex();
        if (i < 0 || slotCoinCosts.Length == 0) return int.MaxValue;
        return slotCoinCosts[Mathf.Min(i, slotCoinCosts.Length - 1)];
    }

    public int NextSlotWoodCost()
    {
        int i = NextSlotCostIndex();
        if (i < 0 || slotWoodCosts.Length == 0) return int.MaxValue;
        return slotWoodCosts[Mathf.Min(i, slotWoodCosts.Length - 1)];
    }

    public bool CanBuySlot()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || !IsBuilt) return false;
        return ProcessingMath.CanBuySlot(SlotsOwned, maxPurchasableSlots,
            cm.Coins, NextSlotCoinCost(), cm.Wood, NextSlotWoodCost());
    }

    public bool TryBuySlot()
    {
        if (!CanBuySlot()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextSlotCoinCost();
        int woodCost = NextSlotWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; }
        EnsureSlotArray(SlotsOwned + 1);
        Debug.Log($"[Smokehouse] Slot purchased → {SlotsOwned} slots.");
        OnChanged?.Invoke();
        return true;
    }

    // ── Selling (Gold only, spec §1) ─────────────────────────────────────

    public bool TrySellRaw(int tier)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || PantryManager.Instance == null) return false;
        if (!PantryManager.Instance.SpendRaw(tier)) return false;
        cm.AddCoins(RawValue(tier));
        OnChanged?.Invoke();
        return true;
    }

    public bool TrySellSmoked(int tier)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || PantryManager.Instance == null) return false;
        if (!PantryManager.Instance.SpendSmoked(tier)) return false;
        cm.AddCoins(SmokedValue(tier));
        OnChanged?.Invoke();
        return true;
    }

    // ── Save / load ──────────────────────────────────────────────────────

    public void CaptureTo(GameData d)
    {
        DrainFinishedToPantry(); // never persist the transient shelf
        d.smokehouseFuelWood = state.fuelWood;
        d.smokehouseSlots = state.slots;
        d.smokehouseLastSimUtcTicks = lastSimUtcTicks != 0 ? lastSimUtcTicks : DateTime.UtcNow.Ticks;
    }

    public void LoadFrom(GameData d)
    {
        state.fuelWood = Math.Max(0, d.smokehouseFuelWood);
        state.slots = d.smokehouseSlots != null && d.smokehouseSlots.Length > 0 ? d.smokehouseSlots : new ProcessingSlot[0];
        for (int i = 0; i < state.slots.Length; i++)
            if (state.slots[i] == null) state.slots[i] = new ProcessingSlot();
        EnsureSlotArray(startingSlots);
        state.readyJars.Clear();

        long now = DateTime.UtcNow.Ticks;
        if (IsBuilt && d.smokehouseLastSimUtcTicks > 0 && now > d.smokehouseLastSimUtcTicks)
        {
            double elapsed = (now - d.smokehouseLastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
            ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
            int drained = DrainFinishedToPantry();
            if (drained > 0)
                ToastManager.Show($"{drained} fish finished smoking while you were away!", "Visit the Smokehouse to sell.");
        }
        lastSimUtcTicks = now;
        OnChanged?.Invoke();
    }
}
```

- [ ] **Step 4: Wire SaveManager** (after the FishingManager capture/load):

```csharp
        if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.CaptureTo(data);
```
```csharp
                if (SmokehouseManager.Instance != null)
                    SmokehouseManager.Instance.LoadFrom(data);
```

> **Load order matters:** `SmokehouseManager.LoadFrom` deposits offline-smoked fish into the Pantry, so it must run **after** `PantryManager.LoadFrom` (which just overwrote Pantry counts from the save). The order above (Pantry Task 4, then Fishing Task 5, then Smokehouse) satisfies this. Verify the three load calls sit in that sequence.

- [ ] **Step 5: Wire AutoSaveManager** (same `OnVoid` handler, after FishingManager):

```csharp
        if (SmokehouseManager.Instance != null)
            SmokehouseManager.Instance.OnChanged += OnVoid;
```
```csharp
        if (SmokehouseManager.Instance != null)
            SmokehouseManager.Instance.OnChanged -= OnVoid;
```

- [ ] **Step 6: Compile clean; run all EditMode tests** (still green).

- [ ] **Step 7: Commit**

```
git add Assets/Scripts/Smokehouse/SmokehouseManager.cs Assets/Scripts/BuildingState.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs
git commit -m "feat(smokehouse): SmokehouseManager over shared firebox + persistence"
```

---

### Task 9: Carpenter "Build Smokehouse" row + SmokehouseBuilding world prop

**Files:**
- Modify: `Assets/Scripts/UI/CarpenterPopupUITK.cs`
- Create: `Assets/Scripts/Smokehouse/SmokehouseBuilding.cs`

**Interfaces:**
- Consumes: `BuildingState.SmokehouseKey`, `CurrencyManager`, `SmokehouseManager` (for `FireLit`), `SmokehousePopupUITK` (Task 11).
- Produces: nothing new.

- [ ] **Step 1: Add serialized fields** in `CarpenterPopupUITK.cs` after the Cannery project fields (~line 27):

```csharp
    [Header("Smokehouse Project (Pantry Economy Phase 2)")]
    [SerializeField] private string smokehouseTitle = "Build Smokehouse";
    [TextArea]
    [SerializeField] private string smokehouseDescription =
        "A wood-fired smoker. Smoke fish caught at the Lake into far pricier goods — the rare ones pay a fortune.";
    [SerializeField] private int smokehouseCoinCost = 1200;
    [SerializeField] private int smokehouseWoodCost = 450;
```

- [ ] **Step 2: Add the row to `Refresh()`** after `BuildCanneryRow();`:

```csharp
        BuildSmokehouseRow();
```

- [ ] **Step 3: Add the builder method** after `BuildCanneryRow()` (identical shape to the Cannery row, against the Smokehouse key/costs):

```csharp
    private void BuildSmokehouseRow()
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label(smokehouseTitle);
        title.AddToClassList("market-row-title");
        Label desc = new Label(smokehouseDescription);
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

        bool built = BuildingState.IsBuilt(BuildingState.SmokehouseKey);
        var cm = CurrencyManager.Instance;
        bool canAfford = cm != null && cm.CanAffordCoins(smokehouseCoinCost) && cm.CanAffordWood(smokehouseWoodCost);

        if (built)
        {
            row.AddToClassList("market-row--owned");
            status.text = "✓ Built";
            cost.text = "";
        }
        else if (canAfford)
        {
            row.AddToClassList("market-row--buy");
            status.text = "BUILD";
            cost.text = $"{FormatCoinCost(smokehouseCoinCost)} + {smokehouseWoodCost} wood";
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var c = CurrencyManager.Instance;
                if (c == null) return;
                if (!c.SpendCoins(smokehouseCoinCost)) return;
                if (!c.SpendWood(smokehouseWoodCost)) { c.AddCoins(smokehouseCoinCost); return; }
                BuildingState.MarkBuilt(BuildingState.SmokehouseKey);
                Debug.Log("[Carpenter] Smokehouse built.");
            });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
            cost.text = $"{FormatCoinCost(smokehouseCoinCost)} + {smokehouseWoodCost} wood";
        }

        rowsList.Add(row);
    }
```

- [ ] **Step 4: Create SmokehouseBuilding** — a structural clone of `CanneryBuilding.cs` against the Smokehouse key/manager/popup.

Create `Assets/Scripts/Smokehouse/SmokehouseBuilding.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clickable Smokehouse building in the world (between the Lake and the Woods, spec §8). Hidden
/// until built (BuildingState); shows the smoke child while the fire is lit; tap opens the popup.
/// Mirrors CanneryBuilding.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class SmokehouseBuilding : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    [Header("Smoke")]
    [SerializeField] private GameObject smokeObject;

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
        if (smokeObject == null)
        {
            Transform smoke = transform.Find("Smoke");
            if (smoke != null) smokeObject = smoke.gameObject;
        }
    }

    private void Start()
    {
        ApplyBuiltVisibility();
        BuildingState.OnBuildingBuilt += OnBuilt;
    }

    private void OnDestroy() => BuildingState.OnBuildingBuilt -= OnBuilt;

    private void OnBuilt(string key)
    {
        if (key == BuildingState.SmokehouseKey) ApplyBuiltVisibility();
    }

    private void ApplyBuiltVisibility()
    {
        bool built = BuildingState.IsBuilt(BuildingState.SmokehouseKey);
        spriteRenderer.enabled = built;
        ownCollider.enabled = built;
        if (!built && smokeObject != null) smokeObject.SetActive(false);
    }

    private void Update()
    {
        if (smokeObject != null && SmokehouseManager.Instance != null)
            smokeObject.SetActive(SmokehouseManager.Instance.FireLit);

        if (!spriteRenderer.enabled) return;
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
        if (pan == null) return true;
        return !pan.IsPanning; // sits between the Lake and the Woods — reachable from either framing
    }

    private void HandleClick()
    {
        if (SmokehousePopupUITK.Instance != null) SmokehousePopupUITK.Instance.Open();
        else Debug.Log("[SmokehouseBuilding] Clicked — no SmokehousePopupUITK in scene.");
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

- [ ] **Step 5: Compile clean.** (`SmokehousePopupUITK` doesn't exist until Task 11; if the compiler errors on the reference, do Task 11 before compiling this — or temporarily guard with `#if false`. Cleanest: implement Task 11 next, then compile both. The commit for this task can follow Task 11's compile.)

- [ ] **Step 6: Commit**

```
git add Assets/Scripts/UI/CarpenterPopupUITK.cs Assets/Scripts/Smokehouse/SmokehouseBuilding.cs
git commit -m "feat(smokehouse): Carpenter 'Build Smokehouse' row + world building prop"
```

---

### Task 10: Smokehouse popup — UXML, USS, controller

**Files:**
- Create: `Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uxml`
- Create: `Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uss`
- Create: `Assets/Scripts/UI/SmokehousePopupUITK.cs`

**Interfaces:**
- Consumes: `SmokehouseManager`, `PantryManager`, `CurrencyManager`, `FishTiers`, `ProcessingMath`.
- Produces: `SmokehousePopupUITK.Instance.Open()` (used by Task 9's world prop).

**UI contents (spec §3):** firebox gauge + "lasts Xh Ym" + Stoke-to-finish / Fill-furnace; **Raw Fish** rows (per tier: name × count, a "Smoke" button that loads a slot, a "Sell +Ng" button); **Smoker** slot rows (fish + countdown / PAUSED / empty); **Smoked Fish** rows (per tier: name × count, "Sell +Ng"); buy-next-slot row + research-gate hint. Lifecycle mirrors CanneryPopupUITK (1s ticker + event refresh).

- [ ] **Step 1: Copy the Cannery USS as the base**

Copy `Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uss` verbatim to `Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uss` (it defines `popup-root`, `backdrop`, `header`, `fuel-bar`/`fuel-fill`, `section-title`, `slot-row*`, `shelf-row`, `buy-slot-row`, `sell-btn*`, etc. — all reused). Then append these Smokehouse-specific additions to the end of the copied file:

```css
/* ── Smokehouse fish rows ─────────────────────────────────────────── */
.fish-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 6px 10px;
    margin-bottom: 4px;
    border-radius: 8px;
    background-color: rgba(0, 0, 0, 0.18);
}
.fish-row-label {
    color: rgb(238, 232, 220);
    font-size: 26px;
    flex-grow: 1;
}
.fish-row-buttons { flex-direction: row; }
.fish-btn {
    font-size: 22px;
    padding: 4px 12px;
    margin-left: 6px;
    border-radius: 8px;
    color: rgb(20, 20, 20);
    background-color: rgb(226, 178, 92);
}
.fish-btn--smoke { background-color: rgb(198, 156, 109); }
.fish-btn:disabled { opacity: 0.4; }
```

- [ ] **Step 2: Create the UXML**

`Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="SmokehousePopupUITK.uss" />
    <ui:VisualElement name="popup-root" class="popup-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="backdrop" picking-mode="Position" />
        <ui:VisualElement name="popup-container" class="popup-container">
            <ui:VisualElement name="header" class="header">
                <ui:Label name="header-title" text="Smokehouse" class="header-title" />
                <ui:Button name="close-button" class="close-button" text="X" />
            </ui:VisualElement>
            <ui:VisualElement name="body" class="body">
                <ui:Label name="fuel-title" text="🔥 Firebox" class="section-title" />
                <ui:VisualElement name="fuel-bar" class="fuel-bar">
                    <ui:VisualElement name="fuel-fill" class="fuel-fill" />
                </ui:VisualElement>
                <ui:Label name="fuel-text" text="Fuel: 0" class="fuel-text" />
                <ui:VisualElement name="fuel-buttons" class="stack-row">
                    <ui:Button name="stoke-button" text="Stoke to finish" class="sell-btn sell-btn--cash" />
                    <ui:Button name="fill-button" text="Fill furnace" class="sell-btn sell-btn--cash" />
                </ui:VisualElement>
                <ui:Label name="raw-title" text="Raw Fish" class="section-title" />
                <ui:ScrollView name="raw-list" class="slots-list" />
                <ui:Label name="slots-title" text="Smoker" class="section-title" />
                <ui:ScrollView name="slots-list" class="slots-list" />
                <ui:Label name="smoked-title" text="Smoked Fish" class="section-title" />
                <ui:ScrollView name="smoked-list" class="shelf-list" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: Create the controller**

Create `Assets/Scripts/UI/SmokehousePopupUITK.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Smokehouse panel: firebox gauge + stoke/fill, raw-fish rows (smoke / sell), smoker slots,
/// smoked-fish rows (sell), in-building slot purchases. Lifecycle mirrors CanneryPopupUITK —
/// rebuilds on a 1s schedule while open (live countdowns) and on manager/pantry change events.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class SmokehousePopupUITK : MonoBehaviour
{
    public static SmokehousePopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root, popupRoot, fuelFill;
    private Label headerLabel, fuelText;
    private Button closeButton, stokeButton, fillButton;
    private ScrollView rawList, slotsList, smokedList;

    private bool isOpen;
    private bool eventsSubscribed;
    private IVisualElementScheduledItem ticker;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable() { CacheElements(); WireCallbacks(); TrySubscribeEvents(); }
    private void Start() { if (root == null) { CacheElements(); WireCallbacks(); } }
    private void OnDisable() => UnsubscribeEvents();

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        if (SmokehouseManager.Instance != null)
        {
            SmokehouseManager.Instance.OnChanged += OnVoidChanged;
            eventsSubscribed = true;
        }
        if (PantryManager.Instance != null) PantryManager.Instance.OnChanged += OnVoidChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged += OnInt;
            CurrencyManager.Instance.OnCoinsChanged += OnInt;
        }
    }

    private void UnsubscribeEvents()
    {
        if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.OnChanged -= OnVoidChanged;
        if (PantryManager.Instance != null) PantryManager.Instance.OnChanged -= OnVoidChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged -= OnInt;
            CurrencyManager.Instance.OnCoinsChanged -= OnInt;
        }
        eventsSubscribed = false;
    }

    private void OnVoidChanged() { if (isOpen) Refresh(); }
    private void OnInt(int _) { if (isOpen) Refresh(); }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[SmokehousePopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        headerLabel = root.Q<Label>("header-title");
        closeButton = root.Q<Button>("close-button");
        fuelFill    = root.Q<VisualElement>("fuel-fill");
        fuelText    = root.Q<Label>("fuel-text");
        stokeButton = root.Q<Button>("stoke-button");
        fillButton  = root.Q<Button>("fill-button");
        rawList     = root.Q<ScrollView>("raw-list");
        slotsList   = root.Q<ScrollView>("slots-list");
        smokedList  = root.Q<ScrollView>("smoked-list");

        if (headerLabel != null) headerLabel.text = "Smokehouse";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (stokeButton != null) stokeButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.StokeToFinish();
        });
        if (fillButton != null) fillButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.FillFurnace();
        });
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
        Refresh();
        ticker = root.schedule.Execute(() => { if (isOpen) Refresh(); }).Every(1000);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (ticker != null) { ticker.Pause(); ticker = null; }
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "0m";
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds:00}s";
        return $"{t.Seconds}s";
    }

    private void Refresh()
    {
        var mgr = SmokehouseManager.Instance;
        var cm = CurrencyManager.Instance;
        if (mgr == null || slotsList == null) return;
        var st = mgr.State;

        int cooking = ProcessingMath.CountCooking(st);
        double ratePerSec = ProcessingMath.BurnRatePerSecond(cooking, mgr.BaseBurnPerHour, mgr.PerSlotBurnPerHour);
        if (fuelFill != null)
            fuelFill.style.width = Length.Percent(Mathf.Clamp01((float)(st.fuelWood / mgr.FurnaceCapacity)) * 100f);
        if (fuelText != null)
        {
            string lasts = ratePerSec > 0 && st.fuelWood > 0
                ? $" — lasts {FormatDuration(st.fuelWood / ratePerSec)} at current load"
                : (st.fuelWood <= 0 ? " — fire is OUT" : "");
            fuelText.text = $"Fuel: {Mathf.FloorToInt((float)st.fuelWood)}/{mgr.FurnaceCapacity}{lasts}";
        }

        int stokeCost = mgr.StokeToFinishCost();
        int wood = cm != null ? cm.Wood : 0;
        if (stokeButton != null)
        {
            stokeButton.text = stokeCost > 0 ? $"Stoke to finish  −{stokeCost} wood" : "Stoke to finish  ✓";
            stokeButton.SetEnabled(stokeCost > 0 && wood > 0);
        }
        if (fillButton != null)
        {
            int space = mgr.FurnaceCapacity - Mathf.CeilToInt((float)st.fuelWood);
            int fillAmount = Mathf.Min(space, wood);
            fillButton.text = $"Fill furnace  −{Mathf.Max(0, fillAmount)} wood";
            fillButton.SetEnabled(fillAmount > 0);
        }

        RebuildRawRows(mgr);
        RebuildSlotRows(mgr, st);
        RebuildSmokedRows(mgr);
    }

    private void RebuildRawRows(SmokehouseManager mgr)
    {
        rawList.Clear();
        var pantry = PantryManager.Instance;
        bool anyEmptySlot = false;
        foreach (var s in mgr.State.slots) if (ProcessingMath.SlotIsEmpty(s)) { anyEmptySlot = true; break; }

        for (int tier = 1; tier <= FishTiers.Count; tier++)
        {
            int count = pantry != null ? pantry.GetRaw(tier) : 0;
            var row = new VisualElement();
            row.AddToClassList("fish-row");
            var label = new Label($"{FishTiers.Name(tier)}  ×{count}");
            label.AddToClassList("fish-row-label");
            var buttons = new VisualElement();
            buttons.AddToClassList("fish-row-buttons");

            int t = tier;
            var smoke = new Button { text = "Smoke" };
            smoke.AddToClassList("fish-btn");
            smoke.AddToClassList("fish-btn--smoke");
            smoke.SetEnabled(count > 0 && anyEmptySlot);
            smoke.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TryLoadFish(t); });

            var sell = new Button { text = $"Sell +{mgr.RawValue(tier)}" };
            sell.AddToClassList("fish-btn");
            sell.SetEnabled(count > 0);
            sell.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TrySellRaw(t); });

            buttons.Add(smoke);
            buttons.Add(sell);
            row.Add(label);
            row.Add(buttons);
            rawList.Add(row);
        }
    }

    private void RebuildSlotRows(SmokehouseManager mgr, CanneryState st)
    {
        slotsList.Clear();
        bool fireOut = st.fuelWood <= 0;
        for (int i = 0; i < st.slots.Length; i++)
        {
            var s = st.slots[i];
            var row = new VisualElement();
            row.AddToClassList("slot-row");
            var label = new Label();
            label.AddToClassList("slot-row-label");
            var state = new Label();
            state.AddToClassList("slot-row-state");

            if (ProcessingMath.SlotIsEmpty(s))
            {
                row.AddToClassList("slot-row--empty");
                label.text = $"Slot {i + 1} — empty";
                state.text = "";
            }
            else
            {
                row.AddToClassList(fireOut ? "slot-row--paused" : "slot-row--cooking");
                label.text = s.cropName;
                state.text = fireOut ? "PAUSED — no fuel" : FormatDuration(s.cookSecondsRemaining);
            }
            row.Add(label);
            row.Add(state);
            slotsList.Add(row);
        }

        if (mgr.SlotsOwned < mgr.MaxPurchasableSlots)
        {
            var buyRow = new VisualElement();
            buyRow.AddToClassList("buy-slot-row");
            var t = new Label($"Add Slot {mgr.SlotsOwned + 1}");
            t.AddToClassList("slot-row-label");
            var c = new Label($"{mgr.NextSlotCoinCost()} coins + {mgr.NextSlotWoodCost()} wood");
            c.AddToClassList("slot-row-state");
            buyRow.Add(t);
            buyRow.Add(c);
            if (mgr.CanBuySlot())
                buyRow.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TryBuySlot(); });
            else
                buyRow.AddToClassList("buy-slot-row--locked");
            slotsList.Add(buyRow);
        }
        else if (mgr.SlotsOwned < mgr.TotalMaxSlots)
        {
            var hint = new Label($"Slots {mgr.MaxPurchasableSlots + 1}–{mgr.TotalMaxSlots} require Research.");
            hint.AddToClassList("slot-row-state");
            slotsList.Add(hint);
        }
    }

    private void RebuildSmokedRows(SmokehouseManager mgr)
    {
        smokedList.Clear();
        var pantry = PantryManager.Instance;
        int totalCount = 0;
        for (int tier = 1; tier <= FishTiers.Count; tier++)
        {
            int count = pantry != null ? pantry.GetSmoked(tier) : 0;
            totalCount += count;
            var row = new VisualElement();
            row.AddToClassList("fish-row");
            var label = new Label($"{FishTiers.SmokedName(tier)}  ×{count}");
            label.AddToClassList("fish-row-label");
            int t = tier;
            var sell = new Button { text = $"Sell +{mgr.SmokedValue(tier)}" };
            sell.AddToClassList("fish-btn");
            sell.SetEnabled(count > 0);
            sell.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TrySellSmoked(t); });
            row.Add(label);
            row.Add(sell);
            smokedList.Add(row);
        }
        if (totalCount == 0)
        {
            var empty = new Label("Nothing smoked yet.");
            empty.AddToClassList("slot-row-state");
            smokedList.Add(empty);
        }
    }
}
```

- [ ] **Step 4: Compile clean; run all EditMode tests** (still green).

- [ ] **Step 5: Commit**

```
git add Assets/UI/SmokehousePopupUITK Assets/Scripts/UI/SmokehousePopupUITK.cs
git commit -m "feat(smokehouse): UITK popup - firebox, raw/smoked fish rows, smoker slots"
```

---

### Task 11: Scene wiring via Unity MCP

**Files:** Scene `SampleScene` (all changes via MCP tools — never text-edit `.unity`).

**Goal:** put the new managers, world props, camera location, and UI documents into the scene so the systems actually run. Mirror how Phase 1 wired the Cannery (manager GO + popup UIDocument GO + world prop at 1.25 scale + smoke child). Use `mcp__gladekit-unity__get_scene_hierarchy` first to find the existing `CanneryManager`, `CanneryPopupUITK`, `Cannery` prop, `WoodRack`, and the Managers/UI parent objects to clone placement from.

- [ ] **Step 1: Managers.** Create three empty GameObjects (siblings of `CanneryManager`) and add the components:
  - `PantryManager` → `PantryManager` component.
  - `FishingManager` → `FishingManager` component.
  - `SmokehouseManager` → `SmokehouseManager` component.
  Leave serialized tuning at code defaults for now.

- [ ] **Step 2: Smokehouse popup UIDocument.** Duplicate the `CanneryPopupUITK` scene object (a GameObject with a `UIDocument` + `CanneryPopupUITK`); on the copy, remove `CanneryPopupUITK`, add `SmokehousePopupUITK`, and set the `UIDocument.visualTreeAsset` to `Assets/UI/SmokehousePopupUITK/SmokehousePopupUITK.uxml` (and the same shared PanelSettings the Cannery popup uses). Confirm via `get_gameobject_components`.

- [ ] **Step 3: Lake location + water + LakeNode.**
  - On the Main Camera's `CameraPanController`, confirm the `Location.Lake` offset entry exists (Task 7 added a code default of `(24,0)`); adjust the vector in the inspector so the Lake frames cleanly to the right of the Farm, farther than the Market. Use `manage_gameobject`/component property set.
  - Create a `Lake` water GameObject at the Lake framing (placeholder sprite is fine — reuse an existing blue/water sprite or a tinted square), give it a `SpriteRenderer` + a `BoxCollider2D` sized to the water, and add the `LakeNode` component.
  - Add a `LakeNavButton` to the home-screen nav bar next to the Woods nav button (duplicate the Woods nav button object, swap the component, wire its `panController` reference to Main Camera's `CameraPanController`). If nav buttons are wired via UnityEvent in the scene rather than code, point the duplicated button's onClick at `CameraPanController.PanToLake` instead.

- [ ] **Step 4: Smokehouse world prop.** Duplicate the `Cannery` world prop object (built-gated sprite + `Smoke` child + collider), place it between the Lake and the Woods (spec §8), remove `CanneryBuilding`, add `SmokehouseBuilding`, and confirm its `Smoke` child auto-resolves (named "Smoke") or wire `smokeObject`. Keep the same ~1.25 world scale Phase 1 settled on for the Cannery. It stays hidden until built (BuildingState gating handles this at runtime).

- [ ] **Step 5: Compile + save the scene** via MCP (`save_scene`). Enter Play mode briefly and check the console (`read_console`) for null-refs from the new objects; fix wiring if any.

- [ ] **Step 6: Commit**

```
git add Assets/Scenes/SampleScene.unity
git commit -m "feat(phase2): scene wiring - Lake location + managers + Smokehouse prop + popup"
```

---

### Task 12: Full-loop smoke test + verification

**Files:** none (verification only; may add a `Tools > Smokehouse`/`Tools > Fishing` dev menu if helpful, mirroring `Assets/Editor/CanneryDevMenu.cs`).

- [ ] **Step 1: Run the full EditMode suite** via the MCP test tool or the `EditModeTestBridge` file trigger. Expected: all previously-green tests plus the new `FishTiersTests` (3), `FishingMathTests` (4), and the added `ProcessingMathTests` case PASS. Record the count in the commit / status memory.

- [ ] **Step 2: Play-mode fishing loop.** Grant coins via the dev menu / Cannery dev menu currency grant. In Play mode: open Carpenter → buy pole (coins-only). Pan to the Lake, tap water → "Cast!". To avoid a 20-min wait, temporarily set `FishingManager.poleTiers[0].biteAvgSeconds` low (e.g. 3) in the inspector, or add a dev-menu "Force bite" that calls a test hook. Confirm the 🐟 bite indicator appears, tap to collect, and the Pantry raw count increments (check the Smokehouse popup's Raw Fish row after building it, or log).

- [ ] **Step 3: Play-mode smoke loop.** Buy coins+wood, build the Smokehouse at the Carpenter (confirm the world prop appears). Open it: Raw Fish rows show caught fish; press **Smoke** on one → a smoker slot starts cooking with a countdown; **Fill furnace** → fuel gauge fills and the countdown ticks. Fast-forward (temporarily shorten `smokeHours` or add a dev "advance cooking" like the Cannery menu) → the fish finishes, a toast fires, and it appears under **Smoked Fish** with a Sell button. Sell it → Coins increase by the smoked value. Sell a raw fish → Coins increase by the raw value.

- [ ] **Step 4: Persistence.** With a line cast (Waiting), a fish smoking, and some Pantry counts, trigger a save (or app-pause), then stop and re-enter Play. Confirm: pole level/ownership restored, the cast line resumes (and if its bite time elapsed, it's biting), smoker slots + fuel restored, Pantry raw/smoked counts restored. Leave the Smokehouse smoking, quit for a real minute with a short `smokeHours`, reopen → the offline catch-up deposits the finished smoked fish into the Pantry with a welcome-back toast.

- [ ] **Step 5: Economy sanity (spec anchor).** Confirm burning still beats selling: one Perch smoked (300g) for 4h × 20 wood/h = 80 wood → 220g premium over selling raw (100g) ≈ 2.75g/wood burned, above the 2.5g floor and far above the 1g rack price. Spot-check Bass/Pike ratios against spec §3. Note any knob that violates "burning beats selling" for a tuning pass — do NOT ship a table that inverts the anchor.

- [ ] **Step 6: Restore any temporarily-lowered tuning** (bite average, smoke hours) to the shipped defaults. Compile clean.

- [ ] **Step 7: Commit + update memory**

```
git add -A
git commit -m "test(phase2): full fishing + smokehouse loop smoke-tested; restore tuning"
```

Update `project_pantry_economy.md`: mark **Phase 2 (Lake + Pole + Smokehouse) BUILT**, note the test count, the deviations (Pantry counts vs shelf; cast meter deferred), and that Phase 3 (QOL intake ladder + research gates) remains.

---

## Self-Review

**Spec coverage:**
- §1 Pantry (counted fish stacks, Gold-only sell) → Tasks 4, 8, 10. Jars-vs-counts deviation documented.
- §3 Smokehouse jackpot ladder (rarity tiers, premiums, slots 1→8 last-2 research-gated) → Tasks 8 (tables + slot caps), 10 (UI).
- §3a Fishing (pole mirrors axe, coins-only first, ~20min UtcNow bite, no minigame, one line, manual collect, bite indicator) → Tasks 3, 5, 6, 7. Cast distance meter deferred (documented).
- §5 progression (Carpenter Construction builds shell; slots in-building) → Tasks 8, 9. (Research kickoff/gates are §5.1 = Phase 3, out of scope per §10.)
- §5a front-loaded slot curve → Task 8 slot cost arrays + purchasable/total caps.
- §6 wood budget → burn rate reuses Phase 1 knobs; no new mechanic needed here.
- §8 world layout (Lake right of farm, Smokehouse between Lake and Woods, clickable + chimney smoke) → Tasks 7, 9, 11.
- §11 architecture (shared ProcessingMath core, thin managers, GameData persistence, UtcNow offline) → all tasks; alias approach documented in lieu of a rename.

**Placeholder scan:** every code step contains complete code; scene steps name exact components/assets; no "TBD"/"similar to". The two "read the neighbor file and mirror it" steps (LakeNavButton in Task 7, USS copy in Task 10) give a concrete skeleton + the exact source file to copy — not a placeholder.

**Type consistency:** `ReadyJar.tier`/`sourceId` defined in Task 1 and consumed in Task 8 (`DrainFinishedToPantry` reads `.tier`). `PantryManager` API (Task 4) matches all call sites (Tasks 5, 8, 10). `SmokehouseManager` public surface (Task 8) matches the popup (Task 10) and building (Task 9). `FishingManager.CastState` + `Cast()/Collect()/HasBite` (Task 5) match `LakeNode` (Task 7). `CameraPanController.Location.Lake` + `PanToLake()` (Task 7) match `LakeNode.CanInteract` and `LakeNavButton`.

---

## Execution Handoff

See below — I'll present the execution options after saving.
