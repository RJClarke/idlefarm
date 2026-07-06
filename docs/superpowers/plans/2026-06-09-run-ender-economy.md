# Run-Ender & Economy Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat end-of-run payout with a self-balancing economy — money is operational fuel (seed bags, escalating cost), coins are banked per harvest, and the run ends naturally on bankruptcy with a "survived 4h 02m" score.

**Architecture:** Money stays per-run and funds seed-bag purchases (one bag = N seeds, bought lazily when a helper needs a seed and the active bag is empty; bag cost escalates with run time). Each harvested crop banks coins directly (no end-of-run conversion). A watcher ends the run when no zone can be replanted *and* nothing is still growing. Core economic math lives in a plain testable C# class (`SeedEconomy`); the rest is thin MonoBehaviour wiring on existing managers.

**Tech Stack:** Unity (Built-in pipeline), C#, existing singleton managers (`CurrencyManager`, `RunManager`, `HelperManager`, `FarmGrid`, `ResearchManager`, `RunStats`), LeanTween, NUnit EditMode tests.

**Design source:** `memory/project_run_ender_economy.md`.

**Key existing facts the implementer must know:**
- `CurrencyManager`: `Money` (per-run, `startingMoney=100`), `Coins` (permanent). `AddMoney(int)`, `SpendMoney(int)→bool`, `CanAffordMoney(int)`, `AddCoins(int)`. Money resets via `ResetMoneyForNewRun()`.
- `RunManager`: `IsRunActive`, `CurrentRunDuration` (wall-clock anchored, survives app close), `OnRunStarted`/`OnRunEnded` events, `EndRun()`, `GetFormattedRunDuration()`. Today `CalculateRunRewards()` converts `leftoverMoney/10` → coins. **This conversion is being removed.**
- `HelperManager.GetSeedForZone(int zoneID)→CropData` gives the equipped crop per zone. `FarmGrid.Instance.GetActiveZoneIds()` and `GetOccupiedTilesInZone(int)` exist (used by `ThreatWaveManager`).
- Planting happens in `UniversalHelper.ExecutePlantTask()` → `tile.PlantCrop(prefab, cropData)`, and via the till→plant chain in `ExecuteTillTask()`.
- Harvest payout is in `Plant.Harvest()` (adds money + `FloatingTextManager.ShowMoney`).
- `RunStats` tracks per-run counters and resets on `OnRunStarted`. `FloatingTextManager.ShowMoney/ShowCoins(int, Vector3 worldPos)` only render `+` amounts today.
- Research bonuses read via `ResearchManager.Instance.GetBonus(Research.StatKey.XXX)` (returns additive float, e.g. 0.25 = +25%). Per-run snapshot persistence already covers `runActive`, `runStartUtcTicks`, `money` — **seed-bag state is intentionally NOT persisted** (resets on resume, like tiles).

**Decisions already locked (do not re-litigate):** per-bag purchasing (not per-seed); time-based bag-cost escalation (not per-purchase); single tier (no Tier ladder); best-time stored in PlayerPrefs (matches existing split-persistence pattern); no day/night cycle.

---

## File structure

| File | Responsibility | New? |
|---|---|---|
| `Assets/Scripts/CropData.cs` | Add `coinValue`, `seedBagSize`, `seedBagBaseCost` fields | modify |
| `Assets/Scripts/Research/StatKey.cs` | Add 3 stat keys (coin bonus, bag size, bag discount) | modify |
| `Assets/Scripts/FloatingTextManager.cs` | Add `ShowMoneySpent` (negative, red) | modify |
| `Assets/Scripts/Economy/SeedEconomy.cs` | Pure C# bag-cost / bag-size math (testable) | create |
| `Assets/Scripts/Economy/SeedInventory.cs` | MonoBehaviour: per-crop bag counts, buy/consume, events | create |
| `Assets/Scripts/Economy/BankruptcyWatcher.cs` | Periodic end-condition check → ends run | create |
| `Assets/Scripts/UniversalHelper.cs` | Gate planting on seed consumption | modify |
| `Assets/Scripts/Plant.cs` | Bank coins on harvest | modify |
| `Assets/Scripts/RunManager.cs` | Remove leftover→coin conversion; bankruptcy end + best-time; survived-time | modify |
| `Assets/Scripts/RunStats.cs` | Add `CoinsBanked` counter | modify |
| `Assets/Scripts/UI/SeedCounterHUD.cs` | Per-crop seed counters on screen | create |
| `Assets/Tests/EditMode/SeedEconomyTests.cs` (+ asmdef) | EditMode unit tests for `SeedEconomy` | create |

