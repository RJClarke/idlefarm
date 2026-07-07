# Pantry Economy Phase 1 — Firebox Core + Cannery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the wood-burning processing core (`ProcessingMath`) and the Cannery — the first
infinite wood sink: harvests divert into jars mid-run, a shared firebox burns Wood in real time,
finished jars sell for Coins.

**Architecture:** Pure decision/simulation logic in the existing **EconomyCore** assembly
(`ProcessingMath`, mirroring `WoodcuttingMath`), consumed by a thin `CanneryManager` MonoBehaviour
singleton (mirroring `WoodcuttingManager`). UI is a code-driven UITK popup (mirroring
`WoodRackPopupUITK`/`CarpenterPopupUITK`). Persistence via `GameData` JSON + `SaveManager`
post-construction assignment; built-flag via `BuildingState` (PlayerPrefs, like Greenhouse).
UtcNow-anchored simulation gives offline catch-up for free.

**Tech Stack:** Unity 6000.3 (6000.3.9f1), C#, UI Toolkit, NUnit EditMode tests, LeanTween,
new Input System, Unity MCP (gladekit) for all scene work.

**Spec:** `docs/superpowers/specs/2026-07-07-pantry-economy-design.md` (§1, §2, §4, §4a, §5, §5a, §7, §11)

## Global Constraints

- **Never edit `.unity`, `.prefab`, `.asset`, `.meta` files as text** — scene/asset work goes through Unity MCP tools; `.cs`, `.uxml`, `.uss` files are fine to create/edit directly.
- **New Input System only** — `Mouse.current` / `Touchscreen.current`, never `Input.*`.
- Pure logic goes in the **EconomyCore** assembly (`Assets/Scripts/EconomyCore/`); MonoBehaviours in Assembly-CSharp (`Assets/Scripts/...`). Do NOT create new asmdefs.
- EditMode tests live in `Assets/Tests/EditMode/` (asmdef `IdleFarm.EditModeTests`, already references EconomyCore).
- USS: Unity 6000.3 does **not** support `:last-child`.
- All tuning numbers below are starting knobs, serialized in the inspector where possible.
- Debug.Log only for important events (purchases, jar completion); LogWarning/LogError freely.
- After each script change: run `mcp__gladekit-unity__compile_scripts` until `hasErrors:false`. Domain reload may drop the MCP bridge briefly ("ReadError"/"connection attempts failed") — retry after ~10s.
- Run EditMode tests via the Unity MCP test tool if one responds (try `mcp__gladekit-unity__run_tests`; extended tools are callable even when unlisted). If none exists, ask the user to run Window → General → Test Runner → EditMode and report results before the final task completes. Compilation cleanliness is NOT a substitute for the math tests.
- Commit after each task with the trailer:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- **Deliberate deviation from spec §1 (record here, not a bug):** no separate `PantryManager` in Phase 1. Finished jars live on the Cannery's ready shelf (its state + UI). The global multi-goods Pantry arrives in Phase 2 when fish need it; jar save fields stay under cannery-prefixed names permanently (jars only ever come from the Cannery), so no migration will be needed.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/EconomyCore/ProcessingMath.cs` | Create | Pure firebox sim + jar/tier/intake/slot-purchase math + serializable state types |
| `Assets/Tests/EditMode/ProcessingMathTests.cs` | Create | Unit tests for all of the above |
| `Assets/Scripts/Cannery/CanneryManager.cs` | Create | Singleton: state, UtcNow tick, intake/fuel/buy/sell APIs, save capture/load |
| `Assets/Scripts/Cannery/CanneryBuilding.cs` | Create | Clickable world prop + smoke toggle (WoodRack pattern) |
| `Assets/Scripts/UI/CanneryPopupUITK.cs` | Create | UITK popup controller (WoodRack/Carpenter pattern) |
| `Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uxml` | Create | Popup layout |
| `Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uss` | Create | Popup styles (cloned from WoodRack + additions) |
| `Assets/Scripts/BuildingState.cs` | Modify | Add `CanneryKey` |
| `Assets/Scripts/GameData.cs` | Modify | Add cannery save fields |
| `Assets/Scripts/SaveManager.cs` | Modify | Capture/load cannery state |
| `Assets/Scripts/AutoSaveManager.cs` | Modify | Subscribe `CanneryManager.OnChanged` |
| `Assets/Scripts/Plant.cs` | Modify (`Harvest()`, ~line 306) | Intake diversion hook |
| `Assets/Scripts/FloatingTextManager.cs` | Modify | Generic text label for "→ Cannery" pop |
| `Assets/Scripts/UI/CarpenterPopupUITK.cs` | Modify | "Build Cannery" construction row (coins+wood) |
| Scene `SampleScene` | Modify via MCP | CanneryManager GO, popup GO + UIDocument, world Cannery prop + smoke child |
| `WoodTreeData` SO assets | Modify via MCP | Regrow stretch: softwood 40→180s, hardwood 80→360s (spec §7) |

---

### Task 1: ProcessingMath — types, tier math, intake routing, slot purchase

**Files:**
- Create: `Assets/Scripts/EconomyCore/ProcessingMath.cs`
- Create: `Assets/Tests/EditMode/ProcessingMathTests.cs`

**Interfaces (Produces):**
- `CannerySlot` (Serializable): `string cropId, cropName; int tier, unitsLoaded, unitsRequired; double cookSecondsRemaining; int jarValue`
- `ReadyJar` (Serializable): `string cropName; int value`
- `CanneryState`: `CannerySlot[] slots; double fuelWood; List<ReadyJar> readyJars`
- `ProcessingMath.CookHoursForTier(int) : int` — 4/8/12, tier clamped 1–3
- `ProcessingMath.UnitsRequiredForTier(int) : int` — equals cook hours (1 unit per cook-hour, spec §4)
- `ProcessingMath.JarValue(int baseUnitValue, int tier, float multiplier) : int`
- `ProcessingMath.SlotIsEmpty(CannerySlot) : bool`, `SlotIsCooking(CannerySlot) : bool`, `CountCooking(CanneryState) : int`
- `ProcessingMath.FindIntakeSlot(CanneryState, string cropId) : int` — partial same-crop jar first, else first empty, else −1
- `ProcessingMath.CanBuySlot(int slotsOwned, int maxPurchasable, int coins, int coinCost, int wood, int woodCost) : bool`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/ProcessingMathTests.cs`:

```csharp
using NUnit.Framework;

public class ProcessingMathTests
{
    // ── Tier math ──────────────────────────────────────────────────────

    [Test]
    public void CookHours_And_Units_ScaleWithTier_Clamped()
    {
        Assert.AreEqual(4,  ProcessingMath.CookHoursForTier(1));
        Assert.AreEqual(8,  ProcessingMath.CookHoursForTier(2));
        Assert.AreEqual(12, ProcessingMath.CookHoursForTier(3));
        Assert.AreEqual(4,  ProcessingMath.CookHoursForTier(0));   // clamped up
        Assert.AreEqual(12, ProcessingMath.CookHoursForTier(99));  // clamped down
        // 1 unit per cook-hour (spec §4): jam=4, compote=8, sauce=12
        Assert.AreEqual(4,  ProcessingMath.UnitsRequiredForTier(1));
        Assert.AreEqual(12, ProcessingMath.UnitsRequiredForTier(3));
    }

    [Test]
    public void JarValue_IsUnitsTimesBaseTimesMultiplier_MinOne()
    {
        // strawberry-ish: base 10, tier 1, x2.5 → 10 * 4 * 2.5 = 100
        Assert.AreEqual(100, ProcessingMath.JarValue(10, 1, 2.5f));
        // tier 3: 10 * 12 * 2.8 = 336
        Assert.AreEqual(336, ProcessingMath.JarValue(10, 3, 2.8f));
        // never below 1, never negative-driven
        Assert.AreEqual(1, ProcessingMath.JarValue(0, 1, 2.5f));
        Assert.AreEqual(1, ProcessingMath.JarValue(-5, 1, 2.5f));
    }

