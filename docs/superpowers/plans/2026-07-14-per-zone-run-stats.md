# Per-Zone Run Stats + Currency Iconography Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every run-stats surface (Current Run, Prev. Run, both welcome-back variants) shows per-zone 2x2 crop cards with losses/worth/equipment stats, an Animals section, and real currency icon sprites.

**Architecture:** Extend the existing shared pipeline: per-zone counters in `RunStats` (live) and `OfflineRunResult.zones` (sim), widened `RunLedgerData` cards, one rewritten `RunStatsLedgerView.Build` that all four windows already call. Spec: `docs/superpowers/specs/2026-07-14-per-zone-run-stats-design.md`.

**Tech Stack:** Unity 6000.3 C#, UI Toolkit (UXML/USS), NUnit EditMode tests (assembly `IdleFarm.EditModeTests` references ONLY `IdleFarm.EconomyCore` — testable logic must live in `Assets/Scripts/EconomyCore/`).

## Global Constraints

- **No emoji in any UITK text** — Android has no emoji fallback; icons are `VisualElement`s with background-image sprites.
- EditMode tests can only see `IdleFarm.EconomyCore` types (no CropData, no MonoBehaviours). UI/ledger builders are verified in play mode.
- Existing aggregate `RunStats` counters and their events (`OnDeerRepelled`, `OnCrowRepelled`, ...) must keep working — quests subscribe to them.
- Compile after each task via `mcp__gladekit-unity__compile_scripts`; run EditMode tests via `mcp__unity-mcp__run_tests` (mode EditMode). Commit after each task (commits are pre-approved for this workflow).
- Currency icon sprites (same as top bar): `Assets/Sprites/UI/Icons/Icons_Essential/Cash.png`, `Coin.png`, `Gem.png`, `Assets/Sprites/UI/Icons/Cyberpunk/Plants/Seedling_Dirt.png` (compost).

---

### Task 1: Per-zone results in the offline simulator (TDD)

**Files:**
- Modify: `Assets/Scripts/EconomyCore/OfflineSim.cs`
- Modify: `Assets/Scripts/EconomyCore/OfflineRunSimulator.cs`
- Test: `Assets/Tests/EditMode/OfflineZoneStatsTests.cs` (new)

**Interfaces:**
- Consumes: existing `OfflineSimContext`, `SimZone`, `OfflineRunResult`.
- Produces: `SimZone.zoneId` (int field), `ZoneSimStats` class (`zoneId, cropId, harvested, moneyEarned, coinsBanked, eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted` — all public ints, cropId string), `OfflineRunResult.zones` (`List<ZoneSimStats>`, one entry per ctx zone, same order). Tasks 4–5 rely on these exact names.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/OfflineZoneStatsTests.cs`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;

public class OfflineZoneStatsTests
{
    private static SimCrop Crop(string id) => new SimCrop {
        id = id, growSeconds = 30f, harvestWindowSeconds = 60f,
        harvestValue = 20, coinValue = 2, bagBaseCost = 10, bagSize = 20, tier = 1
    };

    private static OfflineSimContext TwoZoneCtx()
    {
        return new OfflineSimContext {
            awaySeconds = 3600f, startFarmSeconds = 0f, startMoney = 100000, maxGameSpeed = 1f,
            zones = new List<SimZone> {
                new SimZone { crop = Crop("Strawberry"), tileCount = 5, zoneId = 1 },
                new SimZone { crop = Crop("Blueberry"),  tileCount = 0, zoneId = 3 },
            },
            tuning = new OfflineSimTuning()
        };
    }

    [Test]
    public void Zones_CarryIdAndCrop()
    {
        var r = OfflineRunSimulator.Simulate(TwoZoneCtx());
        Assert.AreEqual(2, r.zones.Count);
        Assert.AreEqual(1, r.zones[0].zoneId);
        Assert.AreEqual("Strawberry", r.zones[0].cropId);
        Assert.AreEqual(3, r.zones[1].zoneId);
        Assert.AreEqual("Blueberry", r.zones[1].cropId);
    }

    [Test]
    public void EmptyZone_GetsNothing_ActiveZoneGetsEverything()
    {
        var r = OfflineRunSimulator.Simulate(TwoZoneCtx());
        Assert.Greater(r.zones[0].harvested, 0);
        Assert.Greater(r.zones[0].eatenByDeer, 0);
        Assert.AreEqual(0, r.zones[1].harvested);
        Assert.AreEqual(0, r.zones[1].eatenByDeer + r.zones[1].eatenByCrows
            + r.zones[1].struckByLightning + r.zones[1].driedUp + r.zones[1].rotted);
    }

    [Test]
    public void Totals_EqualZoneSums()
    {
        var r = OfflineRunSimulator.Simulate(TwoZoneCtx());
        int h = 0, money = 0, coins = 0, deer = 0, crow = 0, light = 0, dry = 0, rot = 0;
        foreach (var z in r.zones)
        {
            h += z.harvested; money += z.moneyEarned; coins += z.coinsBanked;
            deer += z.eatenByDeer; crow += z.eatenByCrows; light += z.struckByLightning;
            dry += z.driedUp; rot += z.rotted;
        }
        Assert.AreEqual(r.TotalHarvested, h);
        Assert.AreEqual(r.moneyEarned, money);
        Assert.AreEqual(r.coinsBanked, coins);
        Assert.AreEqual(r.eatenByDeer, deer);
        Assert.AreEqual(r.eatenByCrows, crow);
        Assert.AreEqual(r.struckByLightning, light);
        Assert.AreEqual(r.driedUp, dry);
        Assert.AreEqual(r.rotted, rot);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via `mcp__unity-mcp__run_tests` (mode `EditMode`, filter `OfflineZoneStatsTests`).
Expected: compile FAILURE ("SimZone does not contain a definition for 'zoneId'") — that counts as the failing state.

- [ ] **Step 3: Implement**

In `Assets/Scripts/EconomyCore/OfflineSim.cs`, change `SimZone` and add `ZoneSimStats` (top-level, after `SimZone`):

```csharp
public struct SimZone
{
    public SimCrop crop;
    public int tileCount;
    public int zoneId;   // FarmGrid ZoneID this sim zone mirrors (per-zone stat attribution)
}