Phases: **A** data/primitives (1–3) → **B** core economy + tests (4–5) → **C** wiring (6–7) → **D** ender (8–9) → **E** UI + research authoring (10–11).

---

## Task 1: CropData — cash + coin + seed-bag fields

**Files:**
- Modify: `Assets/Scripts/CropData.cs` (after `harvestValue`, line ~42)

- [ ] **Step 1: Add the fields**

In `CropData`, directly below the `harvestValue` field (line 42), add:

```csharp
    [Tooltip("Permanent COINS banked when this crop is harvested (separate from cash). The 'keep' currency.")]
    public int coinValue = 1;

    [Header("Seed Economy")]
    [Tooltip("Seeds provided by one purchased bag of this crop.")]
    [Min(1)]
    public int seedBagSize = 20;

    [Tooltip("Base MONEY cost of one seed bag at run start (before time-escalation and discounts).")]
    [Min(1)]
    public int seedBagBaseCost = 50;
```

- [ ] **Step 2: Compile-check**

Run via MCP: `refresh_unity(compile=request, wait_for_ready=true)` then `read_console(types=[error])`.
Expected: no new errors. (Existing `.asset` crop instances keep defaults `coinValue=1, seedBagSize=20, seedBagBaseCost=50` until tuned in Task 11.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/CropData.cs
git commit -m "feat(economy): add coinValue + seed-bag fields to CropData"
```

---

## Task 2: StatKey — coin-value, bag-size, bag-discount keys

**Files:**
- Modify: `Assets/Scripts/Research/StatKey.cs` (Plant section ~line 24)

- [ ] **Step 1: Add keys**

In `StatKey`, under the `// Plant` group after `CropBonusSellAmount` (line 24), add:

```csharp
        public const string CropBonusCoinAmount = "crop_bonus_coin_amount"; // +% coins banked per harvest
```

And under a new group before `// Meta`:

```csharp
        // Economy
        public const string SeedBagSize = "seed_bag_size";        // +% seeds per bag
        public const string SeedBagDiscount = "seed_bag_discount"; // +% reduction to bag cost
```

- [ ] **Step 2: Compile-check** (same as Task 1 Step 2). Expected: no errors. These are plain constants; consumers come in later tasks.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Research/StatKey.cs
git commit -m "feat(economy): add coin-value + seed-bag stat keys"
```

---

## Task 3: FloatingTextManager — negative "money spent" popup

**Files:**
- Modify: `Assets/Scripts/FloatingTextManager.cs`

- [ ] **Step 1: Add a Spend currency type + formatter**

In `FloatingTextManager`, add a public static method next to `ShowMoney` (line ~43):

```csharp
    // Called when money LEAVES the player (e.g. buying a seed bag). Shows "-N$" in red.
    public static void ShowMoneySpent(int amount, Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnSpendLabel(amount, screenPos);
    }
```

- [ ] **Step 2: Add the spend-label spawner**

Add this private method (a trimmed clone of `SpawnLabel`, red, `-N$`):

```csharp
    private void SpawnSpendLabel(int amount, Vector2 screenPos)
    {
        GameObject go = new GameObject("FloatingSpend", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        tmp.text = $"-{amount}$";
        tmp.color = new Color(0.85f, 0.15f, 0.15f); // red

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 80);
        rt.pivot = new Vector2(0.5f, 0f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPt);
        rt.anchoredPosition = localPt;

        // Drift DOWN (opposite of rewards) so spend reads differently from income.
        Vector2 endPos = localPt + new Vector2(0, -70f);
        LeanTween.value(go, localPt, endPos, 1.0f)
            .setEaseOutQuad().setIgnoreTimeScale(true)
            .setOnUpdate((Vector2 p) => { if (rt != null) rt.anchoredPosition = p; });
        LeanTween.value(go, 1f, 0f, 0.4f)
            .setDelay(0.6f).setIgnoreTimeScale(true)
            .setOnUpdate((float a) => { if (tmp != null) tmp.alpha = a; })
            .setOnComplete(() => { if (go != null) Destroy(go); });
    }
```

- [ ] **Step 3: Compile-check** (Task 1 Step 2 pattern). Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/FloatingTextManager.cs
git commit -m "feat(economy): negative money-spent floating popup"
```

---

## Task 4: SeedEconomy (pure logic) + EditMode tests

This is the only piece worth real unit tests: the bag-cost escalation, bag-size, and consume/refill math. Keep it a plain C# class with **no Unity dependencies** so it tests fast.

**Files:**
- Create: `Assets/Scripts/Economy/SeedEconomy.cs`
- Create: `Assets/Tests/EditMode/SeedEconomyTests.cs`
- Create: `Assets/Tests/EditMode/IdleFarm.EditModeTests.asmdef`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/SeedEconomyTests.cs`:

```csharp
using NUnit.Framework;

public class SeedEconomyTests
{
    [Test]
    public void BagCost_AtRunStart_EqualsBaseCost()
    {
        // runMinutes=0, no discount -> base cost
        Assert.AreEqual(50, SeedEconomy.BagCost(baseCost: 50, runMinutes: 0f, discountBonus: 0f));
    }

    [Test]
    public void BagCost_EscalatesWithRunTime()
    {
        // escalationPerMinute = 0.15 -> at 10 min, cost = 50 * (1 + 1.5) = 125
        Assert.AreEqual(125, SeedEconomy.BagCost(baseCost: 50, runMinutes: 10f, discountBonus: 0f));
    }

    [Test]
    public void BagCost_DiscountReducesCost_AndIsAppliedAfterEscalation()
    {
        // base 50, 10 min -> 125, then 20% discount -> 100
        Assert.AreEqual(100, SeedEconomy.BagCost(baseCost: 50, runMinutes: 10f, discountBonus: 0.20f));
    }

    [Test]
    public void BagCost_NeverBelowOne()
    {
        Assert.AreEqual(1, SeedEconomy.BagCost(baseCost: 50, runMinutes: 0f, discountBonus: 5f));
    }

    [Test]
    public void BagSize_ScalesWithBonus_AndFloors()
    {
        // 20 * (1 + 0.35) = 27.0 -> 27
        Assert.AreEqual(27, SeedEconomy.BagSize(baseSize: 20, sizeBonus: 0.35f));
    }

    [Test]
    public void BagSize_NeverBelowOne()
    {
        Assert.AreEqual(1, SeedEconomy.BagSize(baseSize: 20, sizeBonus: -5f));
    }
}
```

- [ ] **Step 2: Create the test assembly definition**

Create `Assets/Tests/EditMode/IdleFarm.EditModeTests.asmdef`:

```json
{
    "name": "IdleFarm.EditModeTests",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "optionalUnityReferences": ["TestAssemblies"]
}
```

> Note: `SeedEconomy` lives in the default `Assembly-CSharp` (no asmdef on `Assets/Scripts`), which test assemblies can see automatically. If the project later adds an asmdef to `Assets/Scripts`, add it to `references` here.

- [ ] **Step 3: Run tests to verify they FAIL**

Run via MCP `run_tests(mode=EditMode)`. Expected: all 6 FAIL (compile error — `SeedEconomy` does not exist yet).

- [ ] **Step 4: Implement `SeedEconomy`**

Create `Assets/Scripts/Economy/SeedEconomy.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Pure, Unity-light economic math for the seed-bag system. No singletons, no state —
/// so it's unit-testable. Tunable constants live here.
/// </summary>
public static class SeedEconomy
{
    /// <summary>Fractional bag-cost increase per minute of run time (0.15 = +15%/min).</summary>
    public const float EscalationPerMinute = 0.15f;

    /// <summary>
    /// Cost (money) of one seed bag. Escalates with run time, then discount is applied.
    /// </summary>
    public static int BagCost(int baseCost, float runMinutes, float discountBonus)
    {
        float escalated = baseCost * (1f + EscalationPerMinute * Mathf.Max(0f, runMinutes));
        float discounted = escalated * (1f - Mathf.Clamp01(discountBonus));
        return Mathf.Max(1, Mathf.RoundToInt(discounted));
    }

    /// <summary>Seeds delivered by one bag, scaled by a size bonus and floored.</summary>
    public static int BagSize(int baseSize, float sizeBonus)
    {
        return Mathf.Max(1, Mathf.FloorToInt(baseSize * (1f + sizeBonus)));
    }
}
```

- [ ] **Step 5: Run tests to verify they PASS**

Run `run_tests(mode=EditMode)`. Expected: 6/6 PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Economy/SeedEconomy.cs Assets/Tests/EditMode/
git commit -m "feat(economy): SeedEconomy bag math + EditMode tests"
```

---

## Task 5: SeedInventory manager

Holds per-crop seed counts, lazily buys bags, debits money, fires UI events. Resets each run.

**Files:**
- Create: `Assets/Scripts/Economy/SeedInventory.cs`

- [ ] **Step 1: Implement SeedInventory**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-run seed-bag inventory. One bag = N seeds of a crop. Bags are bought lazily:
/// when a helper needs a seed and the active bag is empty, we auto-buy if money allows.
/// Seed state is per-run only (never saved) — resets on OnRunStarted.
/// </summary>
public class SeedInventory : MonoBehaviour
{
    public static SeedInventory Instance { get; private set; }

    // crop -> seeds remaining in the current bag
    private readonly Dictionary<CropData, int> _seeds = new Dictionary<CropData, int>();

    /// <summary>Fires (crop, seedsRemaining) whenever a crop's seed count changes.</summary>
    public event Action<CropData, int> OnSeedCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted += ResetInventory;
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted -= ResetInventory;
    }

    private void ResetInventory()
    {
        var crops = new List<CropData>(_seeds.Keys);
        _seeds.Clear();
        foreach (var c in crops) OnSeedCountChanged?.Invoke(c, 0);
    }

    public int SeedsRemaining(CropData crop)
    {
        if (crop == null) return 0;
        return _seeds.TryGetValue(crop, out int n) ? n : 0;
    }

    /// <summary>Current money cost of one bag of this crop (run-time escalated, discounted).</summary>
    public int BagCost(CropData crop)
    {
        if (crop == null) return int.MaxValue;
        float runMinutes = RunManager.Instance != null ? RunManager.Instance.CurrentRunDuration / 60f : 0f;
        float discount = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.SeedBagDiscount) : 0f;
        return SeedEconomy.BagCost(crop.seedBagBaseCost, runMinutes, discount);
    }

    /// <summary>True if this crop can be planted now (has a seed, or a bag is affordable).</summary>
    public bool CanPlant(CropData crop)
    {
        if (crop == null) return false;
        if (SeedsRemaining(crop) > 0) return true;
        return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordMoney(BagCost(crop));
    }

    /// <summary>
    /// Consume one seed for planting. Buys a bag (debiting money + showing a -$ popup) if the
    /// current bag is empty. Returns false if no seed and no affordable bag (caller skips planting).
    /// </summary>
    public bool TryConsumeSeed(CropData crop, Vector3 worldPos)
    {
        if (crop == null) return false;

        if (SeedsRemaining(crop) <= 0)
        {
            if (!TryBuyBag(crop, worldPos)) return false;
        }

        _seeds[crop] = SeedsRemaining(crop) - 1;
        OnSeedCountChanged?.Invoke(crop, _seeds[crop]);
        return true;
    }

    private bool TryBuyBag(CropData crop, Vector3 worldPos)
    {
        if (CurrencyManager.Instance == null) return false;
        int cost = BagCost(crop);
        if (!CurrencyManager.Instance.SpendMoney(cost)) return false;

        int sizeBonus = 0;
        float bonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.SeedBagSize) : 0f;
        int bagSize = SeedEconomy.BagSize(crop.seedBagSize, bonus);
        _ = sizeBonus;

        _seeds[crop] = SeedsRemaining(crop) + bagSize;
        FloatingTextManager.ShowMoneySpent(cost, worldPos);
        OnSeedCountChanged?.Invoke(crop, _seeds[crop]);
        return true;
    }
}
```

- [ ] **Step 2: Compile-check** (Task 1 Step 2 pattern). Expected: no errors.

- [ ] **Step 3: Add the SeedInventory GameObject to the scene**

Via MCP `manage_gameobject(create, name="[MGR] SeedInventory", components_to_add=["SeedInventory"])`, then `manage_scene(save)`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Economy/SeedInventory.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(economy): SeedInventory — per-crop bag buy/consume"
```

