# Offline Reopen Flow — Wiring (Plan 2 of 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the Plan-1 simulator into the live game so reopening with an active run replays the away-period, applies the 30% tax, and gates resume-vs-end (solvent → resume; bankrupt → end), pushing a full per-crop / per-cause breakdown into `RunStats`.

**Architecture:** A new `OfflineRunContextBuilder` (Assembly-CSharp) gathers live state — saved run snapshot, `CropData→SimCrop`, every upgrade channel, and live wave/storm tuning — into an `OfflineSimContext`, runs `OfflineRunSimulator.Simulate`, and returns the result + tax-adjusted payouts. `OfflineProgressManager` orchestrates the reopen: build → simulate → tax → branch (`RunManager.ResumeRun` or new `RunManager.FinalizeOfflineBankruptcy`) → populate `RunStats`. The only new pure/testable logic is `OfflineMitigation` (bonus→reduction math) in `EconomyCore`; everything else is integration glue verified by compile + a DevTools-driven play-mode smoke test.

**Tech Stack:** C# (Unity 6000.x), MonoBehaviour singletons, existing `EconomyCore` asmdef, NUnit EditMode for `OfflineMitigation`.

**Spec:** `docs/superpowers/specs/2026-06-16-offline-run-simulator-and-stats-redesign-design.md`
**Depends on:** Plan 1 (`OfflineRunSimulator`, `OfflineSimContext`, `OfflineRunResult`, `OfflineTax`, `OfflineSimTuning`) — already built, 35/35 tests green.
**Plan 3 (later):** replaces the existing `OfflineProgressModalUITK` + `RunStatsPopup` with the redesigned UITK ledger surfaces. Plan 2 deliberately reuses the EXISTING popups so the behavior is verifiable before the UI work.

---

## Verification note (read first)

The EditMode test assembly (`IdleFarm.EditModeTests`) references only `IdleFarm.EconomyCore` and cannot
see `Assembly-CSharp`, and asmdefs cannot reference the default assembly. Therefore the glue in this plan
(context-builder, manager edits) is **not** unit-testable. It is verified by:
1. **Compile check** — after each `.cs` change, `refresh_unity` (force, scripts) then `read_console`
   filtered to the file; expect zero errors.
2. **Play-mode smoke test** (final task) — a DevTools button force-triggers the offline flow with a
   backdated save and asserts via console logs + the existing popups.

Only `OfflineMitigation` (Task 1) has real branchable logic and gets true TDD.

---

## File Structure

- Create: `Assets/Scripts/EconomyCore/OfflineMitigation.cs` — pure bonus→reduction math (testable).
- Create: `Assets/Tests/EditMode/OfflineMitigationTests.cs`
- Modify: `Assets/Scripts/EconomyCore/OfflineSim.cs` — add offline-only mitigation base dials to `OfflineSimTuning`.
- Modify: `Assets/Scripts/FarmGrid.cs` — add `TileCountPerZone` getter.
- Modify: `Assets/Scripts/ThreatWaveManager.cs` — add `GetOfflineWaveConfig()`.
- Modify: `Assets/Scripts/ThunderstormManager.cs` — add `StormWaveInterval` / `LightningStrikeInterval` getters.
- Modify: `Assets/Scripts/RunStats.cs` — per-crop harvest dict, lightning cause, `IngestOfflineResult`.
- Modify: `Assets/Scripts/RunManager.cs` — add `FinalizeOfflineBankruptcy`.
- Create: `Assets/Scripts/Economy/OfflineRunContextBuilder.cs` — gather context + run sim + payouts.
- Modify: `Assets/Scripts/OfflineProgressManager.cs` — gated reopen orchestration.
- Modify: `Assets/Scripts/SaveManager.cs` — defer resume to the gated flow.
- Modify: `Assets/Scripts/DevToolsSetup.cs` — add a "Force Offline Sim" dev button (smoke test).

---

## Task 1: `OfflineMitigation` — pure bonus→reduction math

The only piece with real logic worth TDD: turn "owns a fence + fence_capacity research" into a 0..1 loss
reduction, and combine independent sources so they never exceed 1.