    // ── Intake routing ─────────────────────────────────────────────────

    private static CanneryState MakeState(int slots)
    {
        var st = new CanneryState { slots = new CannerySlot[slots] };
        for (int i = 0; i < slots; i++) st.slots[i] = new CannerySlot();
        return st;
    }

    private static void LoadSlot(CannerySlot s, string cropId, int loaded, int required, double cookRemaining = 0)
    {
        s.cropId = cropId; s.cropName = cropId; s.tier = 1;
        s.unitsLoaded = loaded; s.unitsRequired = required;
        s.cookSecondsRemaining = cookRemaining; s.jarValue = 100;
    }

    [Test]
    public void FindIntakeSlot_PrefersPartialSameCrop_ThenEmpty_ElseMinusOne()
    {
        var st = MakeState(3);
        LoadSlot(st.slots[1], "strawberry", 2, 4);            // partial strawberry jar
        Assert.AreEqual(1, ProcessingMath.FindIntakeSlot(st, "strawberry"));
        Assert.AreEqual(0, ProcessingMath.FindIntakeSlot(st, "tomato")); // no partial → first empty

        // full up: slot0 cooking, slot1 full, slot2 partial other crop
        LoadSlot(st.slots[0], "tomato", 4, 4, 3600);
        LoadSlot(st.slots[1], "strawberry", 4, 4, 3600);
        LoadSlot(st.slots[2], "carrot", 1, 4);
        Assert.AreEqual(2, ProcessingMath.FindIntakeSlot(st, "carrot"));      // partial match
        Assert.AreEqual(-1, ProcessingMath.FindIntakeSlot(st, "strawberry")); // its jar is cooking, no empties
    }

    [Test]
    public void SlotIsCooking_RequiresFullLoad_AndRemainingCookTime()
    {
        var s = new CannerySlot();
        Assert.IsFalse(ProcessingMath.SlotIsCooking(s));               // empty
        LoadSlot(s, "tomato", 2, 4);
        Assert.IsFalse(ProcessingMath.SlotIsCooking(s));               // partial → not cooking
        LoadSlot(s, "tomato", 4, 4, 100);
        Assert.IsTrue(ProcessingMath.SlotIsCooking(s));
        s.cookSecondsRemaining = 0;
        Assert.IsFalse(ProcessingMath.SlotIsCooking(s));               // done
    }

    // ── Slot purchase gating ───────────────────────────────────────────

    [Test]
    public void CanBuySlot_RequiresUnderCap_AndBothCurrencies()
    {
        Assert.IsTrue(ProcessingMath.CanBuySlot(4, 20, coins: 150, coinCost: 150, wood: 40, woodCost: 40));
        Assert.IsFalse(ProcessingMath.CanBuySlot(20, 20, 99999, 150, 99999, 40)); // at purchasable cap
        Assert.IsFalse(ProcessingMath.CanBuySlot(4, 20, 149, 150, 99999, 40));    // short coins
        Assert.IsFalse(ProcessingMath.CanBuySlot(4, 20, 99999, 150, 39, 40));     // short wood
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Try the MCP test tool; expected: compile error / tests fail with `ProcessingMath not defined`. If no test tool is available, `mcp__gladekit-unity__compile_scripts` showing errors referencing `ProcessingMath` is the fail signal.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/EconomyCore/ProcessingMath.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One jar slot in a processing building. A slot fills with units of ONE crop; once
/// unitsLoaded reaches unitsRequired the jar starts cooking (cookSecondsRemaining set)
/// and progresses only while the shared fire is lit.
/// </summary>
[Serializable]
public class CannerySlot
{
    public string cropId;                // CropData asset name (stable id)
    public string cropName;              // display name
    public int tier;                     // 1..3 → 4h/8h/12h
    public int unitsLoaded;
    public int unitsRequired;
    public double cookSecondsRemaining;  // > 0 only once fully loaded
    public int jarValue;                 // Coins on completion, fixed at jar creation
}

/// <summary>A finished jar on the ready shelf, waiting to be sold for Coins.</summary>
[Serializable]
public class ReadyJar
{
    public string cropName;
    public int value;
}

/// <summary>Mutable state for one processing building (Cannery in Phase 1).</summary>
public class CanneryState
{
    public CannerySlot[] slots = new CannerySlot[0];
    public double fuelWood;              // wood units in the furnace (burns fractionally)
    public List<ReadyJar> readyJars = new List<ReadyJar>();
}

/// <summary>
/// Pure decision + simulation logic for wood-fired processing buildings (spec §2, §4).
/// No Unity object dependencies; fully unit-testable. MonoBehaviours route through here.
/// </summary>
public static class ProcessingMath
{
    /// <summary>Cook duration in hours by crop tier: 1→4h, 2→8h, 3→12h.</summary>
    public static int CookHoursForTier(int tier) => 4 * Mathf.Clamp(tier, 1, 3);

    /// <summary>Input units per jar = 1 per cook-hour (spec §4: jam=4, compote=8, sauce=12).</summary>
    public static int UnitsRequiredForTier(int tier) => CookHoursForTier(tier);

    /// <summary>Jar sale value in Coins: units × base crop value × tier multiplier, min 1.</summary>
    public static int JarValue(int baseUnitValue, int tier, float multiplier)
    {
        int units = UnitsRequiredForTier(tier);
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, baseUnitValue) * units * Mathf.Max(0f, multiplier)));
    }

    public static bool SlotIsEmpty(CannerySlot s) => s == null || string.IsNullOrEmpty(s.cropId);

    public static bool SlotIsCooking(CannerySlot s)
        => !SlotIsEmpty(s) && s.unitsLoaded >= s.unitsRequired && s.cookSecondsRemaining > 0;

    public static int CountCooking(CanneryState st)
    {
        int n = 0;
        for (int i = 0; i < st.slots.Length; i++)
            if (SlotIsCooking(st.slots[i])) n++;
        return n;
    }

    /// <summary>
    /// Where an incoming harvested unit goes: a partial jar of the same crop first,
    /// else the first empty slot, else -1 (cannery full → harvest pays out normally).
    /// </summary>
    public static int FindIntakeSlot(CanneryState st, string cropId)
    {
        for (int i = 0; i < st.slots.Length; i++)
        {
            var s = st.slots[i];
            if (!SlotIsEmpty(s) && s.cropId == cropId && s.unitsLoaded < s.unitsRequired) return i;
        }
        for (int i = 0; i < st.slots.Length; i++)
            if (SlotIsEmpty(st.slots[i])) return i;
        return -1;
    }

    /// <summary>Whether the next slot can be bought: under the purchasable cap and both currencies afford it.</summary>
    public static bool CanBuySlot(int slotsOwned, int maxPurchasable, int coins, int coinCost, int wood, int woodCost)
    {
        if (slotsOwned >= maxPurchasable) return false;
        return coins >= coinCost && wood >= woodCost;
    }
}
```

- [ ] **Step 4: Compile + run tests to verify they pass**

`mcp__gladekit-unity__compile_scripts` → `hasErrors:false`, then run the four tests → PASS.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/EconomyCore/ProcessingMath.cs Assets/Tests/EditMode/ProcessingMathTests.cs
git commit -m "feat(cannery): ProcessingMath types, tier/jar math, intake routing, slot gating"
```

---

### Task 2: ProcessingMath — firebox simulation + stoke-to-finish

**Files:**
- Modify: `Assets/Scripts/EconomyCore/ProcessingMath.cs` (append methods)
- Modify: `Assets/Tests/EditMode/ProcessingMathTests.cs` (append tests)

**Interfaces:**
- Consumes: Task 1 types.
- Produces:
  - `ProcessingMath.BurnRatePerSecond(int cookingSlots, float baseWoodPerHour, float perSlotWoodPerHour) : double`
  - `ProcessingMath.Simulate(CanneryState st, double elapsedSeconds, float baseWoodPerHour, float perSlotWoodPerHour) : int` — returns jars finished; mutates fuel/slots/readyJars
  - `ProcessingMath.WoodToFinishLoaded(CanneryState st, float baseWoodPerHour, float perSlotWoodPerHour) : double` — extra wood needed beyond current fuel so every currently-cooking jar finishes