---

## Task 6: Gate planting on seed consumption

**Files:**
- Modify: `Assets/Scripts/UniversalHelper.cs` (`ExecutePlantTask`, `ExecuteTillTask`)

- [ ] **Step 1: Consume a seed before planting in `ExecutePlantTask`**

Replace the `bool success = currentTask.TargetTile.PlantCrop(...)` block (lines ~217–222) with:

```csharp
        // Seed economy: must consume a seed (buying a bag if needed) before planting.
        if (SeedInventory.Instance != null &&
            !SeedInventory.Instance.TryConsumeSeed(seedType, currentTask.TargetTile.transform.position))
        {
            // Out of seeds and can't afford a bag — leave the tile empty. BankruptcyWatcher handles end.
            return;
        }

        bool success = currentTask.TargetTile.PlantCrop(seedType.plantPrefab, seedType);

        if (!success)
        {
            Debug.LogWarning($"{helperName} ✗ PlantCrop failed for {seedType.cropName} in zone {zoneID}");
        }
```

- [ ] **Step 2: Don't chain till→plant when the crop is unaffordable**

In `ExecuteTillTask`, change the chain condition (line ~152) from:

```csharp
            if (seedType != null && tile.CanPlant)
```

to:

```csharp
            bool canAffordSeed = SeedInventory.Instance == null || SeedInventory.Instance.CanPlant(seedType);
            if (seedType != null && tile.CanPlant && canAffordSeed)
```