**Files:**
- Create: `Assets/Scripts/EconomyCore/OfflineMitigation.cs`
- Test: `Assets/Tests/EditMode/OfflineMitigationTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/OfflineMitigationTests.cs
using NUnit.Framework;

public class OfflineMitigationTests
{
    [Test] public void Reduction_AbsentIsZero()  => Assert.AreEqual(0f, OfflineMitigation.Reduction(false, 0.5f, 1f), 1e-4f);
    [Test] public void Reduction_PresentBase()    => Assert.AreEqual(0.5f, OfflineMitigation.Reduction(true, 0.5f, 0f), 1e-4f);
    [Test] public void Reduction_EffectivenessScales() => Assert.AreEqual(0.6f, OfflineMitigation.Reduction(true, 0.5f, 0.2f), 1e-4f); // 0.5*1.2
    [Test] public void Reduction_ClampedToOne()    => Assert.AreEqual(1f, OfflineMitigation.Reduction(true, 0.9f, 1f), 1e-4f);
    [Test] public void Reduction_NegativeBonusClamped() => Assert.AreEqual(0.5f, OfflineMitigation.Reduction(true, 0.5f, -1f), 1e-4f);

    [Test] public void Stack_TwoSources_ComplementProduct() // 1-(1-.5)(1-.5)=.75
        => Assert.AreEqual(0.75f, OfflineMitigation.Stack(0.5f, 0.5f), 1e-4f);
    [Test] public void Stack_WithZero_Unchanged()  => Assert.AreEqual(0.4f, OfflineMitigation.Stack(0.4f, 0f), 1e-4f);
    [Test] public void Stack_NeverExceedsOne()     => Assert.AreEqual(1f, OfflineMitigation.Stack(1f, 0.5f), 1e-4f);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run EditMode (`OfflineMitigationTests`) via `mcp__UnityMCP__run_tests` (assembly `IdleFarm.EditModeTests`).
Expected: FAIL — `OfflineMitigation` does not exist (CS0103). Confirm via `read_console` filter `OfflineMitigation`.

- [ ] **Step 3: Implement**

```csharp
// Assets/Scripts/EconomyCore/OfflineMitigation.cs
using UnityEngine;

/// <summary>
/// Pure helpers that turn equipment ownership + research effectiveness bonuses into 0..1 loss
/// reductions for the offline simulator, and combine independent sources without ever exceeding 1.
/// </summary>
public static class OfflineMitigation
{
    /// <summary>
    /// Reduction from one source: 0 if absent, else baseReduction scaled by its effectiveness bonus,
    /// clamped to [0,1]. Negative bonuses are treated as 0 (never below base).
    /// </summary>
    public static float Reduction(bool present, float baseReduction, float effectivenessBonus)
        => present ? Mathf.Clamp01(baseReduction * (1f + Mathf.Max(0f, effectivenessBonus))) : 0f;

    /// <summary>Combine two independent reductions via complement product: 1 - (1-a)(1-b). Order-free, &lt;= 1.</summary>
    public static float Stack(float a, float b)
        => 1f - (1f - Mathf.Clamp01(a)) * (1f - Mathf.Clamp01(b));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run EditMode (`OfflineMitigationTests`). Expected: PASS (all 8).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/OfflineMitigation.cs Assets/Scripts/EconomyCore/OfflineMitigation.cs.meta Assets/Tests/EditMode/OfflineMitigationTests.cs Assets/Tests/EditMode/OfflineMitigationTests.cs.meta
git commit -m "feat(economy): pure offline mitigation (bonus->reduction + stacking)"
```

---

## Task 2: Mitigation base dials on `OfflineSimTuning`

Add the offline-only "how much does owning a fence reduce deer losses" base values. These are dials, not
live-mirrored (the live game has no single equivalent number).

**Files:**
- Modify: `Assets/Scripts/EconomyCore/OfflineSim.cs`

- [ ] **Step 1: Add fields to the OFFLINE-ONLY section of `OfflineSimTuning`**

Find the `// --- OFFLINE-ONLY dials ...` block and append:

```csharp
    // base loss reductions when the matching equipment is equipped (scaled by effectiveness research)
    public float fenceDeerReduction = 0.5f;
    public float dogDeerReduction = 0.3f;
    public float scarecrowCrowReduction = 0.5f;
    public float sprinklerDryReduction = 0.5f;
```

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `OfflineSim`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/EconomyCore/OfflineSim.cs
git commit -m "feat(economy): equipment base loss-reduction dials on OfflineSimTuning"
```

---

## Task 3: `FarmGrid.TileCountPerZone` getter

The simulator needs tiles-per-zone; `tilesPerZone` is private and the grid is `tilesPerZone x tilesPerZone`.

**Files:**
- Modify: `Assets/Scripts/FarmGrid.cs` (near the other public accessors, e.g. above `GetActiveZoneIds`)

- [ ] **Step 1: Add the getters**

```csharp
    /// <summary>Tiles along one edge of a zone (grid is square).</summary>
    public int TilesPerZoneEdge => tilesPerZone;