**Spec behavior encoded (spec §2):** burn = base + perSlot × cooking, only while fuel > 0; fire burns even with nothing cooking (waste); fuel out → cook progress pauses, nothing ruined; finished jars free their slot immediately.

- [ ] **Step 1: Write the failing tests**

Append to `ProcessingMathTests.cs` (inside the class). Test rates are chosen for clean math: `perSlot = 3600` wood/h = 1 wood/sec per cooking slot.

```csharp
    // ── Firebox simulation ─────────────────────────────────────────────

    [Test]
    public void Simulate_CooksAndConsumesFuel_FinishedJarMovesToShelfAndFreesSlot()
    {
        var st = MakeState(2);
        LoadSlot(st.slots[0], "tomato", 4, 4, cookRemaining: 10);
        st.fuelWood = 100;
        int finished = ProcessingMath.Simulate(st, 10, baseWoodPerHour: 0f, perSlotWoodPerHour: 3600f);
        Assert.AreEqual(1, finished);
        Assert.AreEqual(1, st.readyJars.Count);
        Assert.AreEqual(100, st.readyJars[0].value);
        Assert.IsTrue(ProcessingMath.SlotIsEmpty(st.slots[0]));      // slot freed
        Assert.AreEqual(90.0, st.fuelWood, 1e-6);                    // 1 wood/sec × 10s
    }

    [Test]
    public void Simulate_FuelOut_PausesCooking_NothingRuined()
    {
        var st = MakeState(1);
        LoadSlot(st.slots[0], "tomato", 4, 4, cookRemaining: 100);
        st.fuelWood = 30; // only 30s of fire at 1 wood/sec
        int finished = ProcessingMath.Simulate(st, 1000, 0f, 3600f);
        Assert.AreEqual(0, finished);
        Assert.AreEqual(0.0, st.fuelWood, 1e-6);
        Assert.AreEqual(70.0, st.slots[0].cookSecondsRemaining, 1e-6); // paused at 70s left

        // re-stoke and resume: part-fills are fine (spec §2)
        st.fuelWood = 70;
        finished = ProcessingMath.Simulate(st, 70, 0f, 3600f);
        Assert.AreEqual(1, finished);
    }

    [Test]
    public void Simulate_EmptyFire_BurnsBaseRate_AsWaste()
    {
        var st = MakeState(2); // nothing loaded
        st.fuelWood = 10;
        ProcessingMath.Simulate(st, 3600, baseWoodPerHour: 5f, perSlotWoodPerHour: 3600f);
        Assert.AreEqual(5.0, st.fuelWood, 1e-6);  // base 5/h burned for an hour, no progress made
        // and with base 0 + nothing cooking, nothing burns (no infinite loop either)
        var st2 = MakeState(1);
        st2.fuelWood = 10;
        ProcessingMath.Simulate(st2, 3600, 0f, 3600f);
        Assert.AreEqual(10.0, st2.fuelWood, 1e-6);
    }

    [Test]
    public void Simulate_MultiSlot_RateDropsWhenFirstJarFinishes()
    {
        var st = MakeState(2);
        LoadSlot(st.slots[0], "a", 4, 4, cookRemaining: 10);
        LoadSlot(st.slots[1], "b", 4, 4, cookRemaining: 30);
        st.fuelWood = 1000;
        int finished = ProcessingMath.Simulate(st, 30, 0f, 3600f);
        Assert.AreEqual(2, finished);
        // 10s at 2 wood/sec + 20s at 1 wood/sec = 40 wood
        Assert.AreEqual(960.0, st.fuelWood, 1e-6);
    }

    // ── Stoke-to-finish ────────────────────────────────────────────────

    [Test]
    public void WoodToFinishLoaded_ExactPiecewiseNeed_MinusCurrentFuel()
    {
        var st = MakeState(2);
        LoadSlot(st.slots[0], "a", 4, 4, cookRemaining: 10);
        LoadSlot(st.slots[1], "b", 4, 4, cookRemaining: 30);
        st.fuelWood = 15;
        // need 40 (see multi-slot test) minus 15 on hand = 25
        Assert.AreEqual(25.0, ProcessingMath.WoodToFinishLoaded(st, 0f, 3600f), 1e-6);
        // nothing cooking → zero
        var idle = MakeState(2);
        idle.fuelWood = 5;
        Assert.AreEqual(0.0, ProcessingMath.WoodToFinishLoaded(idle, 5f, 3600f), 1e-6);
        // ample fuel → zero (never negative)
        st.fuelWood = 500;
        Assert.AreEqual(0.0, ProcessingMath.WoodToFinishLoaded(st, 0f, 3600f), 1e-6);
    }
```

- [ ] **Step 2: Run tests to verify the new ones fail** (missing methods → compile error is the fail signal)

- [ ] **Step 3: Write the implementation**

Append to `ProcessingMath` class:

```csharp
    /// <summary>Wood consumed per second while the fire is lit: base + perSlot × cooking jars.</summary>
    public static double BurnRatePerSecond(int cookingSlots, float baseWoodPerHour, float perSlotWoodPerHour)
        => (Mathf.Max(0f, baseWoodPerHour) + Mathf.Max(0f, perSlotWoodPerHour) * Mathf.Max(0, cookingSlots)) / 3600.0;

    /// <summary>
    /// Advance the building by elapsed wall-clock seconds (piecewise: burn rate changes when a
    /// jar finishes). Fire is lit while fuelWood > 0 and burns even with nothing cooking (waste
    /// is intentional, spec §2). Fuel out → cooking pauses; nothing is ever ruined. Finished
    /// jars move to readyJars and free their slot immediately. Returns jars finished.
    /// </summary>
    public static int Simulate(CanneryState st, double elapsedSeconds, float baseWoodPerHour, float perSlotWoodPerHour)
    {
        int finished = 0;
        double remaining = elapsedSeconds;
        int guard = 0;
        while (remaining > 1e-9 && st.fuelWood > 1e-9 && ++guard < 100000)
        {
            int cooking = CountCooking(st);
            double rate = BurnRatePerSecond(cooking, baseWoodPerHour, perSlotWoodPerHour);
            if (rate <= 0) break; // base 0 + nothing cooking: fire idles for free

            double fuelSecs = st.fuelWood / rate;
            double nextFinish = double.MaxValue;
            for (int i = 0; i < st.slots.Length; i++)
                if (SlotIsCooking(st.slots[i]) && st.slots[i].cookSecondsRemaining < nextFinish)
                    nextFinish = st.slots[i].cookSecondsRemaining;

            double step = Math.Min(remaining, Math.Min(fuelSecs, nextFinish));
            if (step <= 0) break;

            st.fuelWood = Math.Max(0, st.fuelWood - rate * step);
            for (int i = 0; i < st.slots.Length; i++)
            {
                var s = st.slots[i];
                if (!SlotIsCooking(s)) continue;
                s.cookSecondsRemaining -= step;
                if (s.cookSecondsRemaining <= 1e-6)
                {
                    st.readyJars.Add(new ReadyJar { cropName = s.cropName, value = s.jarValue });
                    finished++;
                    s.cropId = null; s.cropName = null; s.tier = 0;
                    s.unitsLoaded = 0; s.unitsRequired = 0;
                    s.cookSecondsRemaining = 0; s.jarValue = 0;
                }
            }
            remaining -= step;
        }
        return finished;
    }

    /// <summary>
    /// Extra wood (beyond current fuel) needed so every currently-COOKING jar finishes —
    /// the "Stoke to finish" amount (spec §2). Piecewise like Simulate; loading-but-not-full
    /// jars don't burn and aren't counted. Never negative.
    /// </summary>
    public static double WoodToFinishLoaded(CanneryState st, float baseWoodPerHour, float perSlotWoodPerHour)
    {
        var remain = new List<double>();
        for (int i = 0; i < st.slots.Length; i++)
            if (SlotIsCooking(st.slots[i])) remain.Add(st.slots[i].cookSecondsRemaining);
        if (remain.Count == 0) return 0.0;

        double needed = 0.0;
        while (remain.Count > 0)
        {
            remain.Sort();
            double dt = remain[0];
            needed += BurnRatePerSecond(remain.Count, baseWoodPerHour, perSlotWoodPerHour) * dt;
            for (int i = remain.Count - 1; i >= 0; i--)
            {
                remain[i] -= dt;
                if (remain[i] <= 1e-6) remain.RemoveAt(i);
            }
        }
        return Math.Max(0.0, needed - st.fuelWood);
    }
```