(If unaffordable, the till completes normally and the tile waits — a helper retries later once money recovers, or the run ends.)

- [ ] **Step 3: Compile-check.** Expected: no errors.

- [ ] **Step 4: Play-mode verification**

Via MCP: `manage_editor(play)`. Start a run through the normal UI flow (or DevTools). Watch the console / Game view: confirm `-N$` popups appear as helpers buy bags and money drops. `manage_camera(screenshot, include_image=true)` to confirm popups. `manage_editor(stop)`.
Expected: money decreases in bag-sized chunks; planting still works.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UniversalHelper.cs
git commit -m "feat(economy): helpers consume seed bags to plant"
```

---

## Task 7: Bank coins on harvest

**Files:**
- Modify: `Assets/Scripts/Plant.cs` (`Harvest`, lines ~284–290)

- [ ] **Step 1: Add coin banking after the money award**

In `Harvest()`, immediately after the existing `CurrencyManager.Instance.AddMoney(harvestValue); FloatingTextManager.ShowMoney(...)` block (line ~288), add:

```csharp
        // Bank permanent coins for this harvest (the "keep" currency). Scaled by coin research.
        if (CurrencyManager.Instance != null && cropData.coinValue > 0)
        {
            int coinGain = cropData.coinValue;
            if (ResearchManager.Instance != null)
            {
                float coinBonus = ResearchManager.Instance.GetBonus(Research.StatKey.CropBonusCoinAmount);
                coinGain = Mathf.RoundToInt(coinGain * (1f + coinBonus));
            }
            coinGain = Mathf.Max(1, coinGain);
            CurrencyManager.Instance.AddCoins(coinGain);
            FloatingTextManager.ShowCoins(coinGain, transform.position + Vector3.up * 0.3f);
            if (RunStats.Instance != null) RunStats.Instance.AddCoinsBanked(coinGain);
        }