    /// <summary>Total tiles in a single zone (tilesPerZone squared).</summary>
    public int TileCountPerZone => tilesPerZone * tilesPerZone;
```

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `FarmGrid`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/FarmGrid.cs
git commit -m "feat(grid): public TileCountPerZone getter for offline sim"
```

---

## Task 4: `ThreatWaveManager.GetOfflineWaveConfig()`

Expose the serialized wave fields so the context-builder copies live values into the tuning (single source
of truth — change them in the inspector and offline follows).

**Files:**
- Modify: `Assets/Scripts/ThreatWaveManager.cs` (add a nested struct + getter near the `Public API` region)

- [ ] **Step 1: Add the config struct and getter**

```csharp
    /// <summary>Snapshot of the serialized wave-formula fields, for the offline simulator.</summary>
    public struct OfflineWaveConfig
    {
        public float waveIntervalSeconds;
        public int deerStartWave, deerCountInterval, maxDeer;
        public int crowStartWave, crowCountInterval, maxCrows;
        public float baseHunger, crowBaseHunger, hungerScalePerWave;
    }

    public OfflineWaveConfig GetOfflineWaveConfig() => new OfflineWaveConfig
    {
        waveIntervalSeconds = waveIntervalSeconds,
        deerStartWave = deerStartWave, deerCountInterval = deerCountInterval, maxDeer = maxDeer,
        crowStartWave = crowStartWave, crowCountInterval = crowCountInterval, maxCrows = maxCrows,
        baseHunger = baseHunger, crowBaseHunger = crowBaseHunger, hungerScalePerWave = hungerScalePerWave
    };
```

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `ThreatWaveManager`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/ThreatWaveManager.cs
git commit -m "feat(threats): expose wave config for offline sim (single source of truth)"
```

---

## Task 5: `ThunderstormManager` storm getters

Expose the two storm fields the sim mirrors (from the assigned `WeatherData`).

**Files:**
- Modify: `Assets/Scripts/ThunderstormManager.cs`

- [ ] **Step 1: Add getters (return safe fallbacks if weatherData is unassigned)**

```csharp
    /// <summary>Waves between storms (from WeatherData); 25 fallback if unassigned.</summary>
    public int StormWaveInterval => weatherData != null ? weatherData.stormWaveInterval : 25;

    /// <summary>Seconds between lightning strikes during a storm (from WeatherData); 8 fallback.</summary>
    public float LightningStrikeInterval => weatherData != null ? weatherData.lightningStrikeInterval : 8f;
```

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `ThunderstormManager`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/ThunderstormManager.cs
git commit -m "feat(weather): expose storm interval/strike getters for offline sim"
```

---

## Task 6: `RunStats` — per-crop harvest + lightning cause + offline ingest

`RunStats` currently keeps only a total `CropsHarvested` and has no lightning cause. Add a per-crop dict,
the lightning counter, and a method to ingest an `OfflineRunResult` so the bankrupt path renders identically
to a live run.

**Files:**
- Modify: `Assets/Scripts/RunStats.cs`

- [ ] **Step 1: Add fields, counters, reset, and ingest**

Add to the counters region (after `CropsHarvested`):

```csharp
    public int PlantsStruckByLightning { get; private set; }
    /// <summary>Per-crop harvested counts (live + offline). Key is CropData.</summary>
    public readonly Dictionary<CropData, int> HarvestedByCrop = new Dictionary<CropData, int>();
```

In `ResetStats()` add:

```csharp
        PlantsStruckByLightning = 0;
        HarvestedByCrop.Clear();
```

Replace `AddCropHarvested` with a crop-aware overload (keep a no-arg fallback so existing callers compile):

```csharp
    public void AddCropHarvested() { CropsHarvested++; OnCropHarvested?.Invoke(); }
    public void AddCropHarvested(CropData crop)
    {
        CropsHarvested++;
        if (crop != null)
        {
            HarvestedByCrop.TryGetValue(crop, out int n);
            HarvestedByCrop[crop] = n + 1;
        }
        OnCropHarvested?.Invoke();
    }
    public void AddPlantStruckByLightning() => PlantsStruckByLightning++;
```

Add an ingest method (used by the bankrupt offline path so the stats popup shows the simulated run):

```csharp
    /// <summary>
    /// Overwrite this run's stats from a simulated offline result (used when a run ended while away).
    /// `harvestedByCrop` maps the simulator's crop ids back to CropData. Coins/compost are the
    /// already-taxed grant amounts; losses/harvests are the raw simulated counts.
    /// </summary>
    public void IngestOfflineResult(
        Dictionary<CropData, int> harvestedByCrop,
        int eatenByDeer, int eatenByCrows, int struckByLightning, int driedUp, int rotted,
        int seedsPlanted, int moneyEarned, int coinsBanked)
    {
        ResetStats();
        HarvestedByCrop.Clear();
        int totalHarvested = 0;
        if (harvestedByCrop != null)
            foreach (var kv in harvestedByCrop) { HarvestedByCrop[kv.Key] = kv.Value; totalHarvested += kv.Value; }
        CropsHarvested = totalHarvested;
        PlantsEatenByDeer = eatenByDeer;
        PlantsEatenByCrows = eatenByCrows;
        PlantsStruckByLightning = struckByLightning;
        PlantsDehydrated = driedUp;
        CropsDecayed = rotted;
        SeedsPlanted = seedsPlanted;
        MoneyEarned = moneyEarned;
        CoinsBanked = coinsBanked;
    }
```

> Note: `CropsHarvested`, `PlantsEatenByDeer`, etc. have `private set;` — assigning them inside `RunStats`
> is fine (same class). If any are auto-props without a setter, change them to `{ get; private set; }`.

- [ ] **Step 2: Update the live harvest caller to pass the crop**

In `Assets/Scripts/Plant.cs:306`, change the parameterless harvest call to pass the plant's crop (the
`cropData` field already exists on `Plant`):