- [ ] **Step 4: Compile + run all ProcessingMath tests** → 9/9 PASS (plus the 13 WoodcuttingMath tests still green).

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/EconomyCore/ProcessingMath.cs Assets/Tests/EditMode/ProcessingMathTests.cs
git commit -m "feat(cannery): firebox piecewise simulation + stoke-to-finish math"
```

---

### Task 3: CanneryManager + persistence (BuildingState, GameData, SaveManager, AutoSaveManager)

**Files:**
- Create: `Assets/Scripts/Cannery/CanneryManager.cs`
- Modify: `Assets/Scripts/BuildingState.cs` (add key)
- Modify: `Assets/Scripts/GameData.cs` (fields + ctor defaults)
- Modify: `Assets/Scripts/SaveManager.cs` (capture in `SaveGame()` after the wood block ~line 139; load in `LoadGame()` after the Woodcutting block ~line 197)
- Modify: `Assets/Scripts/AutoSaveManager.cs` (subscribe/unsubscribe)

**Interfaces:**
- Consumes: Task 1–2 (`ProcessingMath`, `CanneryState`, `CannerySlot`, `ReadyJar`); `BuildingState`; `CurrencyManager.Wood/SpendWood/AddCoins`; `ToastManager.Show(string,string,ToastKind)`.
- Produces (used by Tasks 4–7):
  - `CanneryManager.Instance : CanneryManager`
  - `bool IsBuilt`, `bool IntakeOn`, `void SetIntakeOn(bool)`
  - `CanneryState State`, `bool FireLit`, `int FurnaceCapacity`, `float BaseBurnPerHour`, `float PerSlotBurnPerHour`
  - `bool TryIntake(CropData crop)`
  - `bool TryAddFuel(int amount)`, `int StokeToFinishCost()`, `void StokeToFinish()`, `void FillFurnace()`
  - `int SlotsOwned`, `int MaxPurchasableSlots`, `int TotalMaxSlots`, `int NextSlotCoinCost()`, `int NextSlotWoodCost()`, `bool CanBuySlot()`, `bool TryBuySlot()`
  - `bool TrySellJar(int index)`, `int SellAllJars()`
  - `event Action OnChanged` (durable changes incl. jar completion — autosave + UI hook)
  - `void CaptureTo(GameData d)`, `void LoadFrom(GameData d)`

- [ ] **Step 1: Add the building key**

In `BuildingState.cs` after `GreenhouseKey`:

```csharp
    public const string CanneryKey = "building_cannery_built";
```

- [ ] **Step 2: Add GameData fields**

In `GameData.cs`, after the `trees` field block:

```csharp
    // Cannery (Pantry Economy Phase 1). Built-flag lives in BuildingState (PlayerPrefs);
    // everything else is here. canneryLastSimUtcTicks anchors offline firebox catch-up.
    public bool canneryIntakeOn = true;
    public double canneryFuelWood;
    public CannerySlot[] cannerySlots;
    public ReadyJar[] canneryReadyJars;
    public long canneryLastSimUtcTicks;
```

And in the default constructor (after `trees = new TreeSaveState[0];`):

```csharp
        cannerySlots = new CannerySlot[0];
        canneryReadyJars = new ReadyJar[0];
```

- [ ] **Step 3: Create the manager**

Create `Assets/Scripts/Cannery/CanneryManager.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// Owns the Cannery: jar slots, the shared firebox, intake routing, slot purchases, and
/// jar sales. All decision/simulation math lives in ProcessingMath (EconomyCore); this
/// class is the Unity-side state holder + transaction layer, mirroring WoodcuttingManager.
/// Simulation is UtcNow-anchored so offline time catches up on load (spec §2, §11).
/// </summary>
public class CanneryManager : MonoBehaviour
{
    public static CanneryManager Instance { get; private set; }

    [Header("Firebox Tuning (spec §2)")]
    [Tooltip("Wood/hour burned while the fire is lit even with nothing cooking (waste).")]
    [SerializeField] private float baseBurnPerHour = 5f;
    [Tooltip("Additional wood/hour per cooking jar.")]
    [SerializeField] private float perSlotBurnPerHour = 20f;
    [SerializeField] private int furnaceCapacity = 1200;

    [Header("Jar Tuning (spec §4)")]
    [Tooltip("Jar value multiplier by tier (index 0 = tier 1). 2.5 / 2.65 / 2.8 = patience bonus.")]
    [SerializeField] private float[] tierMultipliers = { 2.5f, 2.65f, 2.8f };

    [Header("Slots (spec §5a: front-loaded curve)")]
    [SerializeField] private int startingSlots = 4;
    [Tooltip("Slots beyond this are research-gated (Phase 3), not purchasable.")]
    [SerializeField] private int maxPurchasableSlots = 20;
    [SerializeField] private int totalMaxSlots = 24;
    [Tooltip("Coin cost of slot N, indexed by (slotsOwned - startingSlots).")]
    [SerializeField] private int[] slotCoinCosts = { 150, 250, 400, 600, 900, 1400, 2500, 4000, 6500, 10000, 15000, 22000, 32000, 45000, 60000, 80000 };
    [SerializeField] private int[] slotWoodCosts = { 40, 60, 90, 130, 180, 250, 400, 600, 900, 1300, 1800, 2500, 3400, 4500, 6000, 8000 };

    private readonly CanneryState state = new CanneryState();
    private bool intakeOn = true;
    private long lastSimUtcTicks;

    public event Action OnChanged; // durable change: intake, fuel, purchase, sale, jar finished, load