/// <summary>Per-zone slice of an OfflineRunResult (harvests, worth, losses by cause).</summary>
public class ZoneSimStats
{
    public int zoneId;
    public string cropId;
    public int harvested;
    public int moneyEarned;
    public int coinsBanked;
    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
}
```

In `OfflineRunResult`, after the `rotted` field:

```csharp
    /// <summary>Per-zone breakdown, same order as ctx.zones. The flat totals are the sums.</summary>
    public List<ZoneSimStats> zones = new List<ZoneSimStats>();
```

In `Assets/Scripts/EconomyCore/OfflineRunSimulator.cs` — inside `Simulate`, right after `var r = new OfflineRunResult();`:

```csharp
        for (int z = 0; z < ctx.zones.Count; z++)
            r.zones.Add(new ZoneSimStats { zoneId = ctx.zones[z].zoneId, cropId = ctx.zones[z].crop.id });
```

In the harvest block (step "2. harvest matured tiles"), replace the three income lines:

```csharp
                        money += crop.harvestValue;
                        r.moneyEarned += crop.harvestValue;
                        r.coinsBanked += crop.coinValue;
                        r.zones[z].harvested++;
                        r.zones[z].moneyEarned += crop.harvestValue;
                        r.zones[z].coinsBanked += crop.coinValue;
                        AddHarvest(r, crop.id);
```

In the losses block (step "4."), replace the five `r.xxx += TakeWhole(...)` lines:

```csharp
                int nDeer  = TakeWhole(ref accDeer[z],  occupied[z], ref r.compostGained, crop.tier);
                int nCrow  = TakeWhole(ref accCrow[z],  occupied[z], ref r.compostGained, crop.tier);
                int nLight = TakeWhole(ref accLight[z], occupied[z], ref r.compostGained, crop.tier);
                int nDry   = TakeWhole(ref accDry[z],   occupied[z], ref r.compostGained, crop.tier);
                int nRot   = TakeWhole(ref accRot[z],   occupied[z], ref r.compostGained, crop.tier);
                r.eatenByDeer += nDeer;             r.zones[z].eatenByDeer += nDeer;
                r.eatenByCrows += nCrow;            r.zones[z].eatenByCrows += nCrow;
                r.struckByLightning += nLight;      r.zones[z].struckByLightning += nLight;
                r.driedUp += nDry;                  r.zones[z].driedUp += nDry;
                r.rotted += nRot;                   r.zones[z].rotted += nRot;
```

- [ ] **Step 4: Run tests to verify they pass**

Run `mcp__unity-mcp__run_tests` (EditMode, filter `OfflineZoneStatsTests`), then the FULL EditMode suite (no filter). Expected: all pass (the suite was 142/142 before this feature).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/OfflineSim.cs Assets/Scripts/EconomyCore/OfflineRunSimulator.cs Assets/Tests/EditMode/OfflineZoneStatsTests.cs
git commit -m "feat(stats): per-zone breakdown in offline run simulator"
```

---

### Task 2: RunStats — ZoneStats + animal counters + unified recorders

**Files:**
- Modify: `Assets/Scripts/RunStats.cs`

**Interfaces:**
- Produces (Task 3/5 use these EXACT signatures):
  - `RunStats.ZoneStats` class: `CropData crop; int harvested, moneyEarned, coinsBanked, eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted, deerRepelledByFence, crowsRepelledByScarecrow, wateredBySprinkler;`
  - `IReadOnlyDictionary<int, ZoneStats> ZoneStatsByZone { get; }` (sorted by zone id)
  - `void AddPlantDeath(int zoneId, CropData crop, string cause)` — updates aggregate AND zone
  - `void AddZoneHarvest(int zoneId, CropData crop, int money, int coins)` — zone-side only
  - `void AddCrowRepelled(int zoneId)` / `void AddDeerRepelled(int zoneId)` — replace `AddCrowRepelledByScarecrow()` / `AddDeerRepelledByFence()` (keep firing `OnCrowRepelled` / `OnDeerRepelled`)
  - `void AddSprinklerWatered(int zoneId)`
  - `void AddDeerChasedByDog()`; `int DeerChasedByDog { get; }`
  - `void AddCowEat(int compostLump)`; `int PlantsEatenByCow { get; }`; `int CompostFromCow { get; }`