```csharp
        // line 306 — before: if (RunStats.Instance != null) RunStats.Instance.AddCropHarvested();
        if (RunStats.Instance != null) RunStats.Instance.AddCropHarvested(cropData);
```

> Live "struck by lightning" attribution is intentionally NOT wired here: `Plant.Die(cause)` (Plant.cs:410)
> only knows `"dry-out"`/`"rot"`, and lightning currently kills via generic HP damage with no distinct cause.
> The offline path sets `PlantsStruckByLightning` directly via `IngestOfflineResult`, which is all this
> feature needs. Wiring a live `"lightning"` cause is a separate future polish item. `AddPlantStruckByLightning()`
> is added now (unused by live code) so that future wiring has a hook.

- [ ] **Step 3: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `RunStats`. Expected: no errors. Confirm no caller of
`AddCropHarvested()` broke (the no-arg overload still exists).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/RunStats.cs Assets/Scripts/Plant.cs
git commit -m "feat(stats): per-crop harvest, lightning cause, offline-result ingest"
```

---

## Task 7: `RunManager.FinalizeOfflineBankruptcy`

The bankrupt-offline path never resumes the run, so `EndRun` (which requires `isRunActive`) can't be used.
Add a sibling that records the ended run from simulated values and shows the stats popup, mirroring `EndRun`.

**Files:**
- Modify: `Assets/Scripts/RunManager.cs` (add after `EndRun`)

- [ ] **Step 1: Add the method**

```csharp
    /// <summary>
    /// Finalize a run that went bankrupt while the player was away. The run was never resumed
    /// (isRunActive stays false); we just record the survived/real time for the stats screen, update
    /// the best-run record, fire OnRunEnded, and show the stats popup. Coins/compost are granted by the
    /// caller (OfflineProgressManager) from the taxed simulator result; RunStats is pre-populated there.
    /// </summary>
    public void FinalizeOfflineBankruptcy(int survivedSeconds, int realSeconds)
    {
        isRunActive = false;
        runStartUtcTicks = 0;
        currentRunDuration = 0f;
        Time.timeScale = 1f;

        int prevBest = PlayerPrefs.GetInt("best_run_seconds", 0);
        LastRunWasRecord = survivedSeconds > prevBest;
        if (survivedSeconds > prevBest)
        {
            PlayerPrefs.SetInt("best_run_seconds", survivedSeconds);
            PlayerPrefs.Save();
        }
        LastRunSurvivedSeconds = survivedSeconds;
        LastRunRealSeconds = realSeconds;
        LastRunEndedBankrupt = true;

        OnRunEnded?.Invoke();

        Debug.Log($"=== OFFLINE RUN ENDED (bankrupt) — survived {FormatTime(survivedSeconds)} ===");
    }
```

> The existing `RunStatsPopup` is shown by `OfflineProgressManager` (Task 9), not here, so the welcome-back
> modal can lead into it. (Live `EndRun` shows it directly; the offline path sequences it after the modal.)

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `RunManager`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/RunManager.cs
git commit -m "feat(run): FinalizeOfflineBankruptcy for runs that end while away"
```

---

## Task 8: `OfflineRunContextBuilder`

Gather everything into an `OfflineSimContext`, run the simulator, and return the result + taxed payouts. Lives
in `Assets/Scripts/Economy/` (Assembly-CSharp) so it can read `CropData` and the singletons.

**Files:**
- Create: `Assets/Scripts/Economy/OfflineRunContextBuilder.cs`

- [ ] **Step 1: Implement the builder**