```

(`RunStats.AddCoinsBanked` is added in Task 8. If implementing strictly in order, this line will not compile until Task 8 — do Task 8 Step 1 first, or temporarily omit the `RunStats` line and add it in Task 8.)

- [ ] **Step 2: Compile-check after Task 8 Step 1.** Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Plant.cs
git commit -m "feat(economy): bank coins per harvest"
```

---

## Task 8: RunManager — remove conversion, add bankruptcy end + best-time; RunStats coins-banked

**Files:**
- Modify: `Assets/Scripts/RunStats.cs`
- Modify: `Assets/Scripts/RunManager.cs`

- [ ] **Step 1: Add `CoinsBanked` to RunStats**

In `RunStats`: add the property next to `CoinsSaved` (line 31):

```csharp
    public int CoinsBanked { get; private set; }
```

Reset it in `ResetStats()` (after line 69): `CoinsBanked = 0;`
Add the increment method near the others (after line 84):

```csharp
    public void AddCoinsBanked(int amount) => CoinsBanked += amount;
```

In `GetDisplayStats()`, replace the `("Coins Saved", CoinsSaved.ToString("N0")),` line (line 101) with:

```csharp
            ("Coins Banked", CoinsBanked.ToString("N0")),
```

- [ ] **Step 2: Stop converting leftover money to coins**

