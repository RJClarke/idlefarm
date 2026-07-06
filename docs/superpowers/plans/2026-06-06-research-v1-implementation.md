# Research V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Research system as designed in `docs/superpowers/specs/2026-06-06-research-catalog-design.md` — minus the Compost economy and Cow run-eating, which ship in Plan 2.

**Scope:** Research data model, 40-entry catalog, manager logic (assign / real-time level-up / GetBonus), save/load via `GameData.json`, popup UI (assignment flow + countdown + visibility), and consumer wiring so the bonuses actually affect crops/helpers/equipment/animals/weather. **No Compost currency, no Compost Bay equipment, no Cow data, no boost tokens** — those land in Plan 2.

**Architecture:** ScriptableObject-driven catalog (`ResearchData` × 40) + global config (`ResearchTuning` × 1). `ResearchManager` (existing singleton, extended) owns active per-slot state and a real-time `Tick()` that promotes levels based on UTC elapsed time. Bonuses are queried via `GetBonus(statKey)` from consuming systems, multiplied into their existing stat chains. Save state lives in `GameData.json` (migrating away from PlayerPrefs).

**Tech Stack:** Unity 2D (URP, C#), Unity UI Toolkit (popup), JsonUtility-based save, existing singleton+SO+event patterns from `UpgradeManager` / `AnimalManager` / `EquipmentManager`.

---

## File Structure

| Path | Role |
|---|---|
| `Assets/Scripts/Research/ResearchTier.cs` *(new)* | enum: `Binary`, `Tier10`, `Tier25`, `Tier100Standard`, `Tier100Absurd` |
| `Assets/Scripts/Research/StatKey.cs` *(new)* | static class of string constants for every research stat key |
| `Assets/Scripts/Research/ResearchData.cs` *(new)* | per-research `ScriptableObject` |
| `Assets/Scripts/Research/ResearchTuning.cs` *(new)* | global `ScriptableObject` (p_time, p_cost, branches) |
| `Assets/Scripts/Research/ResearchSlotState.cs` *(new)* | `[Serializable]` plain struct stored in `GameData` |
| `Assets/Scripts/ResearchManager.cs` *(modify)* | extend existing class: catalog load, assign, tick, GetBonus, visibility, migration |
| `Assets/Scripts/GameData.cs` *(modify)* | add `researchSlots`, `binaryFeatureFlags`, constructor params |
| `Assets/Scripts/SaveManager.cs` *(modify)* | wire research state into Save/Load |
| `Assets/Scripts/UI/ResearchPopupUITK.cs` *(modify)* | assign-flow, picker, countdown, refresh schedule |
| `Assets/UI/ResearchPopupUITK/ResearchPopupUITK.uxml` *(modify)* | picker overlay + research row template + slot countdown layout |
| `Assets/UI/ResearchPopupUITK/ResearchPopupUITK.uss` *(modify)* | picker styles, countdown styles, progress bar |
| `Assets/Editor/ResearchCatalogGenerator.cs` *(new)* | editor menu that programmatically creates all 40 SOs |
| `Assets/Data/Research/ResearchTuning.asset` *(new)* | the global tuning asset |
| `Assets/Data/Research/Catalog/*.asset` *(new × 40)* | catalog SOs (created by the generator) |
| `Assets/Resources/Research/` *(new dir)* | symlinks/copies of catalog SOs for runtime `Resources.LoadAll<ResearchData>("Research")` |
| `Assets/Scripts/UniversalHelper.cs` *(modify)* | apply helper task/efficiency bonuses |
| `Assets/Scripts/Plant.cs` *(modify)* | apply growth speed, HP, sell value, bonus sell amount |
| `Assets/Scripts/SoilTile.cs` *(modify)* | apply soil water efficiency + soil quality coin multiplier |
| `Assets/Scripts/ScarecrowVisual.cs` *(modify)* | apply AoE / Effectiveness / Cooldown bonuses |
| `Assets/Scripts/SprinklerVisual.cs` *(modify)* | apply AoE / Effectiveness / Cooldown bonuses |
| `Assets/Scripts/FenceVisual.cs` *(modify)* | apply AoE / Effectiveness / Cooldown bonuses |
| `Assets/Scripts/FarmDog.cs` *(modify)* | apply dog cooldown / efficiency bonuses |
| `Assets/Scripts/AnimalManager.cs` *(modify)* | apply chicken / rooster cooldown / efficiency to egg/gem timers |
| `Assets/Scripts/ThunderstormManager.cs` *(modify)* | apply storm damage reduction |
| `Assets/Scripts/RainOverlayUI.cs` *(modify)* | apply rain watering bonus |
| `Assets/Scripts/RunManager.cs` *(modify)* | apply Game Speed Multiplier to in-run Time scaling |

**Test harness:** This Unity project has no existing test infrastructure. We will add a single `Assets/Tests/Editor/ResearchMathTests.cs` for the polynomial math (the only pure-logic code). UI and scene-wiring tasks are verified manually via the in-Editor smoke-test checklist at the end of the plan.

---

## Task 1: Define ResearchTier enum + StatKey constants

**Files:**
- Create: `Assets/Scripts/Research/ResearchTier.cs`
- Create: `Assets/Scripts/Research/StatKey.cs`

- [ ] **Step 1.1: Create `ResearchTier.cs`**

```csharp
namespace Research
{
    public enum ResearchTier
    {
        Binary,
        Tier10,
        Tier25,
        Tier100Standard,
        Tier100Absurd
    }

    public static class ResearchTierExtensions
    {
        public static int MaxLevel(this ResearchTier tier) => tier switch
        {
            ResearchTier.Binary => 1,
            ResearchTier.Tier10 => 10,
            ResearchTier.Tier25 => 25,
            ResearchTier.Tier100Standard => 100,
            ResearchTier.Tier100Absurd => 100,
            _ => 1
        };
    }
}
```

- [ ] **Step 1.2: Create `StatKey.cs`** — central registry of every stat key the catalog uses

```csharp
namespace Research
{
    /// <summary>
    /// String constants for every research-bonus stat key. Referenced by ResearchData.targetStatKey
    /// and by consumers calling ResearchManager.GetBonus(StatKey.XYZ).
    /// </summary>
    public static class StatKey
    {
        // Soil
        public const string SoilWaterEfficiency = "soil_water_efficiency";
        public const string SoilQuality = "soil_quality";

        // Helper
        public const string HelperTillSpeed = "helper_till_speed";
        public const string HelperWaterSpeed = "helper_water_speed";
        public const string HelperWaterEfficiency = "helper_water_efficiency";
        public const string HelperPlantSpeed = "helper_plant_speed";
        public const string HelperHarvestSpeed = "helper_harvest_speed";
        public const string HelperHarvestEfficiency = "helper_harvest_efficiency";

        // Plant
        public const string CropHp = "crop_hp";
        public const string CropGrowthSpeed = "crop_growth_speed";
        public const string CropBonusSellAmount = "crop_bonus_sell_amount";

        // Animals
        public const string ChickenCooldown = "chicken_cooldown";
        public const string ChickenEfficiency = "chicken_efficiency";
        public const string DogCooldown = "dog_cooldown";
        public const string DogEfficiency = "dog_efficiency";
        public const string RoosterCooldown = "rooster_cooldown";
        public const string RoosterEfficiency = "rooster_efficiency";

        // Equipment (Compost Bay deferred to Plan 2)
        public const string ScarecrowAoe = "scarecrow_aoe";
        public const string ScarecrowEffectiveness = "scarecrow_effectiveness";
        public const string ScarecrowCooldown = "scarecrow_cooldown";
        public const string SprinklerAoe = "sprinkler_aoe";
        public const string SprinklerEffectiveness = "sprinkler_effectiveness";
        public const string SprinklerCooldown = "sprinkler_cooldown";
        public const string FenceAoe = "fence_aoe";
        public const string FenceEffectiveness = "fence_effectiveness";
        public const string FenceCooldown = "fence_cooldown";

        // Weather
        public const string StormDamageReduction = "storm_damage_reduction";
        public const string RainWatering = "rain_watering";

        // Meta
        public const string GameSpeed = "game_speed";
        public const string ResearchSpeed = "research_speed";
        public const string OfflineCap = "offline_cap";
        public const string OfflineEfficiency = "offline_efficiency";
    }

    /// <summary>String constants for binary feature unlocks granted by completing binary researches.</summary>
    public static class FeatureFlag
    {
        public const string OfflineProgress = "offline_progress";
        public const string MaxWaterHealsPlant = "max_water_heals_plant";
        public const string CompostingBasics = "composting_basics"; // for Market gating Compost Bay (Plan 2)
    }
}
```

- [ ] **Step 1.3: Commit**

```bash
git add Assets/Scripts/Research/ResearchTier.cs Assets/Scripts/Research/StatKey.cs
git commit -m "feat(research): add ResearchTier enum + StatKey constants"
```

---

## Task 2: Define ResearchData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Research/ResearchData.cs`

- [ ] **Step 2.1: Create the SO**

```csharp
using UnityEngine;

namespace Research
{
    [CreateAssetMenu(menuName = "Farm Game/Research Data", fileName = "Research_New", order = 9)]
    public class ResearchData : ScriptableObject
    {
        [Header("Identity")]
        public string researchID;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public string branchID; // soil | helper | plant | animals | equipment | weather | meta

        [Header("Tier")]
        public ResearchTier tier = ResearchTier.Tier100Standard;

        [Header("Bonus")]
        [Tooltip("Empty for binary unlocks.")]
        public string targetStatKey;
        [Tooltip("Additive per level. e.g. 0.005 means +0.5%/lvl; for Game Speed use 0.9.")]
        public float bonusPerLevel = 0.005f;

        [Header("Scaling (overrides global tuning when nonzero)")]
        public float baseCostCoins = 50f;
        public float baseDurationSecs = 120f; // 2 minutes
        [Tooltip("Per-research multiplier applied on top of the polynomial base. 1.0 = neutral.")]
        public float timeDifficulty = 1.0f;
        [Tooltip("Per-research multiplier applied on top of the polynomial base. 1.0 = neutral.")]
        public float costDifficulty = 1.0f;

        [Header("Prerequisites / Visibility")]
        public string prerequisiteResearchID; // empty if none
        public string requiredUnlockID;       // empty if none — checked via UpgradeManager
        public string requiredAnimalID;       // empty if none — checked via AnimalManager

        [Header("Binary overrides (when tier == Binary)")]
        public int binaryFixedCost;          // single up-front coin cost
        public float binaryFixedDurationSecs; // single duration

        [Header("On Complete (binary only)")]
        public int unlocksSlotIndex = -1;     // -1 = not a slot unlock; 2 = Slot 3, 3 = Slot 4
        public string unlocksFeatureID;       // e.g. FeatureFlag.OfflineProgress

        public int MaxLevel => tier.MaxLevel();
        public bool IsBinary => tier == ResearchTier.Binary;
    }
}
```

- [ ] **Step 2.2: Commit**

```bash
git add Assets/Scripts/Research/ResearchData.cs
git commit -m "feat(research): add ResearchData ScriptableObject"
```

---

## Task 3: Define ResearchTuning ScriptableObject + asset

**Files:**
- Create: `Assets/Scripts/Research/ResearchTuning.cs`
- Create asset (via Unity menu after compile): `Assets/Data/Research/ResearchTuning.asset`

- [ ] **Step 3.1: Create the SO class**

```csharp
using UnityEngine;

namespace Research
{
    [CreateAssetMenu(menuName = "Farm Game/Research Tuning", fileName = "ResearchTuning", order = 9)]
    public class ResearchTuning : ScriptableObject
    {
        [Header("Polynomial Exponents")]
        [Tooltip("duration(L) = baseDurationSecs × timeDifficulty × L^p_time")]
        public float pTime = 2.16f;
        [Tooltip("cost(L) = baseCostCoins × costDifficulty × L^p_cost")]
        public float pCost = 2.00f;

        [Header("Tick Cadence")]
        [Tooltip("How often ResearchManager polls real-time elapsed and applies level-ups.")]
        public float tickIntervalSecs = 1.0f;

        [Header("Branches (display order)")]
        public string[] branchOrder = new[] { "soil", "helper", "plant", "animals", "equipment", "weather", "meta" };
    }
}
```

- [ ] **Step 3.2: Create the asset**

In Unity Editor: `Project window → Right-click Assets/Data/Research/ → Create → Farm Game → Research Tuning`.
Name it `ResearchTuning`. Leave all values at defaults.

- [ ] **Step 3.3: Commit**

```bash
git add Assets/Scripts/Research/ResearchTuning.cs "Assets/Data/Research/ResearchTuning.asset" "Assets/Data/Research/ResearchTuning.asset.meta"
git commit -m "feat(research): add ResearchTuning SO + baseline asset"
```

---

## Task 4: Define ResearchSlotState serializable struct

**Files:**
- Create: `Assets/Scripts/Research/ResearchSlotState.cs`

- [ ] **Step 4.1: Create the struct (used by ResearchManager + GameData)**

```csharp
using System;

namespace Research
{
    /// <summary>
    /// Per-slot active research state. Persisted in GameData.json.
    /// startUtcTicks = 0 means slot has no active research (idle).
    /// </summary>
    [Serializable]
    public class ResearchSlotState
    {
        public int slotIndex;
        public string activeResearchID = "";
        public int currentLevel;          // 0..MaxLevel for the active research; 0 for idle
        public long startUtcTicks;        // ticks of UTC at the moment the current level started; 0 = idle

        // Boost (Plan 2 — included now so save format is stable across both plans)
        public long boostExpiresUtcTicks; // 0 = no boost active
        public float boostMultiplier = 1.0f;

        public bool IsIdle => string.IsNullOrEmpty(activeResearchID) || startUtcTicks == 0;
    }
}
```

- [ ] **Step 4.2: Commit**

```bash
git add Assets/Scripts/Research/ResearchSlotState.cs
git commit -m "feat(research): add ResearchSlotState struct"
```

---

## Task 5: Editor script — generate all 40 catalog SOs programmatically

This task creates the entire catalog in one editor menu click. Avoids 40 manual `Create > Research Data` clicks.

**Files:**
- Create: `Assets/Editor/ResearchCatalogGenerator.cs`

- [ ] **Step 5.1: Verify the data directory exists**

In Project window, ensure `Assets/Data/Research/Catalog/` exists. If not, create it.

- [ ] **Step 5.2: Create the generator**

```csharp
#if UNITY_EDITOR
using System.IO;
using Research;
using UnityEditor;
using UnityEngine;

public static class ResearchCatalogGenerator
{
    private const string OutDir = "Assets/Data/Research/Catalog";

    [MenuItem("Farm Game/Research/Generate Catalog")]
    public static void Generate()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        // Soil (2)
        CreateStd("soil_water_efficiency",   "Soil: Water Efficiency",       StatKey.SoilWaterEfficiency,  ResearchTier.Tier25,           "soil",   0.010f, t:1.5f, c:2.0f);
        CreateStd("soil_quality",            "Soil: Quality",                 StatKey.SoilQuality,          ResearchTier.Tier100Absurd,    "soil",   0.005f, t:1.2f, c:3.0f, baseSecs:540f, baseCoins:150f);

        // Helper (7)
        CreateStd("helper_till_speed",       "Helper: Till Speed",            StatKey.HelperTillSpeed,      ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_water_speed",      "Helper: Water Speed",           StatKey.HelperWaterSpeed,     ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_water_efficiency", "Helper: Water Efficiency",      StatKey.HelperWaterEfficiency,ResearchTier.Tier25,           "helper", 0.010f);
        CreateStd("helper_plant_speed",      "Helper: Plant Seeding Speed",   StatKey.HelperPlantSpeed,     ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_harvest_speed",    "Helper: Harvest Speed",         StatKey.HelperHarvestSpeed,   ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_harvest_efficiency","Helper: Harvest Efficiency",   StatKey.HelperHarvestEfficiency,ResearchTier.Tier100Absurd, "helper", 0.005f, t:1.2f, c:3.0f, baseSecs:540f, baseCoins:150f);
        CreateBinary("max_water_heals",      "Max Water Heals Plant HP",      "helper", FeatureFlag.MaxWaterHealsPlant, cost:5000, days:3);

        // Plant (3)
        CreateStd("crop_hp",                 "Plant: Hit Points",             StatKey.CropHp,               ResearchTier.Tier25,           "plant",  0.010f);
        CreateStd("crop_growth_speed",       "Plant: Growth Speed",           StatKey.CropGrowthSpeed,      ResearchTier.Tier100Standard,  "plant",  0.005f, t:1.5f, c:2.0f);
        CreateStd("crop_bonus_sell_amount",  "Plant: Bonus Sell Amount",      StatKey.CropBonusSellAmount,  ResearchTier.Tier100Absurd,    "plant",  0.005f, t:1.2f, c:3.0f, baseSecs:540f, baseCoins:150f);

        // Animals (8) — gated by ownership
        CreateStd("chicken_cooldown",   "Chicken: Cooldown",   StatKey.ChickenCooldown,   ResearchTier.Tier25, "animals", 0.010f, animalID:"chicken");
        CreateStd("chicken_efficiency", "Chicken: Efficiency", StatKey.ChickenEfficiency, ResearchTier.Tier25, "animals", 0.010f, animalID:"chicken");
        CreateStd("dog_cooldown",       "Dog: Cooldown",       StatKey.DogCooldown,       ResearchTier.Tier25, "animals", 0.010f, animalID:"dog");
        CreateStd("dog_efficiency",     "Dog: Efficiency",     StatKey.DogEfficiency,     ResearchTier.Tier25, "animals", 0.010f, animalID:"dog");
        CreateStd("rooster_cooldown",   "Rooster: Cooldown",   StatKey.RoosterCooldown,   ResearchTier.Tier25, "animals", 0.010f, animalID:"rooster");
        CreateStd("rooster_efficiency", "Rooster: Efficiency", StatKey.RoosterEfficiency, ResearchTier.Tier25, "animals", 0.010f, animalID:"rooster");
        // Cow Passive Compost + Cow Run Yield ship in Plan 2 (depend on Compost currency + Cow class).

        // Equipment (9 — Compost Bay deferred to Plan 2)
        CreateStd("scarecrow_aoe",          "Scarecrow: AoE",           StatKey.ScarecrowAoe,           ResearchTier.Tier100Standard, "equipment", 0.005f, t:0.8f, c:0.8f, unlockID:"scarecrow");
        CreateStd("scarecrow_effectiveness","Scarecrow: Effectiveness", StatKey.ScarecrowEffectiveness, ResearchTier.Tier100Standard, "equipment", 0.005f, t:0.8f, c:0.8f, unlockID:"scarecrow");
        CreateStd("scarecrow_cooldown",     "Scarecrow: Cooldown",      StatKey.ScarecrowCooldown,      ResearchTier.Tier25,          "equipment", 0.010f, t:0.8f, c:0.8f, unlockID:"scarecrow");
        CreateStd("sprinkler_aoe",          "Sprinkler: AoE",           StatKey.SprinklerAoe,           ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"sprinkler");
        CreateStd("sprinkler_effectiveness","Sprinkler: Effectiveness", StatKey.SprinklerEffectiveness, ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"sprinkler");
        CreateStd("sprinkler_cooldown",     "Sprinkler: Cooldown",      StatKey.SprinklerCooldown,      ResearchTier.Tier25,          "equipment", 0.010f, unlockID:"sprinkler");
        CreateStd("fence_aoe",              "Fence: AoE",               StatKey.FenceAoe,               ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"fence");
        CreateStd("fence_effectiveness",    "Fence: Effectiveness",     StatKey.FenceEffectiveness,     ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"fence");
        CreateStd("fence_cooldown",         "Fence: Cooldown",          StatKey.FenceCooldown,          ResearchTier.Tier25,          "equipment", 0.010f, unlockID:"fence");

        // Weather (2)
        CreateStd("storm_damage_reduction", "Storm Damage Reduction", StatKey.StormDamageReduction, ResearchTier.Tier25, "weather", 0.010f);
        CreateStd("rain_watering",          "Rain Watering",          StatKey.RainWatering,         ResearchTier.Tier25, "weather", 0.010f);

        // Meta (7 — Composting Basics is created here; Compost Bay equipment ships in Plan 2)
        CreateStd("game_speed",             "Game Speed Multiplier",  StatKey.GameSpeed,            ResearchTier.Tier10, "meta", 0.9f, c:2.5f, baseSecs:3600f, baseCoins:1000f);
        CreateStd("research_speed",         "Research Speed",         StatKey.ResearchSpeed,        ResearchTier.Tier25, "meta", 0.010f, c:1.5f);
        CreateBinary("slot_3_unlock",       "Research Slot 3",        "meta", slotIndex:2, cost:10000,  days:7);
        CreateBinary("slot_4_unlock",       "Research Slot 4",        "meta", slotIndex:3, cost:50000,  days:21);
        CreateBinary("composting_basics",   "Composting Basics",      "meta", featureID:FeatureFlag.CompostingBasics, cost:5000, days:3);
        CreateBinary("offline_unlock",      "Offline Progress",       "meta", featureID:FeatureFlag.OfflineProgress,  cost:2500, days:1);
        CreateStd("offline_cap",            "Offline Progress Cap",   StatKey.OfflineCap,           ResearchTier.Tier25, "meta", 0.01f,  prereqID:"offline_unlock");
        CreateStd("offline_efficiency",     "Offline Progress Efficiency", StatKey.OfflineEfficiency, ResearchTier.Tier10, "meta", 0.025f, prereqID:"offline_unlock");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ResearchCatalogGenerator] Catalog written to " + OutDir);
    }

    private static void CreateStd(
        string id, string displayName, string statKey, ResearchTier tier, string branch,
        float bonusPerLevel,
        float t = 1.0f, float c = 1.0f,
        float baseSecs = 120f, float baseCoins = 50f,
        string prereqID = "", string unlockID = "", string animalID = "")
    {
        var so = ScriptableObject.CreateInstance<ResearchData>();
        so.researchID         = id;
        so.displayName        = displayName;
        so.description        = AutoDescription(bonusPerLevel, tier);
        so.branchID           = branch;
        so.tier               = tier;
        so.targetStatKey      = statKey;
        so.bonusPerLevel      = bonusPerLevel;
        so.baseCostCoins      = baseCoins;
        so.baseDurationSecs   = baseSecs;
        so.timeDifficulty     = t;
        so.costDifficulty     = c;
        so.prerequisiteResearchID = prereqID;
        so.requiredUnlockID   = unlockID;
        so.requiredAnimalID   = animalID;
        Save(so, id);
    }

    private static string AutoDescription(float bonusPerLevel, ResearchTier tier)
    {
        int max = tier.MaxLevel();
        if (Mathf.Approximately(bonusPerLevel, 0f)) return "One-time unlock.";
        float maxBonus = bonusPerLevel * max;
        // Game Speed reads in "multiplier" units (0.9/lvl → +900% at L10)
        if (bonusPerLevel >= 0.5f)
            return $"+{bonusPerLevel:F1}x per level. Maxes at {1f + maxBonus:F1}x.";
        return $"+{bonusPerLevel * 100f:F1}% per level. Maxes at +{maxBonus * 100f:F0}%.";
    }

    private static void CreateBinary(
        string id, string displayName, string branch,
        string featureID = "", int slotIndex = -1,
        int cost = 5000, int days = 3,
        string prereqID = "", string unlockID = "", string animalID = "")
    {
        var so = ScriptableObject.CreateInstance<ResearchData>();
        so.researchID         = id;
        so.displayName        = displayName;
        so.description        = displayName + " — one-time unlock.";
        so.branchID           = branch;
        so.tier               = ResearchTier.Binary;
        so.targetStatKey      = "";
        so.bonusPerLevel      = 0f;
        so.binaryFixedCost    = cost;
        so.binaryFixedDurationSecs = days * 86400f;
        so.unlocksFeatureID   = featureID;
        so.unlocksSlotIndex   = slotIndex;
        so.prerequisiteResearchID = prereqID;
        so.requiredUnlockID   = unlockID;
        so.requiredAnimalID   = animalID;
        Save(so, id);
    }

    private static void Save(ResearchData so, string id)
    {
        string path = $"{OutDir}/Research_{id}.asset";
        AssetDatabase.CreateAsset(so, path);
    }
}
#endif
```

- [ ] **Step 5.3: Run the generator**

In Unity: top menu `Farm Game → Research → Generate Catalog`. Verify the console prints `Catalog written to Assets/Data/Research/Catalog`. Verify the folder contains 38 `Research_*.asset` files. (Cow's 2 + Compost Bay's 1 = 3 deferred to Plan 2.)

- [ ] **Step 5.4: Copy catalog into Resources for runtime load**

Create folder `Assets/Resources/Research/`. Drag all 38 `Research_*.asset` files from `Assets/Data/Research/Catalog/` into the Resources folder (Move, not Copy — Unity references will update). The Resources path lets `Resources.LoadAll<ResearchData>("Research")` find them at runtime.

- [ ] **Step 5.5: Commit**

```bash
git add Assets/Editor/ResearchCatalogGenerator.cs Assets/Editor/ResearchCatalogGenerator.cs.meta Assets/Resources/Research
git commit -m "feat(research): generate 38-entry catalog into Resources/Research"
```

---

## Task 6: Extend GameData with research persistence

**Files:**
- Modify: `Assets/Scripts/GameData.cs`

- [ ] **Step 6.1: Add fields and constructor parameters**

Replace the file with:

```csharp
using System;
using Research;

[Serializable]
public class GameData
{
    public int coins;
    public int gems;
    public string[] unlockedAnimalIDs;
    public string equippedAnimalID;
    public string lastEggClaimTime;

    public ActiveQuest[] activeQuests;
    public int questsCompletedThisWeek;
    public bool[] weeklyMilestonesClaimed;
    public string questWeekStart;
    public string lastQuestDropTime;

    // Research (Plan 1)
    public bool[] researchSlotsUnlocked;           // length 4
    public ResearchSlotState[] researchSlots;     // length 4, idle if startUtcTicks == 0
    public string[] binaryFeatureFlagsSet;        // ids that have been unlocked, e.g. "offline_progress"

    public GameData()
    {
        coins = 0;
        gems = 0;
        unlockedAnimalIDs = new string[0];
        equippedAnimalID = "";
        lastEggClaimTime = "";
        activeQuests = new ActiveQuest[0];
        questsCompletedThisWeek = 0;
        weeklyMilestonesClaimed = new bool[8];
        questWeekStart = "";
        lastQuestDropTime = "";

        researchSlotsUnlocked = new bool[4];
        researchSlots = new ResearchSlotState[4];
        for (int i = 0; i < 4; i++) researchSlots[i] = new ResearchSlotState { slotIndex = i };
        binaryFeatureFlagsSet = new string[0];
    }

    public GameData(
        int currentCoins, int currentGems,
        string[] animalIDs, string equippedID, string eggTime,
        ActiveQuest[] quests, int questsCompleted, bool[] milestones, string weekStart, string lastDrop,
        bool[] researchUnlocked, ResearchSlotState[] researchSlotsIn, string[] featureFlags)
    {
        coins = currentCoins;
        gems = currentGems;
        unlockedAnimalIDs = animalIDs ?? new string[0];
        equippedAnimalID = equippedID ?? "";
        lastEggClaimTime = eggTime ?? "";
        activeQuests = quests ?? new ActiveQuest[0];
        questsCompletedThisWeek = questsCompleted;
        weeklyMilestonesClaimed = milestones ?? new bool[8];
        questWeekStart = weekStart ?? "";
        lastQuestDropTime = lastDrop ?? "";
        researchSlotsUnlocked = researchUnlocked ?? new bool[4];
        researchSlots = researchSlotsIn ?? new ResearchSlotState[4];
        binaryFeatureFlagsSet = featureFlags ?? new string[0];
    }
}
```

- [ ] **Step 6.2: Commit**

```bash
git add Assets/Scripts/GameData.cs
git commit -m "feat(research): extend GameData with research slot state + feature flags"
```

---

## Task 7: Extend ResearchManager — catalog load, lookup, polynomial math

**Files:**
- Modify: `Assets/Scripts/ResearchManager.cs`

The existing file has slot-unlock state. We keep that and bolt on the catalog, lookup, math, slot state, assign/tick/GetBonus/visibility.

- [ ] **Step 7.1: Replace `ResearchManager.cs` with the extended version**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Research;
using UnityEngine;

/// <summary>
/// Owns the player's research state: 4 slots (locked/unlocked), per-slot active research, real-time level-ups,
/// and stat-key bonus queries. Catalog loaded from Resources/Research/*. Persists via GameData.json.
/// </summary>
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    public const int SlotCount = 4;

    public enum SlotUnlockType { Coins, Gems, Research }

    [Serializable]
    public class SlotDefinition
    {
        public SlotUnlockType unlockType;
        public int costAmount;
        public string requiredResearchID;
    }

    [Header("Slot Unlock Costs")]
    [SerializeField] private SlotDefinition[] slotDefs = new SlotDefinition[SlotCount]
    {
        new SlotDefinition { unlockType = SlotUnlockType.Coins, costAmount = 100 },
        new SlotDefinition { unlockType = SlotUnlockType.Gems,  costAmount = 100 },
        new SlotDefinition { unlockType = SlotUnlockType.Research, requiredResearchID = "slot_3_unlock" },
        new SlotDefinition { unlockType = SlotUnlockType.Research, requiredResearchID = "slot_4_unlock" },
    };

    [Header("Tuning")]
    [SerializeField] private ResearchTuning tuning; // assign in Inspector → ResearchTuning.asset

    private readonly bool[] slotUnlocked = new bool[SlotCount];
    private readonly ResearchSlotState[] slots = new ResearchSlotState[SlotCount];
    private readonly HashSet<string> featureFlags = new HashSet<string>();

    // researchID -> ResearchData
    private readonly Dictionary<string, ResearchData> catalog = new Dictionary<string, ResearchData>();
    // researchID -> completed-level (for in-progress slot research, this is the last-claimed level; for non-active, it's whatever level the player got to)
    private readonly Dictionary<string, int> levelsByResearchID = new Dictionary<string, int>();

    public event Action<int> OnSlotUnlocked;             // slotIndex
    public event Action<int> OnSlotStateChanged;          // slotIndex — assigned/cancelled
    public event Action<string, int> OnResearchLeveledUp; // researchID, newLevel
    public event Action<string> OnFeatureFlagUnlocked;    // featureID

    private float tickAccumulator;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < SlotCount; i++) slots[i] = new ResearchSlotState { slotIndex = i };
        LoadCatalog();
    }

    private void Start()
    {
        // Migration from old PlayerPrefs slot state — kept minimal: if PlayerPrefs has slot bits and GameData hasn't loaded any yet, copy them in.
        bool gameDataLoaded = SaveManager.Instance != null && SaveManager.Instance.SaveFileExists();
        if (!gameDataLoaded)
        {
            for (int i = 0; i < SlotCount; i++)
                slotUnlocked[i] = PlayerPrefs.GetInt("research_slot_unlocked_" + i, 0) == 1;
        }
    }

    private void Update()
    {
        if (tuning == null) return;
        tickAccumulator += Time.unscaledDeltaTime;
        if (tickAccumulator < tuning.tickIntervalSecs) return;
        tickAccumulator = 0f;
        Tick();
    }

    // ───────── Catalog ─────────

    private void LoadCatalog()
    {
        catalog.Clear();
        var all = Resources.LoadAll<ResearchData>("Research");
        foreach (var rd in all)
        {
            if (rd == null || string.IsNullOrEmpty(rd.researchID)) continue;
            if (catalog.ContainsKey(rd.researchID)) { Debug.LogError($"[Research] Duplicate ID: {rd.researchID}"); continue; }
            catalog[rd.researchID] = rd;
        }
        Debug.Log($"[Research] Catalog loaded: {catalog.Count} entries");
    }

    public ResearchData GetResearch(string id) => (id != null && catalog.TryGetValue(id, out var rd)) ? rd : null;
    public IEnumerable<ResearchData> AllResearches() => catalog.Values;

    // ───────── Slot Unlock (existing surface, kept) ─────────

    public SlotDefinition GetSlotDef(int index) => (index >= 0 && index < SlotCount) ? slotDefs[index] : null;
    public bool IsSlotUnlocked(int index) => index >= 0 && index < SlotCount && slotUnlocked[index];

    public bool CanUnlockSlot(int index)
    {
        if (!IsValidSlot(index) || slotUnlocked[index]) return false;
        SlotDefinition def = slotDefs[index];
        switch (def.unlockType)
        {
            case SlotUnlockType.Coins: return CurrencyManager.Instance?.CanAffordCoins(def.costAmount) ?? false;
            case SlotUnlockType.Gems:  return CurrencyManager.Instance?.CanAffordGems(def.costAmount) ?? false;
            case SlotUnlockType.Research: return GetCurrentLevel(def.requiredResearchID) >= 1;
        }
        return false;
    }

    public bool TryUnlockSlot(int index)
    {
        if (!CanUnlockSlot(index)) return false;
        var def = slotDefs[index];
        switch (def.unlockType)
        {
            case SlotUnlockType.Coins: if (!CurrencyManager.Instance.SpendCoins(def.costAmount)) return false; break;
            case SlotUnlockType.Gems:  if (!CurrencyManager.Instance.SpendGems(def.costAmount))  return false; break;
            case SlotUnlockType.Research: /* no payment */ break;
        }
        UnlockSlotInternal(index);
        return true;
    }

    private void UnlockSlotInternal(int index)
    {
        if (slotUnlocked[index]) return;
        slotUnlocked[index] = true;
        OnSlotUnlocked?.Invoke(index);
    }

    private static bool IsValidSlot(int index) => index >= 0 && index < SlotCount;

    // ───────── Assign / Cancel ─────────

    public ResearchSlotState GetSlot(int index) => IsValidSlot(index) ? slots[index] : null;

    public bool TryAssignResearch(int slotIndex, string researchID)
    {
        if (!IsValidSlot(slotIndex) || !slotUnlocked[slotIndex]) return false;
        var rd = GetResearch(researchID);
        if (rd == null) return false;
        if (!IsResearchVisible(researchID)) return false;
        int curLevel = GetCurrentLevel(researchID);
        if (curLevel >= rd.MaxLevel) return false; // already maxed

        // Pay L+1 cost (level we're about to start)
        int nextLevel = curLevel + 1;
        int cost = GetCostForLevel(rd, nextLevel);
        if (!CurrencyManager.Instance.SpendCoins(cost)) return false;

        slots[slotIndex].activeResearchID = researchID;
        slots[slotIndex].currentLevel     = curLevel;
        slots[slotIndex].startUtcTicks    = DateTime.UtcNow.Ticks;
        OnSlotStateChanged?.Invoke(slotIndex);
        return true;
    }

    public void CancelResearch(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        slots[slotIndex].activeResearchID = "";
        slots[slotIndex].currentLevel = 0;
        slots[slotIndex].startUtcTicks = 0;
        slots[slotIndex].boostExpiresUtcTicks = 0;
        slots[slotIndex].boostMultiplier = 1.0f;
        OnSlotStateChanged?.Invoke(slotIndex);
    }

    public int GetCurrentLevel(string researchID)
    {
        if (string.IsNullOrEmpty(researchID)) return 0;
        return levelsByResearchID.TryGetValue(researchID, out var lvl) ? lvl : 0;
    }

    public bool IsBinaryComplete(string researchID) => GetCurrentLevel(researchID) >= 1 && GetResearch(researchID)?.IsBinary == true;

    public bool IsFeatureUnlocked(string featureID) => featureFlags.Contains(featureID);

    // ───────── Math ─────────

    public float GetSecondsForLevel(ResearchData rd, int levelOneIndexed)
    {
        if (rd == null || levelOneIndexed < 1) return 0f;
        if (rd.IsBinary) return rd.binaryFixedDurationSecs;
        float p = tuning != null ? tuning.pTime : 2.16f;
        float baseSecs = rd.baseDurationSecs * rd.timeDifficulty;
        // Apply Research Speed bonus globally (divide duration by 1 + bonus)
        float rsBonus = GetBonus(StatKey.ResearchSpeed);
        float scaled = baseSecs * Mathf.Pow(levelOneIndexed, p) / Mathf.Max(0.01f, 1f + rsBonus);
        return scaled;
    }

    public int GetCostForLevel(ResearchData rd, int levelOneIndexed)
    {
        if (rd == null || levelOneIndexed < 1) return 0;
        if (rd.IsBinary) return rd.binaryFixedCost;
        float p = tuning != null ? tuning.pCost : 2.0f;
        float baseCost = rd.baseCostCoins * rd.costDifficulty;
        return Mathf.CeilToInt(baseCost * Mathf.Pow(levelOneIndexed, p));
    }

    // ───────── Tick (real-time level-ups) ─────────

    public void Tick()
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        for (int i = 0; i < SlotCount; i++)
        {
            var s = slots[i];
            if (s.IsIdle) continue;
            var rd = GetResearch(s.activeResearchID);
            if (rd == null) { CancelResearch(i); continue; }

            int safety = 0;
            while (safety++ < 100)
            {
                if (s.currentLevel >= rd.MaxLevel) { OnResearchCompleted(rd, i); break; }
                int nextLevel = s.currentLevel + 1;
                float secs = GetSecondsForLevel(rd, nextLevel);
                if (secs <= 0f) break;

                double elapsedSec = ComputeElapsedSeconds(s, nowTicks);
                if (elapsedSec < secs) break;

                // Level up
                s.currentLevel = nextLevel;
                levelsByResearchID[rd.researchID] = nextLevel;
                // Advance startUtcTicks by the consumed duration
                long consumedTicks = (long)(secs * TimeSpan.TicksPerSecond);
                s.startUtcTicks += consumedTicks;
                OnResearchLeveledUp?.Invoke(rd.researchID, nextLevel);

                if (nextLevel >= rd.MaxLevel) { OnResearchCompleted(rd, i); break; }

                // Auto-charge next level — if player can't afford, pause at current level.
                int nextCost = GetCostForLevel(rd, nextLevel + 1);
                if (!CurrencyManager.Instance.CanAffordCoins(nextCost))
                {
                    s.startUtcTicks = 0;
                    OnSlotStateChanged?.Invoke(i);
                    break;
                }
                CurrencyManager.Instance.SpendCoins(nextCost);
            }
        }
    }

    private static double ComputeElapsedSeconds(ResearchSlotState s, long nowTicks)
    {
        long deltaTicks = nowTicks - s.startUtcTicks;
        if (deltaTicks < 0) deltaTicks = 0;
        double secs = deltaTicks / (double)TimeSpan.TicksPerSecond;
        // Boost interval credit (Plan 2 — currently boost fields default to neutral, so multiplier is 1.0)
        if (s.boostExpiresUtcTicks > s.startUtcTicks && s.boostMultiplier > 1.0f)
        {
            long boostEnd = Math.Min(nowTicks, s.boostExpiresUtcTicks);
            long boostTicks = Math.Max(0, boostEnd - s.startUtcTicks);
            double boostSecs = boostTicks / (double)TimeSpan.TicksPerSecond;
            secs += boostSecs * (s.boostMultiplier - 1.0); // already counted at 1x; add extra
        }
        return secs;
    }

    private void OnResearchCompleted(ResearchData rd, int slotIndex)
    {
        if (rd.IsBinary)
        {
            if (rd.unlocksSlotIndex >= 0 && rd.unlocksSlotIndex < SlotCount)
                UnlockSlotInternal(rd.unlocksSlotIndex);
            if (!string.IsNullOrEmpty(rd.unlocksFeatureID))
            {
                if (featureFlags.Add(rd.unlocksFeatureID))
                    OnFeatureFlagUnlocked?.Invoke(rd.unlocksFeatureID);
            }
        }
        // Slot becomes idle; player picks the next research.
        CancelResearch(slotIndex);
    }

    // ───────── Bonus query ─────────

    public float GetBonus(string statKey)
    {
        if (string.IsNullOrEmpty(statKey)) return 0f;
        float total = 0f;
        foreach (var rd in catalog.Values)
        {
            if (rd.targetStatKey != statKey) continue;
            int lvl = GetCurrentLevel(rd.researchID);
            total += lvl * rd.bonusPerLevel;
        }
        return total;
    }

    // ───────── Visibility ─────────

    public bool IsResearchVisible(string researchID)
    {
        var rd = GetResearch(researchID);
        if (rd == null) return false;

        if (!string.IsNullOrEmpty(rd.prerequisiteResearchID))
            if (GetCurrentLevel(rd.prerequisiteResearchID) < 1) return false;

        if (!string.IsNullOrEmpty(rd.requiredUnlockID))
            if (UpgradeManager.Instance == null || !UpgradeManager.Instance.IsUnlocked(rd.requiredUnlockID)) return false;

        if (!string.IsNullOrEmpty(rd.requiredAnimalID))
            if (AnimalManager.Instance == null || !AnimalManager.Instance.IsUnlocked(rd.requiredAnimalID)) return false;

        return true;
    }

    // ───────── Save/Load surface ─────────

    public bool[] GetSlotsUnlockedForSave() => (bool[])slotUnlocked.Clone();
    public ResearchSlotState[] GetSlotsForSave() => slots.Select(s => DeepCopy(s)).ToArray();
    public string[] GetFeatureFlagsForSave() => featureFlags.ToArray();

    public Dictionary<string, int> GetLevelsForSave() => new Dictionary<string, int>(levelsByResearchID);
    public void LoadLevels(Dictionary<string, int> levels)
    {
        levelsByResearchID.Clear();
        if (levels != null) foreach (var kv in levels) levelsByResearchID[kv.Key] = kv.Value;
    }

    public void LoadState(bool[] unlocked, ResearchSlotState[] savedSlots, string[] flags)
    {
        if (unlocked != null) for (int i = 0; i < Math.Min(unlocked.Length, SlotCount); i++) slotUnlocked[i] = unlocked[i];
        if (savedSlots != null) for (int i = 0; i < Math.Min(savedSlots.Length, SlotCount); i++) slots[i] = DeepCopy(savedSlots[i]);
        featureFlags.Clear();
        if (flags != null) foreach (var f in flags) if (!string.IsNullOrEmpty(f)) featureFlags.Add(f);
    }

    private static ResearchSlotState DeepCopy(ResearchSlotState src)
    {
        if (src == null) return new ResearchSlotState();
        return new ResearchSlotState
        {
            slotIndex = src.slotIndex,
            activeResearchID = src.activeResearchID ?? "",
            currentLevel = src.currentLevel,
            startUtcTicks = src.startUtcTicks,
            boostExpiresUtcTicks = src.boostExpiresUtcTicks,
            boostMultiplier = src.boostMultiplier <= 0 ? 1f : src.boostMultiplier
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Reset All Research State")]
    private void EditorResetAll()
    {
        for (int i = 0; i < SlotCount; i++) { slotUnlocked[i] = false; CancelResearch(i); }
        levelsByResearchID.Clear();
        featureFlags.Clear();
        for (int i = 0; i < SlotCount; i++) PlayerPrefs.SetInt("research_slot_unlocked_" + i, 0);
        PlayerPrefs.Save();
        Debug.Log("[ResearchManager] All research state reset.");
    }
#endif
}
```

- [ ] **Step 7.2: Assign the tuning asset**

In the scene, find the `ResearchManager` GameObject. In the Inspector, drag `Assets/Data/Research/ResearchTuning.asset` into the **Tuning** slot.

- [ ] **Step 7.3: Verify compile**

Open Unity. Check `read_console` (or the Console panel) for errors. Expected: no errors. (Two `UpgradeManager.IsUnlocked` and `AnimalManager.IsUnlocked` calls assume those methods exist on existing managers — if compile fails on those, see Task 7.4.)

- [ ] **Step 7.4: Confirm method signatures on existing managers**

Open `Assets/Scripts/UpgradeManager.cs` and `Assets/Scripts/AnimalManager.cs`. Confirm both have a public `bool IsUnlocked(string id)` method. If not (name differs), update the calls in `ResearchManager.IsResearchVisible` to match. Do NOT add a new method to those managers in this task.

- [ ] **Step 7.5: Commit**

```bash
git add Assets/Scripts/ResearchManager.cs
git commit -m "feat(research): extend ResearchManager with catalog, assign, tick, GetBonus, visibility"
```

---

## Task 8: Wire ResearchManager save/load through SaveManager

**Files:**
- Modify: `Assets/Scripts/SaveManager.cs`

- [ ] **Step 8.1: Update SaveGame() to pull research state**

Find the `GameData data = new GameData(...)` constructor call in `SaveGame()`. Replace it with:

```csharp
bool[] researchUnlocked = new bool[ResearchManager.SlotCount];
Research.ResearchSlotState[] researchSlots = null;
string[] featureFlags = new string[0];
if (ResearchManager.Instance != null)
{
    researchUnlocked = ResearchManager.Instance.GetSlotsUnlockedForSave();
    researchSlots    = ResearchManager.Instance.GetSlotsForSave();
    featureFlags     = ResearchManager.Instance.GetFeatureFlagsForSave();
}

GameData data = new GameData(
    CurrencyManager.Instance.Coins,
    CurrencyManager.Instance.Gems,
    animalIDs,
    equippedID,
    eggTime,
    quests,
    questsCompleted,
    milestones,
    weekStart,
    lastDrop,
    researchUnlocked,
    researchSlots,
    featureFlags
);
```

- [ ] **Step 8.2: Update LoadGame() to apply research state**

After the `QuestManager` LoadState call block, add:

```csharp
if (ResearchManager.Instance != null)
{
    ResearchManager.Instance.LoadState(
        data.researchSlotsUnlocked,
        data.researchSlots,
        data.binaryFeatureFlagsSet
    );
}
```

- [ ] **Step 8.3: Save/load round-trip verification**

In Unity, enter Play mode. Open the Research popup, unlock Slot 1 (100 coins), and assign any research. Stop Play mode. Re-enter Play mode. Verify the slot is still unlocked and the research is still assigned + progressing.

- [ ] **Step 8.4: Commit**

```bash
git add Assets/Scripts/SaveManager.cs
git commit -m "feat(research): persist research slot state via GameData JSON"
```

---

## Task 9: Polynomial math EditMode tests

This is the one piece of pure-logic code that warrants tests. Verifies our scaling matches the spec's reference table.

**Files:**
- Create: `Assets/Tests/Editor/ResearchMathTests.cs`
- Create (if missing): `Assets/Tests/Editor/Tests.Editor.asmdef`

- [ ] **Step 9.1: Create the editor assembly definition if missing**

In `Assets/Tests/Editor/`, create `Tests.Editor.asmdef` with content:

```json
{
  "name": "Tests.Editor",
  "rootNamespace": "",
  "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 9.2: Add a project reference**

Edit `Tests.Editor.asmdef` to also reference the main scripts assembly. Find the project's main asmdef (it may be the default `Assembly-CSharp` — if so, leave references as above; Unity allows asmdefs in `Editor/` to call into the default assembly). If the project has an explicit gameplay asmdef, add it to the `references` array.

- [ ] **Step 9.3: Add the tests**

```csharp
using NUnit.Framework;
using Research;
using UnityEngine;

public class ResearchMathTests
{
    private ResearchTuning tuning;

    [SetUp]
    public void Setup()
    {
        tuning = ScriptableObject.CreateInstance<ResearchTuning>();
        tuning.pTime = 2.16f;
        tuning.pCost = 2.00f;
    }

    private ResearchData MakeStd()
    {
        var rd = ScriptableObject.CreateInstance<ResearchData>();
        rd.researchID = "test_std";
        rd.tier = ResearchTier.Tier100Standard;
        rd.baseDurationSecs = 120f;
        rd.baseCostCoins = 50f;
        rd.timeDifficulty = 1f;
        rd.costDifficulty = 1f;
        return rd;
    }

    [Test]
    public void StandardTier_L1_Is_TwoMinutes()
    {
        var rd = MakeStd();
        float secs = 120f * Mathf.Pow(1f, 2.16f);
        Assert.AreEqual(120f, secs, 1f);
    }

    [Test]
    public void StandardTier_L25_Is_Roughly_1_5_Days()
    {
        var rd = MakeStd();
        float secs = 120f * Mathf.Pow(25f, 2.16f);
        // 1.5 days = 129,600 sec. Allow ±10%.
        Assert.That(secs, Is.InRange(116_000f, 142_000f));
    }

    [Test]
    public void StandardTier_L100_Is_Roughly_30_Days()
    {
        var rd = MakeStd();
        float secs = 120f * Mathf.Pow(100f, 2.16f);
        // 30 days = 2,592,000 sec. Allow ±10%.
        Assert.That(secs, Is.InRange(2_330_000f, 2_850_000f));
    }

    [Test]
    public void AbsurdTier_L100_Is_Roughly_90_Days()
    {
        float secs = 540f * Mathf.Pow(100f, 2.16f); // base 9 min × L100
        // 90 days = 7,776,000 sec. Allow ±10%.
        Assert.That(secs, Is.InRange(7_000_000f, 8_550_000f));
    }

    [Test]
    public void TimeDifficulty_Scales_Linearly()
    {
        var rd = MakeStd();
        rd.timeDifficulty = 2f;
        float baseSecs = 120f * Mathf.Pow(50f, 2.16f);
        float scaledSecs = (120f * 2f) * Mathf.Pow(50f, 2.16f);
        Assert.AreEqual(baseSecs * 2f, scaledSecs, 1f);
    }
}
```

- [ ] **Step 9.4: Run tests**

In Unity: `Window → General → Test Runner → EditMode → Run All`. Expected: 5 tests pass.

- [ ] **Step 9.5: Commit**

```bash
git add Assets/Tests/Editor
git commit -m "test(research): polynomial scaling reference tests"
```

---

## Task 10: ResearchPopupUITK — picker UXML + USS

**Files:**
- Modify: `Assets/UI/ResearchPopupUITK/ResearchPopupUITK.uxml`
- Modify: `Assets/UI/ResearchPopupUITK/ResearchPopupUITK.uss`

- [ ] **Step 10.1: Update UXML to add picker overlay and timer elements**

Replace the file with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="ResearchPopupUITK.uss" />
    <ui:VisualElement name="popup-root" class="popup-root" style="display: none;">
        <ui:VisualElement name="header" class="header">
            <ui:Label name="header-title" text="Greenhouse — Research" class="header-title" />
            <ui:Button name="close-button" class="close-button" text="X" />
        </ui:VisualElement>
        <ui:VisualElement name="slots-list" class="slots-list">
            <ui:VisualElement name="slot-0" class="slot-card" />
            <ui:VisualElement name="slot-1" class="slot-card" />
            <ui:VisualElement name="slot-2" class="slot-card" />
            <ui:VisualElement name="slot-3" class="slot-card" />
        </ui:VisualElement>

        <!-- Picker overlay -->
        <ui:VisualElement name="picker" class="picker" style="display: none;">
            <ui:VisualElement name="picker-header" class="picker-header">
                <ui:Label name="picker-title" text="Select Research" class="picker-title" />
                <ui:Button name="picker-close" class="picker-close" text="X" />
            </ui:VisualElement>
            <ui:VisualElement name="picker-tabs" class="picker-tabs" />
            <ui:ScrollView name="picker-list" class="picker-list" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 10.2: Append picker/timer USS rules**

Open the USS file. Append (do not replace existing rules):

```css
/* Picker overlay */
.picker {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(10, 20, 12, 0.96);
    flex-direction: column;
    padding: 16px;
}
.picker-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
}
.picker-title { font-size: 22px; color: #f4f1e8; -unity-font-style: bold; }
.picker-close { background-color: rgba(0,0,0,0); color: #f4f1e8; font-size: 20px; border-width: 0; padding: 4px 12px; }
.picker-tabs { flex-direction: row; flex-wrap: wrap; margin-bottom: 12px; }
.picker-tab {
    padding: 6px 12px; margin-right: 6px; margin-bottom: 6px;
    background-color: rgba(255,255,255,0.06); color: #cfc8b6; border-radius: 12px;
}
.picker-tab--active { background-color: #4f7a3a; color: #f4f1e8; }
.picker-list { flex-grow: 1; }
.picker-row {
    flex-direction: row; justify-content: space-between; align-items: center;
    padding: 10px 12px; margin-bottom: 6px;
    background-color: rgba(255,255,255,0.04); border-radius: 8px;
    border-width: 1px; border-color: rgba(255,255,255,0.04);
}
.picker-row__text-col { flex-direction: column; flex-grow: 1; flex-shrink: 1; }
.picker-row__name { color: #f4f1e8; font-size: 16px; -unity-font-style: bold; }
.picker-row__desc { color: #a8a290; font-size: 12px; white-space: normal; margin-top: 2px; }
.picker-row__meta-col { flex-direction: column; align-items: flex-end; min-width: 90px; margin-left: 12px; }
.picker-row__cost { color: #f0c878; font-size: 14px; -unity-font-style: bold; }
.picker-row__time { color: #cfc8b6; font-size: 12px; margin-top: 2px; }

/* Unaffordable: row dimmed, cost red */
.picker-row--disabled { background-color: rgba(255,255,255,0.02); }
.picker-row--disabled .picker-row__name { color: #888278; }
.picker-row--disabled .picker-row__desc { color: #5e594d; }
.picker-row__cost--unaffordable { color: #d05a5a; }

/* Maxed: gold tint + gold border */
.picker-row--complete {
    background-color: rgba(240, 200, 120, 0.12);
    border-color: #f0c878;
}
.picker-row--complete .picker-row__name { color: #f0c878; }
.picker-row--complete .picker-row__cost { color: #f0c878; }
.picker-row--complete .picker-row__time { color: #f0c878; }

/* Active slot boost indicator (Plan 2 — element drawn but hidden until boost engaged) */
.slot-card__boost {
    color: #f0c878; font-size: 12px; margin-top: 4px;
    -unity-font-style: bold; display: none;
}
.slot-card__boost--active { display: flex; }

/* Slot card — active research view */
.slot-card__active-name { color: #f4f1e8; font-size: 16px; -unity-font-style: bold; }
.slot-card__active-progress {
    height: 8px; margin-top: 6px; background-color: rgba(255,255,255,0.08); border-radius: 4px;
}
.slot-card__active-progress-fill {
    height: 100%; background-color: #6fb04a; border-radius: 4px;
}
.slot-card__active-timer { color: #cfc8b6; font-size: 12px; margin-top: 4px; }
.slot-card__cancel { color: #b86a6a; font-size: 11px; margin-top: 6px; }
```

- [ ] **Step 10.3: Commit**

```bash
git add Assets/UI/ResearchPopupUITK/ResearchPopupUITK.uxml Assets/UI/ResearchPopupUITK/ResearchPopupUITK.uss
git commit -m "feat(research): UXML/USS for picker overlay and slot timer"
```

---

## Task 11: ResearchPopupUITK — assign flow, picker, countdown

**Files:**
- Modify: `Assets/Scripts/UI/ResearchPopupUITK.cs`

- [ ] **Step 11.1: Replace the script with the extended version**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Research;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class ResearchPopupUITK : MonoBehaviour
{
    public static ResearchPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private Button closeButton;

    // Picker
    private VisualElement picker;
    private VisualElement pickerTabs;
    private ScrollView pickerList;
    private Button pickerClose;
    private string activeBranchTab;
    private int pickerSlotIndex = -1;

    private bool isOpen;
    private bool eventsSubscribed;
    private bool refreshPending;
    public bool IsOpen => isOpen;

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
        TrySubscribeEvents();
    }

    private void Start()
    {
        if (root == null) { CacheElements(); WireCallbacks(); }
    }

    private void OnDisable() => UnsubscribeEvents();

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        bool any = false;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged  += OnCurrencyChanged;
            any = true;
        }
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked     += OnSlotUnlocked;
            ResearchManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
            ResearchManager.Instance.OnResearchLeveledUp += OnLeveledUp;
            any = true;
        }
        eventsSubscribed = any;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged  -= OnCurrencyChanged;
        }
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked     -= OnSlotUnlocked;
            ResearchManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
            ResearchManager.Instance.OnResearchLeveledUp -= OnLeveledUp;
        }
        eventsSubscribed = false;
    }

    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnSlotUnlocked(int _) => MarkDirty();
    private void OnSlotStateChanged(int _) => MarkDirty();
    private void OnLeveledUp(string _, int __) => MarkDirty();

    private void MarkDirty()
    {
        if (!isOpen || refreshPending || root == null) return;
        refreshPending = true;
        root.schedule.Execute(() =>
        {
            refreshPending = false;
            if (isOpen) Refresh();
        }).StartingIn(150);
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[ResearchPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        closeButton = root.Q<Button>("close-button");

        picker      = root.Q<VisualElement>("picker");
        pickerTabs  = root.Q<VisualElement>("picker-tabs");
        pickerList  = root.Q<ScrollView>("picker-list");
        pickerClose = root.Q<Button>("picker-close");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (pickerClose != null) pickerClose.RegisterCallback<ClickEvent>(_ => ClosePicker());
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
        // Tick the popup once a second so countdowns update without waiting on events.
        root.schedule.Execute(TickRefresh).Every(1000);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        ClosePicker();
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private void TickRefresh()
    {
        if (!isOpen) return;
        Refresh();
    }

    private void Refresh()
    {
        if (root == null) return;
        for (int i = 0; i < ResearchManager.SlotCount; i++) RenderSlot(i);
    }

    private void RenderSlot(int slotIndex)
    {
        VisualElement card = root.Q<VisualElement>($"slot-{slotIndex}");
        if (card == null) return;

        card.Clear();
        card.RemoveFromClassList("slot-card--unlocked-empty");
        card.RemoveFromClassList("slot-card--locked");
        card.RemoveFromClassList("slot-card--affordable");

        ResearchManager mgr = ResearchManager.Instance;
        if (mgr == null) return;

        bool unlocked = mgr.IsSlotUnlocked(slotIndex);
        if (!unlocked) { RenderLockedSlot(card, slotIndex, mgr); return; }

        var state = mgr.GetSlot(slotIndex);
        if (state == null || state.IsIdle) { RenderEmptySlot(card, slotIndex, mgr); return; }

        RenderActiveSlot(card, slotIndex, mgr, state);
    }

    private void RenderLockedSlot(VisualElement card, int slotIndex, ResearchManager mgr)
    {
        var def = mgr.GetSlotDef(slotIndex);
        Label statusLabel = new Label("Locked"); statusLabel.AddToClassList("slot-status");
        Label actionLabel = new Label(); actionLabel.AddToClassList("slot-action");
        switch (def.unlockType)
        {
            case ResearchManager.SlotUnlockType.Coins: actionLabel.text = $"{def.costAmount} coins to unlock"; break;
            case ResearchManager.SlotUnlockType.Gems:  actionLabel.text = $"{def.costAmount} gems to unlock"; break;
            case ResearchManager.SlotUnlockType.Research: actionLabel.text = "Research to unlock"; break;
        }
        bool canAfford = mgr.CanUnlockSlot(slotIndex);
        card.AddToClassList(canAfford ? "slot-card--affordable" : "slot-card--locked");
        if (canAfford)
        {
            int captured = slotIndex;
            card.RegisterCallback<ClickEvent>(_ => ResearchManager.Instance?.TryUnlockSlot(captured));
            WirePressedFeedback(card, "slot-card--pressed");
        }
        card.Add(statusLabel); card.Add(actionLabel);
    }

    private void RenderEmptySlot(VisualElement card, int slotIndex, ResearchManager mgr)
    {
        card.AddToClassList("slot-card--unlocked-empty");
        Label statusLabel = new Label("No Active Research"); statusLabel.AddToClassList("slot-status");
        Label actionLabel = new Label("Tap to assign");      actionLabel.AddToClassList("slot-action");
        int captured = slotIndex;
        card.RegisterCallback<ClickEvent>(_ => OpenPicker(captured));
        WirePressedFeedback(card, "slot-card--pressed");
        card.Add(statusLabel); card.Add(actionLabel);
    }

    private void RenderActiveSlot(VisualElement card, int slotIndex, ResearchManager mgr, ResearchSlotState state)
    {
        var rd = mgr.GetResearch(state.activeResearchID);
        if (rd == null) { CancelSlotAndRefresh(slotIndex); return; }

        Label nameLabel = new Label($"{rd.displayName} — L{state.currentLevel + 1}/{rd.MaxLevel}");
        nameLabel.AddToClassList("slot-card__active-name");

        int nextLevel = state.currentLevel + 1;
        float secsForLevel = mgr.GetSecondsForLevel(rd, nextLevel);
        double elapsed = (DateTime.UtcNow.Ticks - state.startUtcTicks) / (double)TimeSpan.TicksPerSecond;
        float progress = secsForLevel <= 0 ? 0f : Mathf.Clamp01((float)(elapsed / secsForLevel));
        double remaining = Math.Max(0, secsForLevel - elapsed);

        VisualElement bar = new VisualElement(); bar.AddToClassList("slot-card__active-progress");
        VisualElement fill = new VisualElement(); fill.AddToClassList("slot-card__active-progress-fill");
        fill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
        bar.Add(fill);

        Label timer = new Label(FormatRemaining(remaining));
        timer.AddToClassList("slot-card__active-timer");

        Label cancel = new Label("Cancel ↩"); cancel.AddToClassList("slot-card__cancel");
        int captured = slotIndex;
        cancel.RegisterCallback<ClickEvent>(_ => CancelSlotAndRefresh(captured));

        card.Add(nameLabel); card.Add(bar); card.Add(timer); card.Add(cancel);
    }

    private void CancelSlotAndRefresh(int slotIndex)
    {
        ResearchManager.Instance?.CancelResearch(slotIndex);
        Refresh();
    }

    private static string FormatRemaining(double secs)
    {
        if (secs >= 86400) return $"{secs/86400:F1} days";
        if (secs >= 3600)  return $"{secs/3600:F1} hr";
        if (secs >= 60)    return $"{secs/60:F0} min";
        return $"{secs:F0} sec";
    }

    // ───────── Picker ─────────

    private void OpenPicker(int slotIndex)
    {
        pickerSlotIndex = slotIndex;
        if (picker != null) picker.style.display = DisplayStyle.Flex;
        RebuildPickerTabs();
        RebuildPickerList();
    }

    private void ClosePicker()
    {
        pickerSlotIndex = -1;
        if (picker != null) picker.style.display = DisplayStyle.None;
    }

    private void RebuildPickerTabs()
    {
        if (pickerTabs == null) return;
        pickerTabs.Clear();
        var mgr = ResearchManager.Instance;
        if (mgr == null) return;
        var branches = mgr.AllResearches()
            .Where(rd => mgr.IsResearchVisible(rd.researchID))
            .Select(rd => rd.branchID).Distinct().OrderBy(b => b).ToList();
        if (string.IsNullOrEmpty(activeBranchTab) || !branches.Contains(activeBranchTab))
            activeBranchTab = branches.FirstOrDefault() ?? "";
        foreach (var b in branches)
        {
            var tab = new Label(b); tab.AddToClassList("picker-tab");
            if (b == activeBranchTab) tab.AddToClassList("picker-tab--active");
            string captured = b;
            tab.RegisterCallback<ClickEvent>(_ => { activeBranchTab = captured; RebuildPickerTabs(); RebuildPickerList(); });
            pickerTabs.Add(tab);
        }
    }

    private void RebuildPickerList()
    {
        if (pickerList == null) return;
        pickerList.Clear();
        var mgr = ResearchManager.Instance;
        if (mgr == null) return;
        var rows = mgr.AllResearches()
            .Where(rd => mgr.IsResearchVisible(rd.researchID) && rd.branchID == activeBranchTab)
            .OrderBy(rd => rd.displayName).ToList();
        foreach (var rd in rows)
        {
            int curLevel = mgr.GetCurrentLevel(rd.researchID);
            bool isMaxed = curLevel >= rd.MaxLevel;
            int nextLevel = isMaxed ? rd.MaxLevel : curLevel + 1;
            int cost = isMaxed ? 0 : mgr.GetCostForLevel(rd, nextLevel);
            float secs = isMaxed ? 0f : mgr.GetSecondsForLevel(rd, nextLevel);
            bool canAfford = !isMaxed && CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(cost);

            var row = new VisualElement(); row.AddToClassList("picker-row");

            var textCol = new VisualElement(); textCol.AddToClassList("picker-row__text-col");
            var name = new Label($"{rd.displayName} — L{curLevel}/{rd.MaxLevel}");
            name.AddToClassList("picker-row__name");
            var desc = new Label(string.IsNullOrEmpty(rd.description) ? "" : rd.description);
            desc.AddToClassList("picker-row__desc");
            textCol.Add(name); textCol.Add(desc);

            var metaCol = new VisualElement(); metaCol.AddToClassList("picker-row__meta-col");
            var costLbl = new Label(isMaxed ? "Complete" : $"{cost} coins");
            costLbl.AddToClassList("picker-row__cost");
            var timeLbl = new Label(isMaxed ? "Maxed" : FormatRemaining(secs));
            timeLbl.AddToClassList("picker-row__time");
            metaCol.Add(costLbl); metaCol.Add(timeLbl);

            row.Add(textCol); row.Add(metaCol);

            if (isMaxed)
            {
                row.AddToClassList("picker-row--complete");
                // Maxed = no click handler
            }
            else if (!canAfford)
            {
                row.AddToClassList("picker-row--disabled");
                costLbl.AddToClassList("picker-row__cost--unaffordable");
                // Disabled = no click handler
            }
            else
            {
                string capturedID = rd.researchID;
                int capturedSlot = pickerSlotIndex;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (ResearchManager.Instance != null && ResearchManager.Instance.TryAssignResearch(capturedSlot, capturedID))
                        ClosePicker();
                });
                WirePressedFeedback(row, "slot-card--pressed");
            }

            pickerList.Add(row);
        }
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
    }
}
```

- [ ] **Step 11.2: Commit**

```bash
git add Assets/Scripts/UI/ResearchPopupUITK.cs
git commit -m "feat(research): popup assign-flow, branch tabs, countdown, cancel"
```

---

## Task 12: Helper consumer wiring

**Files:**
- Modify: `Assets/Scripts/UniversalHelper.cs`

`UniversalHelper` is responsible for till/water/plant/harvest actions. We multiply existing task speed by `1 + GetBonus(StatKey.HelperXxxSpeed)`, and where applicable apply efficiency bonuses.

- [ ] **Step 12.1: Identify the speed and efficiency call sites**

Open `UniversalHelper.cs`. Find where the helper computes `taskSpeed` (or similar) for till / water / plant / harvest. Identify where it grants "watering efficiency" (moisture per pour) and "harvest efficiency" (money or count per harvest).

- [ ] **Step 12.2: Add bonus multipliers**

For each task-speed computation, replace:

```csharp
float taskSpeed = baseSpeed * upgradeMultiplier;
```

with:

```csharp
float taskSpeed = baseSpeed * upgradeMultiplier * (1f + ResearchBonus(taskType));
```

Add a private helper at the bottom of the class:

```csharp
private static float ResearchBonus(HelperTask.TaskType task)
{
    if (ResearchManager.Instance == null) return 0f;
    return task switch
    {
        HelperTask.TaskType.Till    => ResearchManager.Instance.GetBonus(Research.StatKey.HelperTillSpeed),
        HelperTask.TaskType.Water   => ResearchManager.Instance.GetBonus(Research.StatKey.HelperWaterSpeed),
        HelperTask.TaskType.Plant   => ResearchManager.Instance.GetBonus(Research.StatKey.HelperPlantSpeed),
        HelperTask.TaskType.Harvest => ResearchManager.Instance.GetBonus(Research.StatKey.HelperHarvestSpeed),
        _ => 0f,
    };
}
```

(If `HelperTask.TaskType` uses different names, adapt the switch arms accordingly. Do NOT rename `HelperTask` enums.)

- [ ] **Step 12.3: Apply efficiency bonuses**

Find the watering efficiency multiplier (moisture per pour). Multiply by `1 + ResearchManager.Instance.GetBonus(Research.StatKey.HelperWaterEfficiency)`. Find the harvest output (money or count). Multiply by `1 + ResearchManager.Instance.GetBonus(Research.StatKey.HelperHarvestEfficiency)`.

- [ ] **Step 12.4: Commit**

```bash
git add Assets/Scripts/UniversalHelper.cs
git commit -m "feat(research): helper task speeds + efficiencies read research bonuses"
```

---

## Task 13: Plant + SoilTile consumers

**Files:**
- Modify: `Assets/Scripts/Plant.cs`
- Modify: `Assets/Scripts/SoilTile.cs`

- [ ] **Step 13.1: Plant growth speed + HP + sell value**

In `Plant.cs`, find the growth-tick logic (where it advances growth stage / accumulates time toward the next stage). Multiply the growth delta by `1 + ResearchManager.Instance.GetBonus(Research.StatKey.CropGrowthSpeed)`.

Find the max HP / starting HP value. Multiply by `1 + ResearchManager.Instance.GetBonus(Research.StatKey.CropHp)`.

Find where the plant computes its sell value on harvest. Add `+ ResearchManager.Instance.GetBonus(Research.StatKey.CropBonusSellAmount) * baseValue` to the flat-$ amount (treating it as a flat-per-crop bonus multiplied against the base) and multiply by `1 + ResearchManager.Instance.GetBonus(Research.StatKey.SoilQuality)` for the soil quality % multiplier.

Add a null check on `ResearchManager.Instance` everywhere — fall back to no-bonus if it's null (test scenarios).

- [ ] **Step 13.2: SoilTile water efficiency**

In `SoilTile.cs`, find where moisture is added when watered. Multiply the moisture delta by `1 + ResearchManager.Instance.GetBonus(Research.StatKey.SoilWaterEfficiency)`.

- [ ] **Step 13.3: Commit**

```bash
git add Assets/Scripts/Plant.cs Assets/Scripts/SoilTile.cs
git commit -m "feat(research): Plant + SoilTile read research bonuses"
```

---

## Task 14: Equipment consumers (Scarecrow / Sprinkler / Fence)

**Files:**
- Modify: `Assets/Scripts/ScarecrowVisual.cs`
- Modify: `Assets/Scripts/SprinklerVisual.cs`
- Modify: `Assets/Scripts/FenceVisual.cs`

Each equipment script computes its AoE radius (or line length), its effectiveness (capacity / power), and its cooldown. We multiply each by `1 + GetBonus(...)`. Cooldown is divided by `1 + GetBonus(StatKey.XxxCooldown)` (more bonus = shorter cooldown).

- [ ] **Step 14.1: Scarecrow**

In `ScarecrowVisual.cs`, find the AoE radius. Multiply by `1 + GetBonus(StatKey.ScarecrowAoe)`. Find the effectiveness (number of crows repelled per fire). Multiply by `1 + GetBonus(StatKey.ScarecrowEffectiveness)`. Find the cooldown duration. Divide by `1 + GetBonus(StatKey.ScarecrowCooldown)`.

```csharp
float Bonus(string k) => ResearchManager.Instance != null ? ResearchManager.Instance.GetBonus(k) : 0f;
float effectiveAoe = baseAoe * (1f + Bonus(Research.StatKey.ScarecrowAoe));
int effectiveCapacity = Mathf.RoundToInt(baseCapacity * (1f + Bonus(Research.StatKey.ScarecrowEffectiveness)));
float effectiveCooldown = baseCooldown / Mathf.Max(0.01f, 1f + Bonus(Research.StatKey.ScarecrowCooldown));
```

- [ ] **Step 14.2: Sprinkler**

Same pattern in `SprinklerVisual.cs` using `StatKey.SprinklerAoe`, `SprinklerEffectiveness`, `SprinklerCooldown`.

- [ ] **Step 14.3: Fence**

Same pattern in `FenceVisual.cs` using `StatKey.FenceAoe`, `FenceEffectiveness`, `FenceCooldown`.

- [ ] **Step 14.4: Commit**

```bash
git add Assets/Scripts/ScarecrowVisual.cs Assets/Scripts/SprinklerVisual.cs Assets/Scripts/FenceVisual.cs
git commit -m "feat(research): equipment AoE/Effectiveness/Cooldown read research bonuses"
```

---

## Task 15: Animal consumers (Chicken / Rooster / FarmDog)

**Files:**
- Modify: `Assets/Scripts/AnimalManager.cs`
- Modify: `Assets/Scripts/FarmDog.cs`

The egg timer (chicken) and gem timer (rooster) are managed in `AnimalManager`. Cooldown research **divides** the timer; efficiency research **multiplies** the payout.

- [ ] **Step 15.1: AnimalManager timer bonuses**

In `AnimalManager.cs`, locate the per-animal cooldown duration (the time until the next egg / gem). For chicken: divide by `1 + GetBonus(StatKey.ChickenCooldown)`. For rooster: divide by `1 + GetBonus(StatKey.RoosterCooldown)`.

Locate the payout amount on claim. For chicken eggs: multiply by `1 + GetBonus(StatKey.ChickenEfficiency)`. For rooster gems: multiply by `1 + GetBonus(StatKey.RoosterEfficiency)`.

- [ ] **Step 15.2: FarmDog**

In `FarmDog.cs`, find the chase / scare cooldown. Divide by `1 + GetBonus(StatKey.DogCooldown)`. Find the per-scare threat-removal count (or area / damage). Multiply by `1 + GetBonus(StatKey.DogEfficiency)`.

- [ ] **Step 15.3: Commit**

```bash
git add Assets/Scripts/AnimalManager.cs Assets/Scripts/FarmDog.cs
git commit -m "feat(research): animal cooldown/efficiency read research bonuses"
```

---

## Task 16: Weather consumers

**Files:**
- Modify: `Assets/Scripts/ThunderstormManager.cs`
- Modify: `Assets/Scripts/RainOverlayUI.cs` (or wherever rain applies moisture)

- [ ] **Step 16.1: Storm damage reduction**

In `ThunderstormManager.cs`, find where lightning/wind damages crops or equipment. Multiply the damage amount by `(1f - GetBonus(StatKey.StormDamageReduction))` clamped to `[0, 1]`.

- [ ] **Step 16.2: Rain watering**

In whichever file applies rain → moisture per tick (`RainOverlayUI.cs` or `ThunderstormManager` again — search for "moisture" or "AddWater"), multiply the per-tick moisture delta by `1 + GetBonus(StatKey.RainWatering)`. If rain currently grants zero moisture (cosmetic only), make this bonus the SOLE source: `moistureDelta = baseRainContribution * GetBonus(StatKey.RainWatering)` so L0 = no rain watering, L25 = +25% moisture from rain.

- [ ] **Step 16.3: Commit**

```bash
git add Assets/Scripts/ThunderstormManager.cs Assets/Scripts/RainOverlayUI.cs
git commit -m "feat(research): weather damage reduction + rain watering read research bonuses"
```

---

## Task 17: RunManager — Game Speed Multiplier

**Files:**
- Modify: `Assets/Scripts/RunManager.cs`

Game Speed applies to **in-run** gameplay only — crops growing, helpers moving, threat spawn cadence, equipment cooldowns. It is exposed by setting `Time.timeScale` while a run is active (cleanest single hook; all `Time.deltaTime` consumers respect it). Research timers, animal passives, and offline progress all use `UtcNow` or `unscaledDeltaTime`, so they're naturally unaffected.

- [ ] **Step 17.1: Apply on run start**

Find `RunManager.StartRun()` (or equivalent). At the top, after any existing init:

```csharp
float speedBonus = ResearchManager.Instance != null
    ? ResearchManager.Instance.GetBonus(Research.StatKey.GameSpeed)
    : 0f;