```csharp
// Assets/Scripts/Economy/OfflineRunContextBuilder.cs
using System.Collections.Generic;
using UnityEngine;
using Research;

/// <summary>
/// Builds an OfflineSimContext from live game state (saved run snapshot + upgrades + equipment + live
/// wave/storm tuning), runs OfflineRunSimulator, and exposes the result plus tax-adjusted payouts.
/// Glue (singleton + CropData access) — not unit-tested; the pure pieces live in EconomyCore.
/// </summary>
public class OfflineRunOutcome
{
    public OfflineRunResult result;
    public int taxedCoins;        // floor(result.coinsBanked * (1 - effectiveTax))
    public int taxedResumeMoney;  // floor(result.finalMoney  * (1 - effectiveTax))
    public int compostGranted;    // untaxed
    public Dictionary<CropData, int> harvestedByCrop = new Dictionary<CropData, int>();
}

public static class OfflineRunContextBuilder
{
    /// <summary>
    /// Returns null if offline progress isn't unlocked, no active run, no zones, or zero gap.
    /// `awaySeconds` is the real wall-clock gap (already &gt;= MinGap when called).
    /// </summary>
    public static OfflineRunOutcome BuildAndSimulate(float awaySeconds, float startFarmSeconds, int startMoney)
    {
        var research = ResearchManager.Instance;
        if (research != null && !research.IsFeatureUnlocked(FeatureFlag.OfflineProgress))
            return null;

        var seeds = SeedSelectionPopup.Instance != null
            ? SeedSelectionPopup.Instance.LoadAndApplySavedSelections() : null;
        if (seeds == null || seeds.Count == 0) return null;
        if (FarmGrid.Instance == null) return null;

        var tuning = new OfflineSimTuning();
        PopulateLiveTuning(tuning);
        // Helper planting throughput scales with plant-speed research (so faster helpers plant/replant more
        // offline). Harvest-speed and helper-count scaling aren't modeled in v1 (the sim harvests instantly
        // and uses a single farm-wide throughput) — see simplifications.
        tuning.plantsPerSecond *= 1f + Mathf.Max(0f, Bonus(StatKey.HelperPlantSpeed));

        var ctx = new OfflineSimContext
        {
            awaySeconds = ClampAway(awaySeconds, research),
            startFarmSeconds = startFarmSeconds,
            startMoney = startMoney,
            maxGameSpeed = 1f + Bonus(StatKey.GameSpeed),
            tuning = tuning,
            seedBagDiscount = Bonus(StatKey.SeedBagDiscount),
            seedBagSizeBonus = Bonus(StatKey.SeedBagSize),
        };

        // zones + CropData -> SimCrop (bake yield/coin/growth research into the SimCrop values)
        float sellBonus = Bonus(StatKey.CropBonusSellAmount);
        float coinBonus = Bonus(StatKey.CropBonusCoinAmount);
        float growthBonus = Bonus(StatKey.CropGrowthSpeed);
        var cropById = new Dictionary<string, CropData>();
        foreach (var kv in seeds)
        {
            CropData crop = kv.Value;
            if (crop == null) continue;
            cropById[crop.cropName] = crop;
            ctx.zones.Add(new SimZone
            {
                tileCount = FarmGrid.Instance.TileCountPerZone,
                crop = new SimCrop
                {
                    id = crop.cropName,
                    growSeconds = crop.TotalGrowthTime / (1f + Mathf.Max(0f, growthBonus)),
                    harvestWindowSeconds = crop.harvestWindowSeconds,
                    harvestValue = Mathf.RoundToInt(crop.harvestValue * (1f + sellBonus)),
                    coinValue = Mathf.Max(1, Mathf.RoundToInt(crop.coinValue * (1f + coinBonus))),
                    bagBaseCost = crop.seedBagBaseCost,
                    bagSize = crop.seedBagSize,
                    tier = crop.tier,
                }
            });
        }
        if (ctx.zones.Count == 0) return null;

        // mitigation: scan equipped assignments + research effectiveness, stack per cause
        ResolveMitigation(ctx, tuning, seeds);

        var result = OfflineRunSimulator.Simulate(ctx);

        float effBonus = Bonus(StatKey.OfflineEfficiency);
        var outcome = new OfflineRunOutcome
        {
            result = result,
            taxedCoins = OfflineTax.Payout(result.coinsBanked, effBonus),
            taxedResumeMoney = OfflineTax.Payout(result.finalMoney, effBonus),
            compostGranted = result.compostGained, // untaxed
        };
        foreach (var kv in result.harvestedByCropId)
            if (cropById.TryGetValue(kv.Key, out var c)) outcome.harvestedByCrop[c] = kv.Value;
        return outcome;
    }

    private static float Bonus(string key)
        => ResearchManager.Instance != null ? ResearchManager.Instance.GetBonus(key) : 0f;

    private static float ClampAway(float awaySeconds, ResearchManager research)
    {
        float capHours = research != null ? research.GetBonus(StatKey.OfflineCap) : 0f;
        if (capHours <= 0f) return awaySeconds;          // 0 = no cap configured
        return Mathf.Min(awaySeconds, capHours * 3600f);
    }

    private static void PopulateLiveTuning(OfflineSimTuning t)
    {
        if (ThreatWaveManager.Instance != null)
        {
            var w = ThreatWaveManager.Instance.GetOfflineWaveConfig();
            t.waveIntervalSeconds = w.waveIntervalSeconds;
            t.deerStartWave = w.deerStartWave; t.deerCountInterval = w.deerCountInterval; t.maxDeer = w.maxDeer;
            t.crowStartWave = w.crowStartWave; t.crowCountInterval = w.crowCountInterval; t.maxCrows = w.maxCrows;
            t.baseHunger = w.baseHunger; t.crowBaseHunger = w.crowBaseHunger; t.hungerScalePerWave = w.hungerScalePerWave;
        }
        if (ThunderstormManager.Instance != null)
        {
            t.stormWaveInterval = ThunderstormManager.Instance.StormWaveInterval;
            t.lightningStrikeInterval = ThunderstormManager.Instance.LightningStrikeInterval;
        }
    }

    private static void ResolveMitigation(OfflineSimContext ctx, OfflineSimTuning t, Dictionary<int, CropData> seeds)
    {
        bool hasFence = false, hasScarecrow = false, hasSprinkler = false;
        if (EquipmentManager.Instance != null)
            foreach (var zoneId in seeds.Keys)
            {
                var eq = EquipmentManager.Instance.GetAssignment(zoneId);
                if (eq == null) continue;
                if (eq.equipmentType == EquipmentType.Fence) hasFence = true;
                else if (eq.equipmentType == EquipmentType.Scarecrow) hasScarecrow = true;
                else if (eq.equipmentType == EquipmentType.Sprinkler) hasSprinkler = true;
            }

        bool hasDog = Object.FindFirstObjectByType<FarmDog>() != null; // FarmDog has no singleton; scan scene

        float deer = OfflineMitigation.Stack(
            OfflineMitigation.Reduction(hasFence, t.fenceDeerReduction, Bonus(StatKey.FenceEffectiveness)),
            OfflineMitigation.Reduction(hasDog,   t.dogDeerReduction,   Bonus(StatKey.DogEfficiency)));
        float crow = OfflineMitigation.Reduction(hasScarecrow, t.scarecrowCrowReduction, Bonus(StatKey.ScarecrowEffectiveness));
        float dry = OfflineMitigation.Stack(
            OfflineMitigation.Reduction(hasSprinkler, t.sprinklerDryReduction, Bonus(StatKey.SprinklerEffectiveness)),
            Mathf.Clamp01(Bonus(StatKey.SoilWaterEfficiency) + Bonus(StatKey.HelperWaterEfficiency)));
        float lightning = Mathf.Clamp01(Bonus(StatKey.StormDamageReduction));

        ctx.deerLossReduction = deer;
        ctx.crowLossReduction = crow;
        ctx.dryLossReduction = dry;
        ctx.lightningLossReduction = lightning;
    }
}
```