    public bool IsBuilt => BuildingState.IsBuilt(BuildingState.CanneryKey);
    public bool IntakeOn => intakeOn;
    public CanneryState State => state;
    public bool FireLit => IsBuilt && state.fuelWood > 0;
    public int FurnaceCapacity => furnaceCapacity;
    public float BaseBurnPerHour => baseBurnPerHour;
    public float PerSlotBurnPerHour => perSlotBurnPerHour;
    public int SlotsOwned => state.slots.Length;
    public int MaxPurchasableSlots => maxPurchasableSlots;
    public int TotalMaxSlots => totalMaxSlots;

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
        if (elapsed < 0) { lastSimUtcTicks = now; return; } // forward-only clock
        if (elapsed < 0.25) return;                          // throttle
        lastSimUtcTicks = now;
        int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
        if (finished > 0)
        {
            Debug.Log($"[Cannery] {finished} jar(s) finished.");
            ToastManager.Show("Preserves ready!", "Visit the Cannery to sell.", ToastManager.ToastKind.Success);
            OnChanged?.Invoke();
        }
    }

    private void EnsureSlotArray(int count)
    {
        int target = Mathf.Clamp(count, startingSlots, totalMaxSlots);
        if (state.slots.Length >= target) return;
        var next = new CannerySlot[target];
        for (int i = 0; i < target; i++)
            next[i] = i < state.slots.Length && state.slots[i] != null ? state.slots[i] : new CannerySlot();
        state.slots = next;
    }

    public void SetIntakeOn(bool on)
    {
        if (intakeOn == on) return;
        intakeOn = on;
        OnChanged?.Invoke();
    }

    private float MultiplierForTier(int tier)
    {
        int idx = Mathf.Clamp(tier, 1, 3) - 1;
        if (tierMultipliers == null || tierMultipliers.Length == 0) return 2.5f;
        return tierMultipliers[Mathf.Min(idx, tierMultipliers.Length - 1)];
    }

    /// <summary>
    /// Mid-run diversion (spec §4a Tier 1): route one harvested unit into a jar. Returns true
    /// if diverted (caller must then SKIP the normal cash+coin payouts). Value basis is the
    /// crop's BASE harvestValue — deterministic, unaffected by in-run multipliers (knob choice).
    /// </summary>
    public bool TryIntake(CropData crop)
    {
        if (crop == null || !IsBuilt || !intakeOn) return false;
        int idx = ProcessingMath.FindIntakeSlot(state, crop.name);
        if (idx < 0) return false;

        var s = state.slots[idx];
        if (ProcessingMath.SlotIsEmpty(s))
        {
            int tier = Mathf.Clamp(crop.tier, 1, 3);
            s.cropId = crop.name;
            s.cropName = crop.cropName;
            s.tier = tier;
            s.unitsRequired = ProcessingMath.UnitsRequiredForTier(tier);
            s.unitsLoaded = 0;
            s.cookSecondsRemaining = 0;
            s.jarValue = ProcessingMath.JarValue(crop.harvestValue, tier, MultiplierForTier(tier));
        }
        s.unitsLoaded++;
        if (s.unitsLoaded >= s.unitsRequired)
            s.cookSecondsRemaining = ProcessingMath.CookHoursForTier(s.tier) * 3600.0;
        OnChanged?.Invoke();
        return true;
    }

    // ── Fuel ─────────────────────────────────────────────────────────────

    /// <summary>Move wood from the player's stock into the furnace, clamped to capacity.</summary>
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

    /// <summary>Wood still needed (beyond current fuel) so the current batch finishes.</summary>
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
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund like axe upgrade
        EnsureSlotArray(SlotsOwned + 1);
        Debug.Log($"[Cannery] Slot purchased → {SlotsOwned} slots.");
        OnChanged?.Invoke();
        return true;
    }

    // ── Selling (Gold only, spec §1) ─────────────────────────────────────

    public bool TrySellJar(int index)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || index < 0 || index >= state.readyJars.Count) return false;
        int value = state.readyJars[index].value;
        state.readyJars.RemoveAt(index);
        cm.AddCoins(value);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Sells every ready jar; returns total Coins gained.</summary>
    public int SellAllJars()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || state.readyJars.Count == 0) return 0;
        int total = 0;
        for (int i = 0; i < state.readyJars.Count; i++) total += state.readyJars[i].value;
        state.readyJars.Clear();
        cm.AddCoins(total);
        Debug.Log($"[Cannery] Sold all jars for {total} coins.");
        OnChanged?.Invoke();
        return total;
    }

    // ── Save / load (SaveManager post-construction assignment pattern) ───

    public void CaptureTo(GameData d)
    {
        d.canneryIntakeOn = intakeOn;
        d.canneryFuelWood = state.fuelWood;
        d.cannerySlots = state.slots;
        d.canneryReadyJars = state.readyJars.ToArray();
        d.canneryLastSimUtcTicks = lastSimUtcTicks != 0 ? lastSimUtcTicks : DateTime.UtcNow.Ticks;
    }

    public void LoadFrom(GameData d)
    {
        intakeOn = d.canneryIntakeOn;
        state.fuelWood = Math.Max(0, d.canneryFuelWood);
        state.slots = d.cannerySlots != null && d.cannerySlots.Length > 0 ? d.cannerySlots : new CannerySlot[0];
        for (int i = 0; i < state.slots.Length; i++)
            if (state.slots[i] == null) state.slots[i] = new CannerySlot();
        EnsureSlotArray(startingSlots);
        state.readyJars.Clear();
        if (d.canneryReadyJars != null)
            foreach (var j in d.canneryReadyJars)
                if (j != null) state.readyJars.Add(j);

        // Offline catch-up: burn/cook through the away time, then re-anchor.
        long now = DateTime.UtcNow.Ticks;
        if (IsBuilt && d.canneryLastSimUtcTicks > 0 && now > d.canneryLastSimUtcTicks)
        {
            double elapsed = (now - d.canneryLastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
            int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
            if (finished > 0)
                ToastManager.Show($"{finished} jar(s) finished while you were away!",
                    "Visit the Cannery to sell.", ToastManager.ToastKind.Success);
        }
        lastSimUtcTicks = now;
        OnChanged?.Invoke();
    }
}
```

**Note:** verify `ToastManager.ToastKind` is the actual enum name/scope (`Assets/Scripts/UI/ToastManager.cs` line ~96 shows `Show(string, string, ToastKind kind = ToastKind.Success)`). If the enum lives outside the class (`ToastKind` top-level), drop the `ToastManager.` prefix and/or omit the argument entirely (`ToastManager.Show(title, subtitle)`) — the default is Success.

- [ ] **Step 4: Wire SaveManager**

In `SaveGame()`, directly after the `data.trees = ...` line (~139):

```csharp
        if (CanneryManager.Instance != null) CanneryManager.Instance.CaptureTo(data);
```

In `LoadGame()`, directly after the `WoodcuttingManager` block's closing brace (~197):

```csharp
                if (CanneryManager.Instance != null)
                    CanneryManager.Instance.LoadFrom(data);
```

- [ ] **Step 5: Wire AutoSaveManager**

In `TrySubscribe()` after the `UpgradeManager` subscription:

```csharp
        if (CanneryManager.Instance != null)
            CanneryManager.Instance.OnChanged += OnVoid;
```

In `Unsubscribe()` after the `UpgradeManager` unsubscription:

```csharp
        if (CanneryManager.Instance != null)
            CanneryManager.Instance.OnChanged -= OnVoid;
```

- [ ] **Step 6: Compile clean; run all EditMode tests (still green — manager has no EditMode tests; its logic is Task 1–2 math)**

- [ ] **Step 7: Commit**

```
git add Assets/Scripts/Cannery/CanneryManager.cs Assets/Scripts/BuildingState.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs
git commit -m "feat(cannery): CanneryManager singleton + GameData/SaveManager/AutoSave persistence"
```

---

### Task 4: Plant.Harvest intake hook + floating "→ Cannery" text

**Files:**
- Modify: `Assets/Scripts/Plant.cs` (`Harvest()`, the payout blocks at ~lines 306–329)
- Modify: `Assets/Scripts/FloatingTextManager.cs` (new generic text label)

**Interfaces:**
- Consumes: `CanneryManager.Instance.TryIntake(CropData)` (Task 3).
- Produces: `FloatingTextManager.ShowCanneryIntake(Vector3 worldPos)`.

**Behavior (spec §4a Tier 1):** a diverted harvest skips BOTH the cash payout and the coin bank;
stats, seed-refund, and regrow behavior are unchanged. When the cannery can't take it (not built,
intake off, no slot), the harvest pays out exactly as today.

- [ ] **Step 1: Add the floating label**

In `FloatingTextManager.cs`, after `ShowCompost(...)` (~line 103), add:

```csharp
    // Called by Plant.Harvest when a harvest is diverted into the Cannery instead of paying out.
    public static void ShowCanneryIntake(Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnTextLabel("→ Cannery", new Color(0.95f, 0.62f, 0.25f), screenPos);
    }

    // Generic single-string label using the same pool + drift animation as reward labels.
    private void SpawnTextLabel(string text, Color color, Vector2 screenPos)
    {
        GameObject go = GetLabel();
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        RectTransform rt = go.GetComponent<RectTransform>();

        tmp.fontSize = 30;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = text;
        tmp.color = color;

        Vector2 localPt = ToLocalPoint(screenPos);
        rt.anchoredPosition = localPt;
        Vector2 endPos = localPt + new Vector2(0, 120f);

        LeanTween.value(go, localPt, endPos, 1.2f)
            .setEaseOutQuad().setIgnoreTimeScale(true)
            .setOnUpdate((Vector2 p) => { if (rt != null) rt.anchoredPosition = p; });
        LeanTween.value(go, 1f, 0f, 0.4f)
            .setDelay(0.8f).setIgnoreTimeScale(true)
            .setOnUpdate((float a) => { if (tmp != null) tmp.alpha = a; })
            .setOnComplete(() => ReturnLabel(go));
    }