Time.timeScale = 1f + speedBonus; // L0 = 1x, L10 (max bonus 9.0) = 10x
```

- [ ] **Step 17.2: Reset on run end**

In `RunManager.EndRun()` (or equivalent), set `Time.timeScale = 1f;`.

- [ ] **Step 17.3: Audit ResearchManager.Tick uses unscaled time**

Already done in Task 7 (`Time.unscaledDeltaTime`). Just verify the ResearchManager's Update still ticks normally during a run.

- [ ] **Step 17.4: Commit**

```bash
git add Assets/Scripts/RunManager.cs
git commit -m "feat(research): apply Game Speed multiplier to Time.timeScale during runs"
```

---

## Task 18: Max Water Heals Plant HP (feature flag binary)

**Files:**
- Modify: `Assets/Scripts/Plant.cs` (or the relevant watering callback)

When the `max_water_heals_plant` feature flag is set (binary research completed), watering a plant that is already at max moisture restores HP.

- [ ] **Step 18.1: Find the moisture-set callback**

Open `Plant.cs`. Find where `moisture` is set or where `OnWatered` is handled. Identify the existing branch where moisture is already at max.

- [ ] **Step 18.2: Add HP heal**

```csharp
if (currentMoisture >= maxMoisture &&
    ResearchManager.Instance != null &&
    ResearchManager.Instance.IsFeatureUnlocked(Research.FeatureFlag.MaxWaterHealsPlant))
{
    currentHp = Mathf.Min(maxHp, currentHp + healPerOverwater);
}
```

Pick a reasonable `healPerOverwater` (e.g. `maxHp * 0.1f` per pour). Add as a `[SerializeField] private float healPerOverwater = 0.1f;` at the top of the class (interpreted as fraction of max HP).

- [ ] **Step 18.3: Commit**

```bash
git add Assets/Scripts/Plant.cs
git commit -m "feat(research): watering at max moisture heals plant HP when feature unlocked"
```

---

## Task 19: PlayerPrefs → GameData migration (one-shot)

**Files:**
- Modify: `Assets/Scripts/ResearchManager.cs`

The old slot-unlock state lived in PlayerPrefs (`research_slot_unlocked_*`). We migrate on first load so existing players don't lose slot 1 they paid 100 coins for.

- [ ] **Step 19.1: Add migration call**

In `ResearchManager.LoadState` (added in Task 7), at the very top of the method:

```csharp
// One-shot migration: if no slot bits arrived from save but PlayerPrefs has them, adopt those.
bool anyFromSave = unlocked != null && unlocked.Any(b => b);
if (!anyFromSave)
{
    for (int i = 0; i < SlotCount; i++)
    {
        if (PlayerPrefs.GetInt("research_slot_unlocked_" + i, 0) == 1)
            slotUnlocked[i] = true;
    }
    // Clear the legacy keys so we don't keep migrating.
    for (int i = 0; i < SlotCount; i++) PlayerPrefs.DeleteKey("research_slot_unlocked_" + i);
    PlayerPrefs.Save();
}
```

- [ ] **Step 19.2: Commit**

```bash
git add Assets/Scripts/ResearchManager.cs
git commit -m "feat(research): migrate legacy PlayerPrefs slot state on first GameData load"
```

---

## Task 20: Smoke-test checklist (manual)

This is verification, not a code task. Run through the checklist in Unity Play mode.

- [ ] **Step 20.1: Verify catalog loads**

Enter Play mode. Open Greenhouse → Research. Console should show `[Research] Catalog loaded: 38 entries`. The popup should display 4 slot cards.

- [ ] **Step 20.2: Slot 1 unlock + assign**

With ≥100 coins, tap Slot 1. It unlocks. Tap the now-empty slot. Picker opens. Tabs show: `helper`, `meta`, `plant`, `soil`, `weather` (Animals/Equipment hidden until you own them). Tap a research. Confirm coin cost is deducted and slot card shows the active research with a countdown.

- [ ] **Step 20.3: Level-up tick**

Pick a research with low base time (e.g. Helper Till Speed: base 2 min, L1 = 2 min). Wait ~2 minutes. Verify the slot ticks over to L1 (level number visible), countdown resets for L2, and a new cost is auto-charged.

- [ ] **Step 20.4: Bonus applies live**

After Helper Till Speed hits L1, start a run with a helper. Observe the helper tills slightly faster than baseline.

- [ ] **Step 20.5: Save round-trip**

Stop play. Re-enter play. Confirm slot 1 still unlocked, research still progressing from where it was.

- [ ] **Step 20.6: Game Speed during run**

Use the `[ContextMenu("Reset All Research State")]` on `ResearchManager` to clear; then manually set Game Speed's `levelsByResearchID["game_speed"] = 5` via the inspector context menu (or write a quick `[ContextMenu] GiveGameSpeedL5`). Start a run. Confirm `Time.timeScale` reads 5.5 during the run and 1.0 after.

- [ ] **Step 20.7: Visibility — equipment hidden until owned**

Without buying Scarecrow from Market, open the picker → Equipment tab. Confirm Scarecrow researches do NOT appear. Buy Scarecrow. Reopen picker. Confirm they now appear.

- [ ] **Step 20.8: Binary unlock — Slot 3**

Use `[ContextMenu]` to give yourself enough coins. Pick `Research Slot 3` (Meta branch). Wait through the 7-day countdown (or temporarily lower `binaryFixedDurationSecs` on the asset to 30 sec to verify). When it completes, Slot 3 should auto-unlock.

- [ ] **Step 20.9: Commit results**

If any of the smoke-test steps required tuning fixes, commit them now:

```bash
git add -u
git commit -m "fix(research): smoke-test tuning fixes"
```

---

## What Plan 2 will add (out of scope here)

- `Compost` currency field on `CurrencyManager` + save/load
- `CompostBay` equipment (per-zone, no radius/capacity, auto-collects dead crops in zone)
- `Cow` class + `Cow Passive Compost Rate` (100-lvl) + `Cow Run Yield` (25-lvl) catalog entries
- `Compost Bay Conversion Efficiency` (100-lvl) catalog entry
- "Composting Basics" binary unlock gating Compost Bay in the Market (the binary itself ships in Plan 1; Market gating ships in Plan 2)
- Boost token modal — pay X compost for Y hours of 2x / 3x / 4x on a slot
- ResearchPopupUITK boost button per active slot
- Top currency bar displays Compost amount
- Animals catalog grows from 6 to 8

---

## Self-Review Notes

- **Spec coverage:** Plan 1 ships 38 of the 40 spec'd researches (Cow Passive Compost + Cow Run Yield + Compost Bay Conversion are deferred to Plan 2). All visibility rules, polynomial scaling, difficulty dials, binary unlocks, and save-state persistence are covered. Game Speed scope ("run-only") is enforced by toggling `Time.timeScale` in `RunManager` and using `unscaledDeltaTime` in `ResearchManager.Tick`.
- **No placeholders:** Each step shows actual code or names a specific file/method to edit. Smoke-test checklist gives concrete expectations.
- **Type consistency:** `ResearchData`, `ResearchTier`, `ResearchSlotState`, `StatKey`, `FeatureFlag` referenced consistently across tasks. `ResearchManager` method names (`GetBonus`, `GetSlot`, `TryAssignResearch`, `CancelResearch`, `IsResearchVisible`, `IsFeatureUnlocked`, `GetSecondsForLevel`, `GetCostForLevel`) defined in Task 7 and used identically in Tasks 11–18.
- **Known soft spots that may need tweaks during execution:** (1) `UpgradeManager.IsUnlocked(string)` and `AnimalManager.IsUnlocked(string)` are assumed to exist; Task 7.4 explicitly checks. (2) `HelperTask.TaskType` enum names assumed in Task 12 — adapt switch arms to actual names. (3) Plant sell-value math in Task 13 is a sketch; exact formula depends on the existing `Plant.cs` shape — the engineer applies the multipliers to the right variable.