> `FarmDog` (confirmed) has no static `Instance`, so we scan the scene with `FindFirstObjectByType<FarmDog>()`
> — cheap, runs once on reopen. `EquipmentType` enum values `Fence`/`Scarecrow`/`Sprinkler` are confirmed in
> `EquipmentManager`. `Object` is `UnityEngine.Object` (already imported via `using UnityEngine;`).

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `OfflineRunContextBuilder`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Economy/OfflineRunContextBuilder.cs Assets/Scripts/Economy/OfflineRunContextBuilder.cs.meta
git commit -m "feat(economy): offline context builder (live upgrades -> sim) + taxed payouts"
```

---

## Task 9: `OfflineProgressManager` — gated reopen orchestration

Replace the cow/research-only `ShowWithGap` with: run the sim, branch resume-vs-end, apply payouts, populate
`RunStats`. Reuse the EXISTING `OfflineProgressModalUITK` (resume path) and `RunStatsPopup` (ended path) for
now — Plan 3 swaps in the redesigned surfaces.

**Files:**
- Modify: `Assets/Scripts/OfflineProgressManager.cs`

- [ ] **Step 1: Add a run snapshot seed + branch in `ShowWithGap`**

Add fields + a setter the SaveManager calls (Task 10):

```csharp
    private static bool pendingRunActive;
    private static float pendingRunFarmSeconds;
    private static int pendingRunMoney;

    /// <summary>Called by SaveManager.LoadGame with the saved active-run snapshot (if any).</summary>
    public static void SeedRunSnapshot(bool runActive, float runFarmSeconds, int runMoney)
    {
        pendingRunActive = runActive;
        pendingRunFarmSeconds = runFarmSeconds;
        pendingRunMoney = runMoney;
    }