```

- [ ] **Step 2: Hook Harvest()**

In `Plant.cs` `Harvest()`, replace the two payout blocks (currently lines ~306–329, the
`if (CurrencyManager.Instance != null) { AddMoney... }` block and the
`if (CurrencyManager.Instance != null && cropData.coinValue > 0) { ...AddCoins... }` block) with:

```csharp
        // Cannery intake (Pantry Economy §4a): a diverted harvest becomes jar progress
        // instead of cash + banked coins. Stats/refund/regrow below are unaffected.
        bool divertedToCannery = CanneryManager.Instance != null && CanneryManager.Instance.TryIntake(cropData);
        if (divertedToCannery)
            FloatingTextManager.ShowCanneryIntake(transform.position);

        if (!divertedToCannery && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMoney(harvestValue);
            FloatingTextManager.ShowMoney(harvestValue, transform.position);
        }

        // Bank permanent coins for this harvest (the "keep" currency). Scaled by coin research.
        if (!divertedToCannery && CurrencyManager.Instance != null && cropData.coinValue > 0)
        {
            int coinGain = cropData.coinValue;
            if (ResearchManager.Instance != null)
            {
                float coinBonus = ResearchManager.Instance.GetBonus(Research.StatKey.CropBonusCoinAmount);
                coinGain = Mathf.RoundToInt(coinGain * (1f + coinBonus));
            }
            // Farm upgrades: Fertilizer B × Soil Quality × Zone Level, doubled on a Bountiful crit.
            coinGain = Mathf.RoundToInt(coinGain * FarmUpgrades.CoinYieldMultiplier(zone));
            if (bountiful) coinGain *= 2;
            coinGain = Mathf.Max(1, coinGain);
            CurrencyManager.Instance.AddCoins(coinGain);
            // Stagger 0.35s after the cash pop and nudge up so both numbers stay readable.
            FloatingTextManager.ShowCoins(coinGain, transform.position + Vector3.up * 0.4f, 0.35f);
            if (RunStats.Instance != null) RunStats.Instance.AddCoinsBanked(coinGain);
        }
```

(The coin block's inner content is byte-identical to today's — only the `!divertedToCannery &&`
condition is new. Everything after — `RunStats.AddCropHarvested`, seed refund, regrow — stays.)

- [ ] **Step 3: Compile clean.**

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Plant.cs Assets/Scripts/FloatingTextManager.cs
git commit -m "feat(cannery): harvest intake diversion + floating '→ Cannery' feedback"
```

---

### Task 5: Carpenter "Build Cannery" row (coins + wood)

**Files:**
- Modify: `Assets/Scripts/UI/CarpenterPopupUITK.cs`

**Interfaces:**
- Consumes: `BuildingState.CanneryKey` / `MarkBuilt` / `IsBuilt`; `CurrencyManager` coins+wood.
- Produces: nothing new (UI only).

- [ ] **Step 1: Add serialized fields** after the Greenhouse fields (~line 19):

```csharp
    [Header("Cannery Project (Pantry Economy Phase 1)")]
    [SerializeField] private string canneryTitle = "Build Cannery";
    [TextArea]
    [SerializeField] private string canneryDescription =
        "A wood-fired kettle house. Divert harvests into jars, keep the fire stoked, sell preserves for Gold.";
    [SerializeField] private int canneryCoinCost = 800;
    [SerializeField] private int canneryWoodCost = 300;
```

- [ ] **Step 2: Add the row to `Refresh()`** after `BuildGreenhouseRow();`:

```csharp
        BuildCanneryRow();
```

- [ ] **Step 3: Add the builder method** after `BuildGreenhouseRow()`:

```csharp
    private void BuildCanneryRow()
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label(canneryTitle);
        title.AddToClassList("market-row-title");
        Label desc = new Label(canneryDescription);
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

        bool built = BuildingState.IsBuilt(BuildingState.CanneryKey);
        var cm = CurrencyManager.Instance;
        bool canAfford = cm != null && cm.CanAffordCoins(canneryCoinCost) && cm.CanAffordWood(canneryWoodCost);

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
            cost.text = $"{FormatCoinCost(canneryCoinCost)} + {canneryWoodCost} wood";
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var c = CurrencyManager.Instance;
                if (c == null) return;
                if (!c.SpendCoins(canneryCoinCost)) return;
                if (!c.SpendWood(canneryWoodCost)) { c.AddCoins(canneryCoinCost); return; }
                BuildingState.MarkBuilt(BuildingState.CanneryKey);
                Debug.Log("[Carpenter] Cannery built.");
            });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
            cost.text = $"{FormatCoinCost(canneryCoinCost)} + {canneryWoodCost} wood";
        }

        rowsList.Add(row);
    }
```

- [ ] **Step 4: Compile clean.**

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/UI/CarpenterPopupUITK.cs
git commit -m "feat(cannery): Carpenter 'Build Cannery' construction row (coins+wood)"
```

---

### Task 6: Cannery popup — UXML, USS, controller

**Files:**
- Create: `Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uxml`
- Create: `Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uss`
- Create: `Assets/Scripts/UI/CanneryPopupUITK.cs`

**Interfaces:**
- Consumes: everything on `CanneryManager` (Task 3), `CurrencyManager.OnWoodChanged/OnCoinsChanged`.
- Produces: `CanneryPopupUITK.Instance.Open()` (used by Task 7's world prop).

**UI contents (spec §2/§4/§4a/§5):** intake toggle; fuel gauge + "lasts Xh Ym at current load" +
Stoke-to-finish / Fill-furnace buttons (each showing its wood cost); slot rows (crop, units x/y,
countdown or LOADING, or empty); ready-shelf rows with per-jar SELL + SELL ALL; buy-next-slot row;
research-gated hint once purchasable cap is reached. Refreshes on a 1s schedule while open
(countdowns), plus event-driven refresh on durable changes.

- [ ] **Step 1: Create the UXML**

`Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="CanneryPopupUITK.uss" />
    <ui:VisualElement name="popup-root" class="popup-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="backdrop" picking-mode="Position" />
        <ui:VisualElement name="popup-container" class="popup-container">
            <ui:VisualElement name="header" class="header">
                <ui:Label name="header-title" text="Cannery" class="header-title" />
                <ui:Button name="close-button" class="close-button" text="X" />
            </ui:VisualElement>
            <ui:VisualElement name="body" class="body">
                <ui:VisualElement name="intake-row" class="intake-row">
                    <ui:Label name="intake-label" text="Harvest Intake" class="intake-label" />
                    <ui:Button name="intake-toggle" text="ON" class="intake-toggle" />
                </ui:VisualElement>
                <ui:Label name="fuel-title" text="🔥 Firebox" class="section-title" />
                <ui:VisualElement name="fuel-bar" class="fuel-bar">
                    <ui:VisualElement name="fuel-fill" class="fuel-fill" />
                </ui:VisualElement>
                <ui:Label name="fuel-text" text="Fuel: 0" class="fuel-text" />
                <ui:VisualElement name="fuel-buttons" class="stack-row">
                    <ui:Button name="stoke-button" text="Stoke to finish" class="sell-btn sell-btn--cash" />
                    <ui:Button name="fill-button" text="Fill furnace" class="sell-btn sell-btn--cash" />
                </ui:VisualElement>
                <ui:Label name="slots-title" text="Jars" class="section-title" />
                <ui:ScrollView name="slots-list" class="slots-list" />
                <ui:Label name="shelf-title" text="Ready Shelf" class="section-title" />
                <ui:ScrollView name="shelf-list" class="shelf-list" />
                <ui:Button name="sell-all" text="Sell All" class="sell-btn sell-btn--gold" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create the USS**