In `RunManager.CalculateRunRewards()` (lines ~181–201), replace the whole body with:

```csharp
    private int CalculateRunRewards()
    {
        // Coins are now banked per-harvest during the run (see Plant.Harvest).
        // Leftover money is operational fuel and is intentionally discarded at run end.
        return 0;
    }
```

- [ ] **Step 3: Add a bankruptcy flag + best-time record to EndRun**

Change `EndRun()` signature (line 132) to:

```csharp
    public void EndRun(bool bankrupt = false)
```

After `isRunActive = false;` (line 140), add best-time tracking:

```csharp
        // Best-survived-time record (PlayerPrefs, matching the project's split-persistence pattern).
        int survivedSecs = Mathf.FloorToInt(currentRunDuration);
        int prevBest = PlayerPrefs.GetInt("best_run_seconds", 0);
        LastRunWasRecord = bankrupt && survivedSecs > prevBest;
        if (survivedSecs > prevBest)
        {
            PlayerPrefs.SetInt("best_run_seconds", survivedSecs);
            PlayerPrefs.Save();
        }
        LastRunSurvivedSeconds = survivedSecs;
        LastRunEndedBankrupt = bankrupt;
```

Add these public properties near the other properties (line ~29):

```csharp
    public bool LastRunEndedBankrupt { get; private set; }
    public bool LastRunWasRecord { get; private set; }
    public int LastRunSurvivedSeconds { get; private set; }
    public int BestRunSeconds => PlayerPrefs.GetInt("best_run_seconds", 0);
```