```

Replace the body of `ShowWithGap(TimeSpan gap)` with:

```csharp
    private void ShowWithGap(TimeSpan gap)
    {
        // Cow + research catch-up happen regardless (existing behavior).
        int cowCompost = 0;
        if (AnimalManager.Instance != null)
        {
            cowCompost = AnimalManager.Instance.RunOfflineCompostCatchUp();
            cowCompost += AnimalManager.Instance.RunOfflineCowEatingCatchUp(gap.TotalSeconds);
        }
        var researchReport = ResearchManager.Instance != null ? ResearchManager.Instance.LastOfflineReport : null;

        // No active run -> existing welcome-back (cow/research only).
        if (!pendingRunActive)
        {
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.Open(gap, cowCompost, researchReport);
            return;
        }

        // Active run -> simulate the away-period.
        var outcome = OfflineRunContextBuilder.BuildAndSimulate(
            (float)gap.TotalSeconds, pendingRunFarmSeconds, pendingRunMoney);

        // Sim unavailable (offline_progress locked / no zones) -> plain resume, existing modal.
        if (outcome == null)
        {
            RunManager.Instance?.ResumeRun(
                System.DateTime.UtcNow.Ticks - (long)(gap.TotalSeconds * System.TimeSpan.TicksPerSecond),
                pendingRunFarmSeconds, 0L);
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.Open(gap, cowCompost, researchReport);
            return;
        }

        // Grant compost (untaxed) now, for both branches.
        if (CurrencyManager.Instance != null && outcome.compostGranted > 0)
            CurrencyManager.Instance.AddCompost(outcome.compostGranted);

        if (outcome.result.bankrupt)
        {
            // Ended while away: grant taxed coins, record the run, show the existing stats popup.
            if (CurrencyManager.Instance != null && outcome.taxedCoins > 0)
                CurrencyManager.Instance.AddCoins(outcome.taxedCoins);

            if (RunStats.Instance != null)
                RunStats.Instance.IngestOfflineResult(
                    outcome.harvestedByCrop,
                    outcome.result.eatenByDeer, outcome.result.eatenByCrows, outcome.result.struckByLightning,
                    outcome.result.driedUp, outcome.result.rotted,
                    outcome.result.seedsPlanted, outcome.result.moneyEarned, outcome.taxedCoins);

            int survived = Mathf.FloorToInt(outcome.result.finalFarmSeconds);
            int real = Mathf.FloorToInt((float)gap.TotalSeconds);
            RunManager.Instance?.FinalizeOfflineBankruptcy(survived, real);

            Debug.Log($"[Offline] Run ENDED bankrupt at {survived}s; +{outcome.taxedCoins} coins, +{outcome.compostGranted} compost.");
            if (RunStatsPopup.Instance != null) RunStatsPopup.Instance.Show();
        }
        else
        {
            // Survived: grant taxed coins, resume at the simulated farm time with taxed money.
            if (CurrencyManager.Instance != null)
            {
                if (outcome.taxedCoins > 0) CurrencyManager.Instance.AddCoins(outcome.taxedCoins);
                CurrencyManager.Instance.SetMoney(outcome.taxedResumeMoney);
            }
            long startTicks = System.DateTime.UtcNow.Ticks - (long)(gap.TotalSeconds * System.TimeSpan.TicksPerSecond);
            RunManager.Instance?.ResumeRun(startTicks, outcome.result.finalFarmSeconds, 0L);

            Debug.Log($"[Offline] Run CONTINUES at {outcome.result.finalFarmSeconds:F0}s; +{outcome.taxedCoins} coins, resume $ {outcome.taxedResumeMoney}, +{outcome.compostGranted} compost.");
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.Open(gap, cowCompost + outcome.compostGranted, researchReport);
        }
    }
```

> `ResumeRun` is called with `lastOnlineUtcTicks = 0L` so it does NOT re-credit the offline window — the
> simulator already advanced farm time to `finalFarmSeconds`. `startTicks` is set so `RealRunDuration`
> (wall-clock) stays accurate from the true start.

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `OfflineProgressManager`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/OfflineProgressManager.cs
git commit -m "feat(offline): gated reopen — simulate, tax, resume-vs-end, populate stats"
```

---

## Task 10: `SaveManager` — defer resume to the gated flow

Stop auto-resuming on load; hand the run snapshot to `OfflineProgressManager` so the gate decides. Keep the
short-gap fast path intact (the gate already plain-resumes when the sim is unavailable, and
`OfflineProgressManager` skips the modal for gaps &lt; `MinGapMinutes`, plain-resuming there too).

**Files:**
- Modify: `Assets/Scripts/SaveManager.cs` (the load block around lines 206–222)

- [ ] **Step 1: Replace the resume block**

Current (around 206–222):

```csharp
                if (data.runActive && data.runStartUtcTicks > 0)
                {
                    if (CurrencyManager.Instance != null)
                        CurrencyManager.Instance.SetMoney(data.money);
                    RunManager.Instance.ResumeRun(data.runStartUtcTicks, data.runTotalSeconds, data.lastSeenUtcTicks);
                }
                OfflineProgressManager.SeedLastSeen(data.lastSeenUtcTicks);
```

Replace with:

```csharp
                // Hand the active-run snapshot to the offline gate. It decides resume-vs-end after
                // simulating the away-period (see OfflineProgressManager.ShowWithGap). We do NOT resume
                // here anymore. Money is restored by the gate (taxed) on the resume branch; for the
                // short-gap / sim-unavailable fast path the gate restores it via the plain ResumeRun.
                if (data.runActive && data.runStartUtcTicks > 0 && CurrencyManager.Instance != null)
                    CurrencyManager.Instance.SetMoney(data.money); // provisional; gate overwrites on resume
                OfflineProgressManager.SeedRunSnapshot(data.runActive, data.runTotalSeconds, data.money);
                OfflineProgressManager.SeedLastSeen(data.lastSeenUtcTicks);
```

> Why keep the provisional `SetMoney`: if the gap is below `MinGapMinutes`, `OfflineProgressManager.TryShow`
> returns before simulating; in that case we still need the saved money and a resumed run. Handle that in
> Step 2.

- [ ] **Step 2: Plain-resume on sub-threshold gaps in `OfflineProgressManager.TryShow`**

In `OfflineProgressManager.TryShow()`, the early return for `gap.TotalMinutes < MinGapMinutes` must still
resume an active run (today SaveManager did it). Update that branch:

```csharp
        if (gap.TotalMinutes < MinGapMinutes)
        {
            // Too short for the welcome-back flow, but an active run must still resume.
            if (pendingRunActive && RunManager.Instance != null && !RunManager.Instance.IsRunActive)
                RunManager.Instance.ResumeRun(pendingRunStartTicksOrNow(lastSeen), pendingRunFarmSeconds, 0L);
            return;
        }
```

Add the small helper to `OfflineProgressManager` (true start preserved if we have it; else "now"):

```csharp
    private static long pendingRunStartTicksOrNow(System.DateTime lastSeen)
        => System.DateTime.UtcNow.Ticks; // sub-threshold: negligible offline credit, anchor real start to now
```

> The snapshot doesn't carry the original `runStartUtcTicks`; for a &lt;5-min gap the lost offline credit is
> negligible, so anchoring to now is acceptable. (The &gt;= threshold path computes `startTicks` from the gap.)

- [ ] **Step 3: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `SaveManager`. Then filter `OfflineProgressManager`.
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/SaveManager.cs Assets/Scripts/OfflineProgressManager.cs
git commit -m "feat(save): route active-run resume through the offline gate"
```

---

## Task 11: DevTools force-trigger + play-mode smoke test

End-to-end verification: a dev button backdates the save and forces the reopen flow, so we can observe
resume-vs-end + payouts in the console and the existing popups.

**Files:**
- Modify: `Assets/Scripts/DevToolsSetup.cs`

- [ ] **Step 1: Add a "Force Offline Sim (2h)" dev action**

Find how existing dev buttons are registered in `DevToolsSetup.cs` (search `AddButton`/`button` patterns) and
add one wired to:

```csharp
    private void ForceOfflineSim2h()
    {
        // Backdate lastSeen by 2 hours and re-run the offline gate against the current run snapshot.
        long twoHoursAgo = System.DateTime.UtcNow.Ticks - 2L * 3600L * System.TimeSpan.TicksPerSecond;
        OfflineProgressManager.SeedRunSnapshot(
            RunManager.Instance != null && RunManager.Instance.IsRunActive,
            RunManager.Instance != null ? RunManager.Instance.CurrentRunDuration : 0f,
            CurrencyManager.Instance != null ? CurrencyManager.Instance.Money : 0);
        OfflineProgressManager.SeedLastSeen(twoHoursAgo);
        if (OfflineProgressManager.Instance != null) OfflineProgressManager.Instance.ForceShow();
    }
```

> Use the existing `OfflineProgressManager.ForceShow()` (already present). Match the file's existing button
> registration style; do not invent a UI framework.

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `DevToolsSetup`. Expected: no errors.

- [ ] **Step 3: Play-mode smoke test**

Enter play mode (`manage_editor` set play, or have the human press Play). Start a run, let crops grow a few
seconds, then click **Force Offline Sim (2h)**. Verify via `read_console`:
- A `[Offline] Run CONTINUES …` or `[Offline] Run ENDED bankrupt …` line appears (depending on affordability).
- Coins increased by the logged taxed amount; compost increased; on resume, money was set to the logged
  resume value; on end, the stats popup shows per-crop harvests + per-cause losses (incl. lightning if a
  storm fell in the window).
- No exceptions/NREs in the console.

Stop play mode when done (per project convention, the assistant may stop play mode via MCP without asking).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/DevToolsSetup.cs
git commit -m "chore(devtools): force-offline-sim button for reopen-flow smoke test"
```

---

## Done criteria for Plan 2

- [ ] `OfflineMitigationTests` pass (8); full EditMode suite still green (43 total).
- [ ] Clean compile across all modified files (no new console errors).
- [ ] Force-offline smoke test shows a coherent resume-vs-end decision, taxed payouts, and a populated stats
      breakdown, with no exceptions.
- [ ] Single source of truth verified: changing a `ThreatWaveManager` wave field in the inspector changes the
      offline deer/crow counts (spot-check via the smoke test logs).

## Notes / simplifications (intentional v1 scope)

- **Mitigation is aggregated to global reductions**, not per-zone (the sim uses `ctx.*LossReduction`). If a
  player fences only some zones, offline averages the effect. Per-zone mitigation is a future refinement.
- **Helper throughput** offline scales only with `helper_plant_speed` research; **helper count** and
  **harvest-speed** are not modeled (the sim harvests matured tiles instantly and uses one farm-wide
  `plantsPerSecond`). Count-scaling needs a `HelperManager` count getter — a future refinement.
- **Defense (repelled) counts** are not simulated offline (the redesign shows them only for live runs).
- **`crop_hp`** is not yet mapped (Plan-1 sim has no HP model); fold into a small `dryLossReduction` later if
  playtest wants it.
- Plan 3 replaces the reused `OfflineProgressModalUITK` / `RunStatsPopup` with the redesigned ledger surfaces
  and the two distinct welcome-back modals; the data they need is already populated by this plan.