Copy `Assets/UI/WoodRackPopupUITK/WoodRackPopupUITK.uss` to
`Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uss` **verbatim** (keeps popup-root/backdrop/
popup-container/header/body/sell-btn/stack-row styles identical), then APPEND:

```css
/* ── Cannery additions ─────────────────────────────────────────── */
.section-title {
    -unity-font-style: bold;
    font-size: 26px;
    color: rgb(244, 226, 198);
    margin-top: 12px;
    margin-bottom: 6px;
}
.intake-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 6px;
}
.intake-label { font-size: 24px; color: rgb(244, 226, 198); }
.intake-toggle {
    width: 90px; height: 44px;
    background-color: rgb(76, 132, 62);
    color: white; font-size: 22px; -unity-font-style: bold;
    border-radius: 8px;
}
.intake-toggle--off { background-color: rgb(120, 60, 50); }
.fuel-bar {
    height: 18px;
    background-color: rgba(0, 0, 0, 0.45);
    border-radius: 9px;
    overflow: hidden;
}
.fuel-fill {
    height: 100%;
    width: 0%;
    background-color: rgb(224, 122, 41);
    border-radius: 9px;
}
.fuel-text { font-size: 20px; color: rgb(230, 210, 180); margin-top: 4px; margin-bottom: 4px; }
.slots-list { max-height: 260px; }
.shelf-list { max-height: 180px; }
.slot-row, .shelf-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 8px;
    margin-bottom: 4px;
    background-color: rgba(0, 0, 0, 0.25);
    border-radius: 8px;
}
.slot-row-label { font-size: 22px; color: rgb(244, 226, 198); }
.slot-row-state { font-size: 20px; color: rgb(200, 185, 160); }
.slot-row--empty .slot-row-label { color: rgba(200, 185, 160, 0.5); }
.slot-row--cooking .slot-row-state { color: rgb(224, 122, 41); }
.slot-row--paused .slot-row-state { color: rgb(200, 80, 60); }
.shelf-sell-btn {
    width: 120px; height: 40px;
    background-color: rgb(180, 140, 40);
    color: white; font-size: 20px; -unity-font-style: bold;
    border-radius: 8px;
}
.buy-slot-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 8px;
    margin-top: 4px;
    background-color: rgba(40, 80, 40, 0.35);
    border-radius: 8px;
}
.buy-slot-row--locked { background-color: rgba(0, 0, 0, 0.25); }
```

- [ ] **Step 3: Create the controller**

`Assets/Scripts/UI/CanneryPopupUITK.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Cannery panel: intake toggle, firebox gauge + stoke/fill, jar slots, ready shelf,
/// in-building slot purchases. Lifecycle mirrors WoodRackPopupUITK. Rebuilds rows on a
/// 1s schedule while open (live countdowns) and on CanneryManager.OnChanged.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class CanneryPopupUITK : MonoBehaviour
{
    public static CanneryPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root, popupRoot, fuelFill;
    private Label headerLabel, fuelText;
    private Button closeButton, intakeToggle, stokeButton, fillButton, sellAllButton;
    private ScrollView slotsList, shelfList;

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
        if (CanneryManager.Instance != null)
        {
            CanneryManager.Instance.OnChanged += OnCanneryChanged;
            eventsSubscribed = true;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged += OnInt;
            CurrencyManager.Instance.OnCoinsChanged += OnInt;
        }
    }

    private void UnsubscribeEvents()
    {
        if (CanneryManager.Instance != null)
            CanneryManager.Instance.OnChanged -= OnCanneryChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged -= OnInt;
            CurrencyManager.Instance.OnCoinsChanged -= OnInt;
        }
        eventsSubscribed = false;
    }

    private void OnCanneryChanged() { if (isOpen) Refresh(); }
    private void OnInt(int _) { if (isOpen) Refresh(); }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[CanneryPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot    = root.Q<VisualElement>("popup-root");
        headerLabel  = root.Q<Label>("header-title");
        closeButton  = root.Q<Button>("close-button");
        intakeToggle = root.Q<Button>("intake-toggle");
        fuelFill     = root.Q<VisualElement>("fuel-fill");
        fuelText     = root.Q<Label>("fuel-text");
        stokeButton  = root.Q<Button>("stoke-button");
        fillButton   = root.Q<Button>("fill-button");
        slotsList    = root.Q<ScrollView>("slots-list");
        shelfList    = root.Q<ScrollView>("shelf-list");
        sellAllButton = root.Q<Button>("sell-all");

        if (headerLabel != null) headerLabel.text = "Cannery";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (intakeToggle != null) intakeToggle.RegisterCallback<ClickEvent>(_ =>
        {
            var mgr = CanneryManager.Instance;
            if (mgr != null) mgr.SetIntakeOn(!mgr.IntakeOn);
        });
        if (stokeButton != null) stokeButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.StokeToFinish();
        });
        if (fillButton != null) fillButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.FillFurnace();
        });
        if (sellAllButton != null) sellAllButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.SellAllJars();
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
        var mgr = CanneryManager.Instance;
        var cm = CurrencyManager.Instance;
        if (mgr == null || slotsList == null) return;
        var st = mgr.State;

        // Intake toggle
        if (intakeToggle != null)
        {
            intakeToggle.text = mgr.IntakeOn ? "ON" : "OFF";
            if (mgr.IntakeOn) intakeToggle.RemoveFromClassList("intake-toggle--off");
            else intakeToggle.AddToClassList("intake-toggle--off");
        }

        // Fuel gauge: fraction of capacity + burn-time at current load
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

        // Fuel buttons
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

        RebuildSlotRows(mgr, st);
        RebuildShelfRows(mgr, st);
    }

    private void RebuildSlotRows(CanneryManager mgr, CanneryState st)
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
            else if (ProcessingMath.SlotIsCooking(s))
            {
                row.AddToClassList(fireOut ? "slot-row--paused" : "slot-row--cooking");
                label.text = $"{s.cropName} ({s.unitsLoaded}/{s.unitsRequired})";
                state.text = fireOut ? "PAUSED — no fuel" : FormatDuration(s.cookSecondsRemaining);
            }
            else
            {
                label.text = $"{s.cropName} ({s.unitsLoaded}/{s.unitsRequired})";
                state.text = "loading…";
            }
            row.Add(label);
            row.Add(state);
            slotsList.Add(row);
        }

        // Buy-next-slot row (spec §5a) / research hint past the purchasable cap
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
                buyRow.RegisterCallback<ClickEvent>(_ => { if (CanneryManager.Instance != null) CanneryManager.Instance.TryBuySlot(); });
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

    private void RebuildShelfRows(CanneryManager mgr, CanneryState st)
    {
        shelfList.Clear();
        int total = 0;
        for (int i = 0; i < st.readyJars.Count; i++)
        {
            var jar = st.readyJars[i];
            total += jar.value;
            var row = new VisualElement();
            row.AddToClassList("shelf-row");
            var label = new Label($"{jar.cropName} preserves");
            label.AddToClassList("slot-row-label");
            var sell = new Button { text = $"Sell +{jar.value}" };
            sell.AddToClassList("shelf-sell-btn");
            int index = i;
            sell.RegisterCallback<ClickEvent>(_ => { if (CanneryManager.Instance != null) CanneryManager.Instance.TrySellJar(index); });
            row.Add(label);
            row.Add(sell);
            shelfList.Add(row);
        }
        if (st.readyJars.Count == 0)
        {
            var empty = new Label("Nothing ready yet.");
            empty.AddToClassList("slot-row-state");
            shelfList.Add(empty);
        }
        if (sellAllButton != null)
        {
            sellAllButton.text = $"Sell All  +{total}";
            sellAllButton.SetEnabled(total > 0);
        }
    }
}
```

- [ ] **Step 4: Compile clean.** (UI can't be exercised until Task 7's scene wiring.)

- [ ] **Step 5: Commit**