- [ ] **Step 4: Compile-check.** Expected: no errors (this also unblocks Task 7's `AddCoinsBanked`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/RunManager.cs Assets/Scripts/RunStats.cs
git commit -m "feat(economy): per-harvest coins replace end conversion; survived-time record"
```

---

## Task 9: BankruptcyWatcher — natural run-ender

**Files:**
- Create: `Assets/Scripts/Economy/BankruptcyWatcher.cs`

- [ ] **Step 1: Implement the watcher**

```csharp
using UnityEngine;

/// <summary>
/// Ends the run when the player is out of seed money AND nothing is growing — i.e. no future
/// income can arrive. Checked on an interval (not every frame) while a run is active.
/// </summary>
public class BankruptcyWatcher : MonoBehaviour
{
    [Tooltip("Seconds between bankruptcy checks.")]
    [SerializeField] private float checkInterval = 2f;

    [Tooltip("Grace period after run start before bankruptcy can trigger.")]
    [SerializeField] private float startGrace = 8f;

    private float _timer;

    private void Update()
    {
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) return;
        if (RunManager.Instance.CurrentRunDuration < startGrace) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;

        if (IsBankrupt())
            RunManager.Instance.EndRun(bankrupt: true);
    }

    /// <summary>
    /// Bankrupt = (a) no equipped crop can be planted (no seeds AND no affordable bag) AND
    /// (b) zero crops currently growing anywhere (no harvest income incoming).
    /// </summary>
    private bool IsBankrupt()
    {
        if (FarmGrid.Instance == null || HelperManager.Instance == null || SeedInventory.Instance == null)
            return false;

        // (b) anything still growing?
        foreach (int zoneId in FarmGrid.Instance.GetActiveZoneIds())
        {
            if (FarmGrid.Instance.GetOccupiedTilesInZone(zoneId).Count > 0)
                return false;
        }

        // (a) can we plant anything anywhere?
        foreach (int zoneId in FarmGrid.Instance.GetActiveZoneIds())
        {
            CropData crop = HelperManager.Instance.GetSeedForZone(zoneId);
            if (crop != null && SeedInventory.Instance.CanPlant(crop))
                return false;
        }

        return true; // nothing growing and nothing plantable -> done
    }
}
```

- [ ] **Step 2: Compile-check.** Expected: no errors.

- [ ] **Step 3: Add the watcher to the scene**

MCP `manage_gameobject(create, name="[MGR] BankruptcyWatcher", components_to_add=["BankruptcyWatcher"])`, then `manage_scene(save)`.

- [ ] **Step 4: Play-mode verification**

Enter play, start a run, and use DevTools to drain money (or set `startingMoney` low + skip defense so threats eat crops). Confirm: when money can't afford a bag and the last crop dies/harvests with nothing replanted, the run ends within ~`checkInterval` seconds and the stats popup appears. `manage_editor(stop)`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Economy/BankruptcyWatcher.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(economy): BankruptcyWatcher ends run on insolvency"
```

---

## Task 10: Seed counter HUD

A lightweight per-crop counter strip so the player *sees* seeds depleting (answers "why did it stop?"). Built in code against the existing `FloatingTextManager` canvas pattern to avoid new UXML wiring.

**Files:**
- Create: `Assets/Scripts/UI/SeedCounterHUD.cs`

- [ ] **Step 1: Implement the HUD**

```csharp
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Top-left strip of "🌱 CropName: N" counters, one per active crop, driven by SeedInventory.
/// Pulses red when a crop has 0 seeds. Code-built Canvas (no UXML) for a trial-quality HUD.
/// </summary>
public class SeedCounterHUD : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    private Canvas _canvas;
    private readonly Dictionary<CropData, TextMeshProUGUI> _labels = new Dictionary<CropData, TextMeshProUGUI>();

    private void Awake()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;
        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private void OnEnable()
    {
        if (SeedInventory.Instance != null)
            SeedInventory.Instance.OnSeedCountChanged += HandleChange;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += Clear;
            RunManager.Instance.OnRunEnded += Clear;
        }
    }

    private void OnDisable()
    {
        if (SeedInventory.Instance != null)
            SeedInventory.Instance.OnSeedCountChanged -= HandleChange;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= Clear;
            RunManager.Instance.OnRunEnded -= Clear;
        }
    }

    private void Clear()
    {
        foreach (var lbl in _labels.Values) if (lbl != null) Destroy(lbl.gameObject);
        _labels.Clear();
    }

    private void HandleChange(CropData crop, int remaining)
    {
        if (crop == null) return;
        if (!_labels.TryGetValue(crop, out var lbl) || lbl == null)
        {
            lbl = CreateLabel(_labels.Count);
            _labels[crop] = lbl;
        }
        lbl.text = $"\U0001F331 {crop.cropName}: {remaining}";
        lbl.color = remaining <= 0 ? new Color(0.85f, 0.15f, 0.15f) : Color.white;
    }

    private TextMeshProUGUI CreateLabel(int index)
    {
        var go = new GameObject($"SeedCounter_{index}", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(360, 40);
        rt.anchoredPosition = new Vector2(20f, -20f - index * 36f);
        return tmp;
    }
}
```

- [ ] **Step 2: Compile-check.** Expected: no errors.

- [ ] **Step 3: Add to scene + assign font**

MCP `manage_gameobject(create, name="[UI] SeedCounterHUD", components_to_add=["SeedCounterHUD"])`. Set its `font` to the project's main TMP font (`NotoSans-Regular SDF`) via `manage_components(set_property, property="font", value={"path":"Assets/Fonts/NotoSans-Regular SDF.asset"})`. `manage_scene(save)`.