No unit tests (MonoBehaviour singleton, thin counters; test assembly can't reference Assembly-CSharp). Verified by compile + Task 7 play-mode pass.

- [ ] **Step 1: Implement**

In `Assets/Scripts/RunStats.cs`:

Add fields/properties after `public int CoinsBanked { get; private set; }`:

```csharp
    public int DeerChasedByDog { get; private set; }
    public int PlantsEatenByCow { get; private set; }
    public int CompostFromCow { get; private set; }

    /// <summary>Per-zone breakdown for the run-stats zone cards. Keyed by FarmGrid ZoneID.</summary>
    public class ZoneStats
    {
        public CropData crop;    // set on first harvest/death recorded in the zone
        public int harvested, moneyEarned, coinsBanked;
        public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
        public int deerRepelledByFence, crowsRepelledByScarecrow, wateredBySprinkler;
    }

    private readonly SortedDictionary<int, ZoneStats> zoneStats = new SortedDictionary<int, ZoneStats>();
    public IReadOnlyDictionary<int, ZoneStats> ZoneStatsByZone => zoneStats;

    private ZoneStats Zone(int zoneId)
    {
        if (!zoneStats.TryGetValue(zoneId, out var z)) { z = new ZoneStats(); zoneStats[zoneId] = z; }
        return z;
    }
```

In `ResetStats()`, add at the end:

```csharp
        DeerChasedByDog = 0;
        PlantsEatenByCow = 0;
        CompostFromCow = 0;
        zoneStats.Clear();
```

Replace the four methods `AddPlantDehydrated / AddCropDecayed / AddPlantEatenByDeer / AddPlantEatenByCrow` and `AddDeerRepelledByFence / AddCrowRepelledByScarecrow` block with (keep `AddPlantStruckByLightning` too — it's folded in):

```csharp
    /// <summary>One recorder for every unharvested plant death: bumps the aggregate cause counter
    /// AND the per-zone card. Cause strings match Plant.Die.</summary>
    public void AddPlantDeath(int zoneId, CropData crop, string cause)
    {
        var z = Zone(zoneId);
        if (crop != null) z.crop = crop;
        switch (cause)
        {
            case "dry-out":   PlantsDehydrated++;        z.driedUp++;           break;
            case "rot":       CropsDecayed++;            z.rotted++;            break;
            case "deer":      PlantsEatenByDeer++;       z.eatenByDeer++;       break;
            case "crow":      PlantsEatenByCrows++;      z.eatenByCrows++;      break;
            case "lightning": PlantsStruckByLightning++; z.struckByLightning++; break;
        }
    }

    /// <summary>Zone-side harvest record (aggregates are recorded separately at the same call
    /// site via AddCropHarvested/AddCoinsBanked — do not double count).</summary>
    public void AddZoneHarvest(int zoneId, CropData crop, int money, int coins)
    {
        var z = Zone(zoneId);
        if (crop != null) z.crop = crop;
        z.harvested++;
        z.moneyEarned += money;
        z.coinsBanked += coins;
    }

    public void AddDeerRepelled(int zoneId)
    {
        DeerRepelledByFence++;
        Zone(zoneId).deerRepelledByFence++;
        OnDeerRepelled?.Invoke();
    }

    public void AddCrowRepelled(int zoneId)
    {
        CrowsRepelledByScarecrow++;
        Zone(zoneId).crowsRepelledByScarecrow++;
        OnCrowRepelled?.Invoke();
    }

    public void AddSprinklerWatered(int zoneId) => Zone(zoneId).wateredBySprinkler++;
    public void AddDeerChasedByDog() => DeerChasedByDog++;

    public void AddCowEat(int compostLump)
    {
        PlantsEatenByCow++;
        CompostFromCow += compostLump;
    }
```

Delete the now-superseded methods: `AddPlantStruckByLightning`, `AddPlantDehydrated`, `AddCropDecayed`, `AddPlantEatenByDeer`, `AddPlantEatenByCrow`, `AddDeerRepelledByFence`, `AddCrowRepelledByScarecrow`. (Their only callers are Plant.cs / EquipmentManager.cs — rewired in Task 3; `IngestOfflineResult` sets properties directly and is unaffected.)

Leave `GetDisplayStats()` untouched (legacy TMP path).

- [ ] **Step 2: Compile — expect errors ONLY in Plant.cs / EquipmentManager.cs**

Run `mcp__gladekit-unity__compile_scripts`. Expected: errors referencing the deleted methods in `Plant.cs` and `EquipmentManager.cs` (proceed straight to Task 3 which fixes them; commit happens at end of Task 3).

---

### Task 3: Wire live recording sites

**Files:**
- Modify: `Assets/Scripts/Plant.cs` (~lines 286–345 Harvest, ~447–464 Die)
- Modify: `Assets/Scripts/EquipmentManager.cs` (~452, ~479, ~528; `ZoneEquipmentState`; `UpdateSprinkler`; `WaterPlantsInRange`)
- Modify: `Assets/Scripts/FarmDog.cs` (~line 204)
- Modify: `Assets/Scripts/Cow.cs` (`EatPlant`, ~line 135)

**Interfaces:**
- Consumes Task 2's recorders exactly as declared there.

- [ ] **Step 1: Plant.cs — Die**

Replace the per-cause block in `Die(string cause)`:

```csharp
        if (RunStats.Instance != null)
            RunStats.Instance.AddPlantDeath(parentTile != null ? parentTile.ZoneID : -1, cropData, cause);
```

- [ ] **Step 2: Plant.cs — Harvest**

Hoist the coin amount so the zone record can see it. In `Harvest()`, immediately before the `if (!divertedToCannery && CurrencyManager.Instance != null && cropData.coinValue > 0)` block, add `int coinGain = 0;` and change that block's first line from `int coinGain = cropData.coinValue;` to `coinGain = cropData.coinValue;`.

Then directly after the existing `if (RunStats.Instance != null) RunStats.Instance.AddCropHarvested(cropData);` line, add:

```csharp
        // Per-zone card: harvest count always; money/coins only when actually paid out
        // (a cannery-diverted harvest pays jar progress, not currency).
        if (RunStats.Instance != null)
            RunStats.Instance.AddZoneHarvest(zone, cropData,
                divertedToCannery ? 0 : harvestValue,
                divertedToCannery ? 0 : coinGain);
```

(`zone` is the existing local from line ~301.)

- [ ] **Step 3: EquipmentManager.cs — repel + sprinkler recording**

- `TryRepelThreat` (~452): replace `RunStats.Instance.AddCrowRepelledByScarecrow();` with `RunStats.Instance.AddCrowRepelled(zoneId);`
- `CheckFlightPathInterception` (~479): replace with `RunStats.Instance.AddCrowRepelled(kvp.Key);`
- `TryFenceInterception` (~528): replace `RunStats.Instance.AddDeerRepelledByFence();` with `RunStats.Instance.AddDeerRepelled(kvp.Key);`
- `ZoneEquipmentState` class: add field

```csharp
        // Plants already counted as "watered" during the current sprinkler active phase,
        // so the run-stat counts each plant once per watering cycle (not once per frame).
        public readonly HashSet<Plant> wateredThisPhase = new HashSet<Plant>();
```

(add `using System.Collections.Generic;` if not present — it is, line 2.)

- `UpdateSprinkler`: where the inactive phase flips active (`state.sprinklerActive = true; state.sprinklerCycleTimer = state.data.activeDurationSeconds;`), add `state.wateredThisPhase.Clear();`
- `WaterPlantsInRange`: after `plant.ApplyRain(moistureThisFrame);` add:

```csharp
            if (plant != null && state.wateredThisPhase.Add(plant) && RunStats.Instance != null)
                RunStats.Instance.AddSprinklerWatered(state.zoneId);
```

- [ ] **Step 4: FarmDog.cs — chase success**

Inside `if (deer != null && !deer.IsDone)` after `deer.ForceRepel();`:

```csharp
            if (RunStats.Instance != null) RunStats.Instance.AddDeerChasedByDog();
```

- [ ] **Step 5: Cow.cs — eat record**

In `EatPlant`, after `CurrencyManager.Instance.AddCompost(lump);`:

```csharp
        if (RunStats.Instance != null && RunManager.Instance != null && RunManager.Instance.IsRunActive)
            RunStats.Instance.AddCowEat(lump);
```

- [ ] **Step 6: Compile clean + full EditMode suite passes**

`mcp__gladekit-unity__compile_scripts` → no errors. `mcp__unity-mcp__run_tests` EditMode full suite → all pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/RunStats.cs Assets/Scripts/Plant.cs Assets/Scripts/EquipmentManager.cs Assets/Scripts/FarmDog.cs Assets/Scripts/Cow.cs
git commit -m "feat(stats): per-zone + animal recording across live sites"
```

---

### Task 4: Offline plumbing — zoneId, cropById, per-zone ingest

**Files:**
- Modify: `Assets/Scripts/Economy/OfflineRunContextBuilder.cs`
- Modify: `Assets/Scripts/RunStats.cs` (IngestOfflineResult)
- Modify: `Assets/Scripts/OfflineProgressManager.cs` (~line 175 call site)

**Interfaces:**
- Consumes: `SimZone.zoneId`, `OfflineRunResult.zones` (Task 1); `RunStats.ZoneStats` (Task 2).
- Produces: `OfflineRunOutcome.cropById` (`Dictionary<string, CropData>`); `RunStats.IngestOfflineResult(OfflineRunOutcome outcome)` overload. Task 5 relies on `cropById`.

- [ ] **Step 1: OfflineRunContextBuilder**

In the `OfflineRunOutcome` class add:

```csharp
    public Dictionary<string, CropData> cropById = new Dictionary<string, CropData>();
```

In `BuildAndSimulate`, inside the `foreach (var kv in seeds)` zone loop, add `zoneId = kv.Key` to the `SimZone` initializer:

```csharp
            ctx.zones.Add(new SimZone
            {
                zoneId = kv.Key,
                tileCount = FarmGrid.Instance.TileCountPerZone,
                crop = new SimCrop { /* unchanged */ }
            });
```

And when building the outcome, hand over the map: after `var outcome = new OfflineRunOutcome { ... };` add `outcome.cropById = cropById;`

- [ ] **Step 2: RunStats.IngestOfflineResult overload**

Add to `RunStats.cs` (keep the existing parameter-list version; the new one wraps it):

```csharp
    /// <summary>Ingest a full offline outcome: aggregates + the per-zone cards, so the
    /// "Prev. Run Stats" popup after reopening matches the welcome-back modal exactly.</summary>
    public void IngestOfflineResult(OfflineRunOutcome outcome)
    {
        IngestOfflineResult(
            outcome.harvestedByCrop,
            outcome.result.eatenByDeer, outcome.result.eatenByCrows, outcome.result.struckByLightning,
            outcome.result.driedUp, outcome.result.rotted,
            outcome.result.seedsPlanted, outcome.result.moneyEarned, outcome.taxedCoins);

        foreach (var zs in outcome.result.zones)
        {
            var z = Zone(zs.zoneId);
            if (zs.cropId != null && outcome.cropById.TryGetValue(zs.cropId, out var crop)) z.crop = crop;
            z.harvested = zs.harvested;
            z.moneyEarned = zs.moneyEarned;
            z.coinsBanked = zs.coinsBanked;
            z.eatenByDeer = zs.eatenByDeer;
            z.eatenByCrows = zs.eatenByCrows;
            z.struckByLightning = zs.struckByLightning;
            z.driedUp = zs.driedUp;
            z.rotted = zs.rotted;
        }
    }
```

- [ ] **Step 3: OfflineProgressManager call site**

Replace the multi-arg `RunStats.Instance.IngestOfflineResult(outcome.harvestedByCrop, ...)` call (~line 175) with:

```csharp
            if (RunStats.Instance != null)
                RunStats.Instance.IngestOfflineResult(outcome);
```

- [ ] **Step 4: Compile clean, EditMode suite passes, commit**

```bash
git add Assets/Scripts/Economy/OfflineRunContextBuilder.cs Assets/Scripts/RunStats.cs Assets/Scripts/OfflineProgressManager.cs
git commit -m "feat(stats): thread per-zone offline results into RunStats"
```

---

### Task 5: RunLedgerData — zone cards + animal rows

**Files:**
- Modify: `Assets/Scripts/UI/RunLedgerData.cs`

**Interfaces:**
- Consumes: `RunStats.ZoneStatsByZone`, animal counters (Task 2); `OfflineRunResult.zones` + `OfflineRunOutcome.cropById` (Tasks 1/4); `EquipmentManager.Instance.GetAssignment(int zoneId)` → `EquipmentData` (existing, has `.equipmentType`); `AnimalManager.Instance.GetEquippedAnimalID()` / `GetAnimalData(string)` → `AnimalData` with `.iconSprite` (existing).
- Produces (Task 6 uses these EXACT members):

```csharp
public class LedgerZoneCard
{
    public int zoneId;
    public Sprite cropSprite; public string cropName;
    public int harvested, moneyEarned, coinsBanked;
    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
    public int? deerRepelled, crowsRepelled, wateredBySprinkler; // null = gear absent / not simulated -> hide line
}
// on RunLedgerData:
public readonly List<LedgerZoneCard> zoneCards = new List<LedgerZoneCard>();
public bool hasDog; public int deerChasedByDog; public Sprite dogSprite;
public bool hasCow; public int plantsEatenByCow, compostFromCow; public Sprite cowSprite;
```

- [ ] **Step 1: Implement**

Add `LedgerZoneCard` (top-level, next to `LedgerCropRow` which stays for now) and the new fields above to `RunLedgerData`. The flat loss/defense fields REMAIN (aggregates still power Economy totals and legacy checks).

In `FromCurrentRun()`, after the existing `if (rs != null)` body, add:

```csharp
            foreach (var kv in rs.ZoneStatsByZone)
            {
                var z = kv.Value;
                var card = new LedgerZoneCard
                {
                    zoneId = kv.Key,
                    cropSprite = z.crop != null ? (z.crop.cropSprite != null ? z.crop.cropSprite : z.crop.seedPacketSprite) : null,
                    cropName = z.crop != null ? z.crop.cropName : $"Zone {kv.Key}",
                    harvested = z.harvested, moneyEarned = z.moneyEarned, coinsBanked = z.coinsBanked,
                    eatenByDeer = z.eatenByDeer, eatenByCrows = z.eatenByCrows,
                    struckByLightning = z.struckByLightning, driedUp = z.driedUp, rotted = z.rotted,
                };
                // Equipment lines only for gear assigned to this zone (assignments persist post-run).
                var eq = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetAssignment(kv.Key) : null;
                if (eq != null)
                {
                    if (eq.equipmentType == EquipmentType.Fence)     card.deerRepelled = z.deerRepelledByFence;
                    if (eq.equipmentType == EquipmentType.Scarecrow) card.crowsRepelled = z.crowsRepelledByScarecrow;
                    if (eq.equipmentType == EquipmentType.Sprinkler) card.wateredBySprinkler = z.wateredBySprinkler;
                }
                d.zoneCards.Add(card);
            }

            // Animals: show the equipped animal even at zero, plus anything that recorded counts.
            string equippedAnimal = AnimalManager.Instance != null ? AnimalManager.Instance.GetEquippedAnimalID() : null;
            d.deerChasedByDog = rs.DeerChasedByDog;
            d.hasDog = rs.DeerChasedByDog > 0 || equippedAnimal == "farm_dog";
            d.plantsEatenByCow = rs.PlantsEatenByCow;
            d.compostFromCow = rs.CompostFromCow;
            d.hasCow = rs.PlantsEatenByCow > 0 || rs.CompostFromCow > 0 || equippedAnimal == "cow";
            if (d.compostGained == 0) d.compostGained = rs.CompostFromCow; // Economy line, live path
            if (AnimalManager.Instance != null)
            {
                var dog = AnimalManager.Instance.GetAnimalData("farm_dog");
                var cow = AnimalManager.Instance.GetAnimalData("cow");
                d.dogSprite = dog != null ? dog.iconSprite : null;
                d.cowSprite = cow != null ? cow.iconSprite : null;
            }
```

In `FromOffline(...)`, after the harvested loop, add (equipment/animal fields stay null/false — not simulated):

```csharp
        foreach (var zs in o.result.zones)
        {
            CropData crop = zs.cropId != null && o.cropById.TryGetValue(zs.cropId, out var c) ? c : null;
            d.zoneCards.Add(new LedgerZoneCard
            {
                zoneId = zs.zoneId,
                cropSprite = crop != null ? (crop.cropSprite != null ? crop.cropSprite : crop.seedPacketSprite) : null,
                cropName = crop != null ? crop.cropName : $"Zone {zs.zoneId}",
                harvested = zs.harvested, moneyEarned = zs.moneyEarned, coinsBanked = zs.coinsBanked,
                eatenByDeer = zs.eatenByDeer, eatenByCrows = zs.eatenByCrows,
                struckByLightning = zs.struckByLightning, driedUp = zs.driedUp, rotted = zs.rotted,
            });
        }
```

- [ ] **Step 2: Compile clean, commit**

```bash
git add Assets/Scripts/UI/RunLedgerData.cs
git commit -m "feat(stats): zone cards + animal rows in RunLedgerData"
```

---

### Task 6: RunStatsLedgerView + USS — icons, 2x2 Fields grid, Animals, spacing

**Files:**
- Modify: `Assets/Scripts/UI/RunStatsLedgerView.cs` (full rewrite of `Build` + helpers)
- Modify: `Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uss`
- Modify: `Assets/UI/OfflineProgressModalUITK/OfflineProgressModalUITK.uss`

**Interfaces:**
- Consumes: `RunLedgerData` exactly as produced in Task 5.

- [ ] **Step 1: Rewrite `RunStatsLedgerView.Build`**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the shared ledger (Economy / Fields / Animals) from a RunLedgerData into a container.
/// Used by RunStatsPopupUITK and both welcome-back modal variants — ONE layout for all four
/// run-stat windows. `compact` = the lighter welcome-back "Continue" summary (keeps Economy +
/// zone cards; drops equipment lines, Animals, and spent-on-bags).
/// NOTE: no emoji anywhere — currency/crop/animal icons are sprite-backed VisualElements.
/// </summary>
public static class RunStatsLedgerView
{
    public static void Build(VisualElement container, RunLedgerData d, bool compact)
    {
        container.Clear();

        // Economy — currency values with real icon sprites on the right.
        var econ = Section(container, "Economy");
        if (d.hasResumeMoney) Row(econ, "Money now", d.resumeMoney.ToString("N0"), null, "money");
        else if (!compact)
        {
            Row(econ, "Money earned", d.moneyEarned.ToString("N0"), null, "money");
            if (d.moneySpentOnBags > 0) Row(econ, "Spent on seed bags", "−" + d.moneySpentOnBags.ToString("N0"), "neg", "money");
        }
        Row(econ, "Coins banked", "+" + d.coinsBanked.ToString("N0"), "coin", "coins");
        if (d.compostGained > 0) Row(econ, "Compost gained", "+" + d.compostGained.ToString("N0"), "pos", "compost");
        Row(econ, "Total harvested", d.totalHarvested.ToString("N0"), "total");
        if (d.offlineTaxApplied) Row(econ, "after 30% offline tax", "applied", "dim");

        // Fields — 2x2 zone cards mirroring the farm.
        if (d.zoneCards.Count > 0)
        {
            Section(container, "Fields");
            var grid = new VisualElement(); grid.AddToClassList("zone-grid");
            container.Add(grid);
            foreach (var card in d.zoneCards) ZoneCard(grid, card, compact);
        }
        else
        {
            var none = Section(container, "Fields");
            Row(none, "Nothing planted", "0", "dim");
        }

        // Animals — small sprite icon rows; live full view only.
        if (!compact && (d.hasDog || d.hasCow))
        {
            var animals = Section(container, "Animals");
            if (d.hasDog) IconRow(animals, d.dogSprite, "Dog — deer chased off", d.deerChasedByDog.ToString());
            if (d.hasCow)
            {
                IconRow(animals, d.cowSprite, "Cow — plants eaten", d.plantsEatenByCow.ToString());
                IconRow(animals, d.cowSprite, "Cow — compost gained", "+" + d.compostFromCow.ToString("N0"), "compost");
            }
        }
    }

    // ── Zone card ────────────────────────────────────────────────────────

    private static void ZoneCard(VisualElement grid, LedgerZoneCard c, bool compact)
    {
        var card = new VisualElement(); card.AddToClassList("zone-card");

        var header = new VisualElement(); header.AddToClassList("zone-card__header");
        var icon = new VisualElement(); icon.AddToClassList("zone-card__icon");
        if (c.cropSprite != null) icon.style.backgroundImage = new StyleBackground(c.cropSprite);
        var name = new Label(c.cropName); name.AddToClassList("zone-card__name");
        var zone = new Label($"Z{c.zoneId}"); zone.AddToClassList("zone-card__zone");
        header.Add(icon); header.Add(name); header.Add(zone);
        card.Add(header);

        ZoneRow(card, "Harvested", c.harvested.ToString("N0"), c.harvested == 0, null, null);
        ZoneRow(card, "Cash", "+" + c.moneyEarned.ToString("N0"), c.moneyEarned == 0, "money", "pos");
        ZoneRow(card, "Coins", "+" + c.coinsBanked.ToString("N0"), c.coinsBanked == 0, "coins", "pos");

        ZoneRow(card, "Deer ate", c.eatenByDeer.ToString(), c.eatenByDeer == 0, null, "neg");
        ZoneRow(card, "Crows ate", c.eatenByCrows.ToString(), c.eatenByCrows == 0, null, "neg");
        ZoneRow(card, "Lightning", c.struckByLightning.ToString(), c.struckByLightning == 0, null, "neg");
        ZoneRow(card, "Dried up", c.driedUp.ToString(), c.driedUp == 0, null, "neg");
        ZoneRow(card, "Rotted", c.rotted.ToString(), c.rotted == 0, null, "neg");

        if (!compact)
        {
            if (c.deerRepelled.HasValue)      ZoneRow(card, "Fence blocked", c.deerRepelled.Value.ToString(), false, null, "def");
            if (c.crowsRepelled.HasValue)     ZoneRow(card, "Scarecrow scared", c.crowsRepelled.Value.ToString(), false, null, "def");
            if (c.wateredBySprinkler.HasValue) ZoneRow(card, "Sprinkler watered", c.wateredBySprinkler.Value.ToString(), false, null, "def");
        }

        grid.Add(card);
    }

    private static void ZoneRow(VisualElement card, string label, string value, bool zero, string iconClass, string mod)
    {
        var row = new VisualElement(); row.AddToClassList("zone-row");
        if (zero) row.AddToClassList("zone-row--zero");
        var l = new Label(label); l.AddToClassList("zone-row__label");
        var group = new VisualElement(); group.AddToClassList("stat-row__value-group");
        if (iconClass != null) group.Add(CurrencyIcon(iconClass, small: true));
        var v = new Label(value); v.AddToClassList("zone-row__value");
        if (mod == "neg" && !zero) v.AddToClassList("stat-row__value--negative");
        if (mod == "pos" && !zero) v.AddToClassList("stat-row__value--positive");
        if (mod == "def") v.AddToClassList("zone-row__value--defense");
        group.Add(v);
        row.Add(l); row.Add(group);
        card.Add(row);
    }

    // ── Shared rows ──────────────────────────────────────────────────────

    private static VisualElement Section(VisualElement parent, string title)
    {
        var header = new Label(title); header.AddToClassList("section-title");
        parent.Add(header);
        var body = new VisualElement(); body.AddToClassList("section");
        parent.Add(body);
        return body;
    }

    private static VisualElement CurrencyIcon(string kind, bool small)
    {
        var icon = new VisualElement();
        icon.AddToClassList(small ? "ledger-currency-icon--small" : "ledger-currency-icon");
        icon.AddToClassList("currency-icon--" + kind); // money / coins / gems / compost
        return icon;
    }

    private static void Row(VisualElement parent, string label, string value, string valueMod, string currency = null)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        if (valueMod == "total") row.AddToClassList("stat-row--total");
        if (valueMod == "coin")  row.AddToClassList("stat-row--coin");
        if (valueMod == "dim")   row.AddToClassList("stat-row--dim");
        var l = new Label(label); l.AddToClassList("stat-row__label");

        var valueGroup = new VisualElement(); valueGroup.AddToClassList("stat-row__value-group");
        if (!string.IsNullOrEmpty(currency)) valueGroup.Add(CurrencyIcon(currency, small: false));
        var v = new Label(value); v.AddToClassList("stat-row__value");
        if (valueMod == "neg") v.AddToClassList("stat-row__value--negative");
        if (valueMod == "pos" || valueMod == "coin") v.AddToClassList("stat-row__value--positive");
        valueGroup.Add(v);

        row.Add(l); row.Add(valueGroup);
        parent.Add(row);
    }

    private static void IconRow(VisualElement parent, Sprite sprite, string label, string value, string currency = null)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        var left = new VisualElement(); left.AddToClassList("crop-row__left");
        var icon = new VisualElement(); icon.AddToClassList("crop-row__icon");
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
        var name = new Label(label); name.AddToClassList("stat-row__label");
        left.Add(icon); left.Add(name);
        var group = new VisualElement(); group.AddToClassList("stat-row__value-group");
        if (currency != null) group.Add(CurrencyIcon(currency, small: false));
        var v = new Label(value); v.AddToClassList("stat-row__value");
        v.AddToClassList("stat-row__value--positive");
        group.Add(v);
        row.Add(left); row.Add(group);
        parent.Add(row);
    }
}
```

Note: the old `CropRow` + the Harvested/Losses/Defense sections are gone; `LedgerCropRow`/`d.harvested` stay in RunLedgerData but are no longer rendered (leave them — removal is a separate cleanup).

- [ ] **Step 2: USS additions — append the SAME block to BOTH files**

Append to `Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uss` AND `Assets/UI/OfflineProgressModalUITK/OfflineProgressModalUITK.uss`:

```css
/* ── Per-zone Fields grid + currency icons (2026-07-14) ─────────────── */
.zone-grid { flex-direction: row; flex-wrap: wrap; justify-content: space-between; margin-bottom: 10px; }
.zone-card {
    width: 48.5%;
    background-color: rgb(34,26,14); border-color: rgb(90,74,48);
    border-left-width: 1px; border-right-width: 1px; border-top-width: 1px; border-bottom-width: 1px;
    border-top-left-radius: 12px; border-top-right-radius: 12px; border-bottom-left-radius: 12px; border-bottom-right-radius: 12px;
    padding-top: 10px; padding-bottom: 10px; padding-left: 12px; padding-right: 12px;
    margin-bottom: 14px;
}
.zone-card__header { flex-direction: row; align-items: center; margin-bottom: 8px; border-bottom-width: 1px; border-bottom-color: rgba(255,240,210,0.14); padding-bottom: 7px; }
.zone-card__icon { width: 34px; height: 34px; margin-right: 8px; -unity-background-scale-mode: scale-to-fit; flex-shrink: 0; }
.zone-card__name { font-size: 21px; -unity-font-style: bold; color: rgb(245,233,200); flex-shrink: 1; }
.zone-card__zone { font-size: 14px; color: rgb(140,126,98); margin-left: 6px; }
.zone-row { flex-direction: row; justify-content: space-between; align-items: center; padding-top: 2px; padding-bottom: 2px; }
.zone-row--zero { opacity: 0.4; }
.zone-row__label { color: rgb(200,184,150); font-size: 17px; }
.zone-row__value { color: rgb(255,255,255); font-size: 18px; -unity-font-style: bold; }
.zone-row__value--defense { color: rgb(120,190,235); }

.ledger-currency-icon { width: 26px; height: 26px; margin-right: 6px; -unity-background-scale-mode: scale-to-fit; flex-shrink: 0; }
.ledger-currency-icon--small { width: 20px; height: 20px; margin-right: 5px; -unity-background-scale-mode: scale-to-fit; flex-shrink: 0; }
.currency-icon--money   { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Cash.png"); }
.currency-icon--coins   { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Coin.png"); }
.currency-icon--gems    { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Gem.png"); }
.currency-icon--compost { background-image: url("project://database/Assets/Sprites/UI/Icons/Cyberpunk/Plants/Seedling_Dirt.png"); }
```

Then in BOTH files bump the section spacing (edit existing rules):
- `.section-title` → `margin-top: 30px;` (was 22 in RunStatsPopupUITK.uss; adjust the same property in the offline modal's USS to 30 whatever its current value)
- `.section` → `margin-bottom: 26px;` (was 16)

- [ ] **Step 3: Compile + full EditMode suite**

`mcp__gladekit-unity__compile_scripts` → clean. Suite → pass (view has no tests; this catches signature drift).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/RunStatsLedgerView.cs Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uss Assets/UI/OfflineProgressModalUITK/OfflineProgressModalUITK.uss
git commit -m "feat(stats): 2x2 zone cards, Animals section, currency icons in shared ledger view"
```

---

### Task 7: Play-mode verification of all four surfaces

**Files:** none (verification only; screenshots to scratchpad).

- [ ] **Step 1: Enter play mode** (`mcp__unity-mcp__manage_editor` action `play`).

- [ ] **Step 2: Current Run window** — via `mcp__unity-mcp__execute_code`: start a run (`RunManager.Instance.StartNewRun()` — ensure crops equipped first via `SeedSelectionPopup.Instance.HasAnyCropEquipped()`), let helpers plant/harvest ~20s (or bump `Time.timeScale` via GameSpeedControl), then `RunStatsPopupUITK.Instance.Show()`. Capture with `UnityEngine.ScreenCapture.CaptureScreenshot(<scratchpad path>)` (delayed via `EditorApplication.update` if needed — overlay/UITK do NOT appear in gladekit `look_at_game_view`). Verify: Economy icons render (Cash/Coin sprites, not text), zone cards appear 2-up with crop icons, zero rows dimmed, equipment lines only on equipped zones, Animals section shows equipped animal.

- [ ] **Step 3: Prev Run window** — `RunManager.Instance.EndRun()`, reopen `RunStatsPopupUITK.Instance.Show()`. Verify same layout + "Run Complete" banner.

- [ ] **Step 4: Welcome-back (failed) variant** — simulate: build an outcome via `OfflineRunContextBuilder.BuildAndSimulate(3600f, 0f, <lowMoney>)` and call `RunStatsPopupUITK.Instance.Show(RunLedgerData.FromOffline(outcome, TimeSpan.FromHours(1)), "away for 1h")`. Verify zone cards present WITHOUT equipment/animal lines, tax row shown.

- [ ] **Step 5: Welcome-back (survived/compact) variant** — locate the `OfflineProgressModalUITK` path that calls `RunStatsLedgerView.Build(..., compact: true)` and drive it with the same outcome (or temporarily call `Build` on its ledger element via reflection). Verify compact keeps Economy + cards, drops equipment/Animals.

- [ ] **Step 6: Exit play mode**, fix anything found, re-verify, then final commit of any fixes:

```bash
git add -A
git commit -m "fix(stats): play-verification fixes for zone-card ledger"
```

---

## Self-review notes (done at plan time)

- Spec coverage: currency icons (T6), 2x2 zone cards (T5/T6), per-crop losses (T1–T4), worth-with-icons (T5/T6), equipment-in-cards (T3/T5/T6), animals w/ sprites (T3/T5/T6), spacing (T6), all four windows via shared view (T6), Total harvested moved to Economy (T6), tests (T1). Gems class included for future use (T6 USS).
- Type consistency: `ZoneSimStats` (sim) vs `RunStats.ZoneStats` (live) vs `LedgerZoneCard` (view) are deliberately separate types across assembly boundaries; field names align.
- Known simplification: `LedgerCropRow`/`d.harvested` become dead data (left for a later cleanup); `RunStats.GetDisplayStats()` legacy TMP path untouched.