```
git add Assets/UI/CanneryPopupUITK/ Assets/Scripts/UI/CanneryPopupUITK.cs
git commit -m "feat(cannery): UITK popup — firebox gauge, slots, ready shelf, slot purchase"
```

---

### Task 7: World building + smoke, scene wiring (Unity MCP), regrow stretch, play-mode smoke test

**Files:**
- Create: `Assets/Scripts/Cannery/CanneryBuilding.cs`
- Scene `SampleScene` via MCP (never as text)
- `WoodTreeData` SO assets via MCP (regrow stretch, spec §7)

**Interfaces:**
- Consumes: `CanneryPopupUITK.Instance.Open()`, `CanneryManager.Instance.FireLit/IsBuilt`, `BuildingState`.

- [ ] **Step 1: Create the world prop script**

`Assets/Scripts/Cannery/CanneryBuilding.cs` (press/click handling mirrors WoodRack; adds
built-visibility + smoke toggle):

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clickable Cannery building in the world (between Farm and Woods, spec §8). Hidden until
/// built (BuildingState); shows the smoke child while the fire is lit; tap opens the popup.
/// Press/click handling mirrors WoodRack.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class CanneryBuilding : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    [Header("Smoke")]
    [Tooltip("Child object shown while the firebox is lit (chimney smoke, spec §8).")]
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
    }

    private void Start()
    {
        ApplyBuiltVisibility();
        BuildingState.OnBuildingBuilt += OnBuilt;
    }

    private void OnDestroy() => BuildingState.OnBuildingBuilt -= OnBuilt;

    private void OnBuilt(string key)
    {
        if (key == BuildingState.CanneryKey) ApplyBuiltVisibility();
    }

    private void ApplyBuiltVisibility()
    {
        bool built = BuildingState.IsBuilt(BuildingState.CanneryKey);
        spriteRenderer.enabled = built;
        ownCollider.enabled = built;
        if (!built && smokeObject != null) smokeObject.SetActive(false);
    }

    private void Update()
    {
        // Smoke tracks the fire cheaply (SetActive is a no-op when unchanged).
        if (smokeObject != null && CanneryManager.Instance != null)
            smokeObject.SetActive(CanneryManager.Instance.FireLit);

        if (!spriteRenderer.enabled) return;
        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
            return;

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
        return !pan.IsPanning; // reachable from Farm or Woods framing — it sits between them
    }

    private void HandleClick()
    {
        if (CanneryPopupUITK.Instance != null) CanneryPopupUITK.Instance.Open();
        else Debug.Log("[CanneryBuilding] Clicked — no CanneryPopupUITK in scene.");
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

Compile clean before scene work.

- [ ] **Step 2: Scene wiring via Unity MCP** (stop play mode first if running — auto-stop, don't ask)

1. Create root GameObject `CanneryManager`; add component `CanneryManager`.
2. Create root GameObject `CanneryPopupUITK`; add `UIDocument`; set its **PanelSettings** to the shared `RunewoodPanelSettings` asset (locate with `find_asset`, same one WoodRackPopupUITK uses; sortingOrder stays 0) and **Source Asset** to `Assets/UI/CanneryPopupUITK/CanneryPopupUITK.uxml`; add component `CanneryPopupUITK`.
3. Create world GameObject `Cannery`:
   - Inspect existing positions first (`get_gameobject_info` on the `Woods` area root and the farm/grid root); place the Cannery roughly midway between farm and Woods, nudged toward the path the camera pans across (spec §8: between Farm and Woods; exact spot is art direction, user may move it).
   - `SpriteRenderer` with a **placeholder building sprite** — search `find_asset` for something barn/shed-like (e.g. in `Assets/Sprites/Environment/` or reuse `Trunk_Closed_1` scaled up as a last resort). Placeholder is fine; art pass is user-owned.
   - `BoxCollider2D` sized to the sprite.
   - Add component `CanneryBuilding`.
   - Child GameObject `Smoke`: small placeholder puff sprite (any soft round sprite, light gray, alpha ~0.7) positioned above the roofline; assign it to `CanneryBuilding.smokeObject`; leave it active (script drives it).
4. **Regrow stretch (spec §7):** update the `WoodTreeData` assets — Softwood `growSeconds` 40 → **180**, Hardwood 80 → **360** (use the MCP asset/property tools; if SO fields prove unreachable via MCP, tell the user exactly which two inspector fields to change and wait).
5. Save the scene (`save_scene`).

- [ ] **Step 3: Play-mode smoke test** (drive it, don't assume — `verify` skill applies)

In play mode via MCP (get_unity_console_logs after each step; no errors expected):
1. Grant test currency (CurrencyManager context menu or set fields): ≥ 800 coins + ≥ 300 wood.
2. Open Carpenter → confirm "Build Cannery" row → click BUILD → coins/wood deducted, row shows ✓ Built, world Cannery sprite appears.
3. Click the Cannery → popup opens: 4 empty slots, fuel 0, fire OUT.
4. Start a run, plant/harvest a crop with intake ON → harvest shows "→ Cannery", slot shows `crop (1/4)`, no money/coin floaters for that harvest. Toggle intake OFF → harvests pay out normally again.
5. Fill a jar to 4/4 (or set `unitsLoaded` via inspector) → state flips to cooking with a 4h countdown; `Fill furnace` moves wood in; countdown ticks; smoke appears on the building.
6. Quit play mode, re-enter → cannery state persisted (slots/fuel restored, countdown advanced by the gap).
7. For a full-cycle check without waiting 4h: temporarily set the slot's `cookSecondsRemaining` low via the debugger/inspector or accept a scaled test (e.g. inspector-set `perSlotBurnPerHour` high + hand-set cook remaining to ~60s) → jar completes → toast fires → ready shelf shows it → SELL adds coins.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Cannery/CanneryBuilding.cs Assets/Scenes/SampleScene.unity Assets/Data/ Assets/Scripts/Cannery/*.meta
git commit -m "feat(cannery): world building + smoke, scene wiring, tree regrow stretch"
```

(Include whatever `.meta` files Unity generated for the new folders/files — `git status` first.)

---

### Task 8: Final verification + docs/memory

- [ ] **Step 1:** Full EditMode test run (all suites) → expect prior 104 + ~9 new, all green. If no MCP test tool exists, ask the user to run the Test Runner and report.
- [ ] **Step 2:** `compile_scripts` → clean. Console after a fresh play-mode entry → no errors/warnings from new code.
- [ ] **Step 3:** Re-run the Task 7 Step 3 smoke flow end-to-end once more after all commits (regression).
- [ ] **Step 4:** Update `docs/superpowers/specs/2026-07-07-pantry-economy-design.md` status line (Phase 1 built) and the auto-memory `project_pantry_economy.md` (built-status, deviations — e.g. no separate PantryManager, `CanInteract` allows any settled location, actual placeholder sprite used).
- [ ] **Step 5:** Commit docs/memory updates.

---

## Self-Review Notes (already applied)

- **Spec coverage:** §2 firebox (Task 2/3/6), §4 tiers+values (Task 1/3), §4a Tier-1 intake + ON/OFF (Task 3/4/6), §5 build via Carpenter + in-menu slots (Task 5/6), §5a front-loaded curve (Task 3 knob arrays), §7 sapling fix already shipped + regrow stretch (Task 7), §8 world building + smoke (Task 7), §11 architecture/persistence (Task 3). NOT in Phase 1 by design: research gating (§5 step 1 — Phase 3), Lake/Smokehouse (§3 — Phase 2), intake bin/pre-assign (§4a Tiers 2–3 — Phase 3).
- **Known naming risk:** `ToastManager.ToastKind` scope (Task 3 note) — executor must check the real signature before assuming.
- **`CropData.tier` currently means compost tier;** reusing it as the cannery tier is intentional (spec Open Question 3 says crop→tier mapping is a data decision; the existing 1–3 values are the v1 mapping). If the user later wants independent tiers, add a `preserveTier` field then.