- [ ] **Step 4: Play-mode verification**

Enter play, start a run, screenshot: confirm a counter appears per active crop, ticks down as bags deplete, and turns red at 0. `manage_editor(stop)`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/SeedCounterHUD.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(economy): on-screen seed counters per crop"
```

---

## Task 11: Author research + crop upgrade levers

Data authoring so the new stat keys actually do something. No new code paths — the consumers were wired in Tasks 5/7.

**Files:**
- Modify: `Assets/Editor/ResearchCatalogGenerator.cs` (the catalog generator referenced in repo; regenerates `Resources/Research/`)
- Data: per-crop `coinValue` / `seedBagBaseCost` / `seedBagSize` tuning on existing `Assets/Data/` crop assets

- [ ] **Step 1: Add catalog entries for the 3 new stat keys**

In `ResearchCatalogGenerator.cs`, add three research definitions (follow the existing entry pattern in that file — match its struct/initializer shape exactly; do not invent fields):
- **Coin Yield** → `targetStatKey = StatKey.CropBonusCoinAmount`, branch Plant, e.g. `bonusPerLevel = 0.10f`.
- **Bigger Seed Bags** → `targetStatKey = StatKey.SeedBagSize`, branch Plant/Economy, e.g. `bonusPerLevel = 0.10f`.
- **Bulk Seed Discount** → `targetStatKey = StatKey.SeedBagDiscount`, branch Plant/Economy, e.g. `bonusPerLevel = 0.05f` (cap effective discount in tuning so cost can't reach 0).

- [ ] **Step 2: Regenerate the catalog**

Run the generator's menu item via MCP `execute_menu_item` (find its `[MenuItem(...)]` path in `ResearchCatalogGenerator.cs`). Confirm new `.asset` files appear under `Assets/Resources/Research/`.

- [ ] **Step 3: Tune crop values**

For each crop asset in `Assets/Data/` (open via Inspector or `manage_scriptable_object`), set distinct `harvestValue` (cash) vs `coinValue` (keep) so crops have real trade-offs (e.g. a "cash crop" with high `harvestValue`/low `coinValue` to fund seeds, and a "bank crop" with the reverse). Set `seedBagBaseCost`/`seedBagSize` per crop tier.

- [ ] **Step 4: Compile-check + catalog sanity** via `read_console`. Expected: no errors; ResearchManager loads the expanded catalog.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ResearchCatalogGenerator.cs Assets/Resources/Research/ Assets/Data/
git commit -m "feat(economy): research levers + crop cash/coin tuning"
```

---

## Self-review notes

- **Spec coverage:** money=fuel via seed bags (T1,5,6) ✓; escalating bag cost (T4) ✓; coins banked per-harvest (T7) ✓; per-crop cash+coin values (T1,11) ✓; bankruptcy ender (T9) ✓; score=survived time + record (T8) ✓; coin/bag research (T2,11) ✓; negative money viz + seed counters answering "why did it stop?" (T3,10) ✓; remove flat end-conversion (T8) ✓. Day/night and Tiers correctly absent (parked/punted).
- **Type consistency:** `SeedInventory.TryConsumeSeed(CropData, Vector3)`, `CanPlant(CropData)`, `BagCost(CropData)`; `SeedEconomy.BagCost(int,float,float)`/`BagSize(int,float)`; `RunStats.AddCoinsBanked(int)`; `RunManager.EndRun(bool)` — all referenced consistently across tasks.
- **Ordering caveat called out:** Task 7's `RunStats.AddCoinsBanked` line depends on Task 8 Step 1 (flagged inline).
- **Known soft spots to verify during play-testing (not blockers):** `EscalationPerMinute=0.15` and `startingMoney=100` are first-guess tuning — the crossover timing must be felt in-game; `BankruptcyWatcher.IsBankrupt` treats "occupied tile" as "income incoming," which is correct only if occupied tiles always carry a living plant (true today — tiles clear on plant death/harvest-removal).
```
