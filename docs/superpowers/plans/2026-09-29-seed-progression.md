# Seed Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** New farms start with Radish and unlock the other 8 crops at Hazel's Plants stall — priced packets and masked (research-gated) packets — in one fixed crop order, with teaching mail and tips and a town-board chain that ends in spending the first Barn point.

**Architecture:**
- A pure `SeedLadder` + `SeedShopRules` in `IdleFarm.EconomyCore` defines the fixed order, prices, starter, and packet states (unit-tested).
- `CropData` carries the editable per-crop values, which an editor tool seeds from the ladder.
- A runtime `CropOwnership` service stores ownership as `UpgradeManager` permanent levels under new `seed_<slug>` ids, so old saves start fresh with no migration step.
- UI (seed menu, Plants stall, Almanac) and systems (compost, reputation, tutorials, narrative) read ownership through that service.

**Tech Stack:** Unity 6.3 (6000.3.9f1), C#, UI Toolkit (code-built + UXML popups), NUnit EditMode tests (`Assets/Tests/EditMode`, assembly `IdleFarm.EditModeTests` references only `IdleFarm.EconomyCore`).

**Spec:** `docs/superpowers/specs/2026-09-29-seed-progression-design.md`

## Global Constraints

- **One fixed crop order, for every player, everywhere:** Radish, Carrot, Green Beans, Corn, Strawberry, Tomato, Blueberry, Green Pepper, Red Pepper.
  - Code iterates `CropDatabase.allCrops` and filters it. Never sort by ownership or purchase time, and never re-collect crops from a dictionary.
- Prices (Coins): Carrot 10; Green Beans, Corn, Strawberry 100; Tomato, Blueberry, Green Pepper, Red Pepper 1,000. Radish is the free starter.
- Coins per harvest: Radish 1; Carrot 2; Green Beans 2; Strawberry 2; Corn 3; Tomato 3; Blueberry 3; Green Pepper 4; Red Pepper 4.
- Masked crops: Corn (feature flag `composting_basics`) and Strawberry (feature flag `cannery_unlocked`). Masked hint text: "Discovered through research".
- Ownership ids: `seed_<slug>`, where the slug is the crop name lower-cased with spaces replaced by `_`. The old `*_unlock` crop ids are never read.
- Corn `compostMultiplier` = 2. Every other crop is 1.
- **No emoji in UITK text:** they are invisible on Android. Use sprites or plain words.
- **Button colours use the game browns** (walnut `rgb(92,58,24)`, `rgb(120,90,45)`, cream text `rgb(255,240,210)`, gold `rgb(230,180,60)`), never generic blue. No coloured one-side accent stripes.
- **Git:** do not commit. This project only commits when the user asks, so the checkpoints below are "tests green", not commits.
- **Copy:** the source of truth is `Assets/Resources/LetterCatalog.asset`, and `NarrativeDefaults` seeds missing ids. Keep `docs/narrative/cast-and-copy.md` regenerated whenever copy changes.

## How to build, test and play (read once)

Unity must be open. All bridges are file requests under `Temp/` in the project root, and results appear within about 2 seconds.

- **Recompile:**

  ```bash
  touch Temp/compile_marker; : > Temp/refresh.request
  ```

  Then wait until `Library/ScriptAssemblies/Assembly-CSharp.dll` is newer than `Temp/compile_marker`. Check for errors with `grep -a "error CS" "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail`.
- **EditMode tests:**

  ```bash
  rm -f Temp/editmode_test_results.txt; : > Temp/run_editmode_tests.request
  ```

  Poll until `Temp/editmode_test_results.txt` contains `RESULT:`. It reads `RESULT: Passed passed=N failed=0` plus every non-passing test.
- **Run an editor menu item:** `echo "Farm Game/Seeds/Apply Seed Progression" > Temp/menu.request`. The result goes to `Temp/ui_drive_result.txt`.
- **Set a string on an asset:** write `Temp/asset_string.request` with three lines: asset path, serialized property path, value.
- **Play mode:**
  - `: > Temp/enter_play_mode.request` / `: > Temp/exit_play_mode.request`.
  - The welcome-back modal needs `echo "540,1006" > Temp/ui_press.request`, twice.
  - Call any method: `echo "Type.Method|arg" > Temp/open_popup.request`, result in `Temp/open_popup_result.txt`.
  - Screenshot: `: > Temp/screenshot.request` produces `Temp/game_screenshot.png`, at a game view of 1080x2400.
  - `ui_press`/`ui_pick` coordinates are screen pixels with a **bottom-left** origin (`y = 2400 - screenshotY`).
  - Click uGUI buttons by GameObject name with `ui_click` (StartRunButton, BackToFarmButton, WoodsPanButton, LakePanButton).
  - Scroll a ScrollView with `echo "name,x,y" > Temp/ui_scroll.request` (`end` is allowed).
- **Before any play-mode session:**
  - Back up `%USERPROFILE%/AppData/LocalLow/DefaultCompany/IdleFarm - Silo/gamedata.json`, and restore it afterwards.
  - Delete any `runs_completed_h*` value under `HKCU\Software\Unity\UnityEditor\DefaultCompany\IdleFarm - Silo` that the session created.
- **Do not edit `.asset`/`.unity`/`.prefab` YAML by hand.** Use the editor tool in Task 3, `asset_string`, or the Inspector.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `Assets/Scripts/EconomyCore/SeedLadder.cs` | Create | The fixed crop order and each crop's starting price/flag/starter/coins/compost values (pure data). |
| `Assets/Scripts/EconomyCore/SeedShopRules.cs` | Create | `SeedState` enum, `State(...)`, `OwnershipKey(...)`, `FilterInOrder(...)` (pure). |
| `Assets/Scripts/EconomyCore/CropTraits.cs` | Modify | Adds the "Great compost" tag. |
| `Assets/Scripts/EconomyCore/ReputationCore.cs` | Modify | `DeliveryRequest.isWelcomeBasket`. |
| `Assets/Scripts/EconomyCore/LetterContent.cs` | Modify | Appends `CtaKind.OpenPlantsShop`. |
| `Assets/Scripts/EconomyCore/NarrativeDefaults.cs` | Modify | Hazel, 3 letters, 3 tips, updated copy. |
| `Assets/Scripts/CropData.cs` | Modify | `unlockCost`, `unlockFeatureFlag`, `isStarter`, `compostMultiplier`. |
| `Assets/Scripts/Economy/CropOwnership.cs` | Create | Runtime ownership: `IsOwned`, `StateOf`, `Owned`, `TryBuy`. |
| `Assets/Scripts/UpgradeManager.cs` | Modify | `GrantPermanentLevel(id, level)`: sets a level without spending or checking the run. |
| `Assets/Editor/SeedProgressionTools.cs` | Create | Menu `Farm Game/Seeds/Apply Seed Progression`: reorders `allCrops`, writes `CropData` values, updates the welcome and compost letters. |
| `Assets/Tests/EditMode/SeedLadderTests.cs` | Create | Pure rule tests + the `CropDatabase.asset` order guard. |
| `Assets/Scripts/SeedSelectionData.cs` | Modify | `RemoveCropsWhere`, `CanStartRun` (a run starts with at least one field filled). |
| `Assets/Scripts/SeedSelectionPopup.cs` | Modify | Owned-only rail + Hazel tile; clears unowned assignments. |
| `Assets/Scripts/UI/ShopPopupUITK.cs` | Modify | Plants section built from `CropDatabase` with Owned/Priced/Masked rows. |
| `Assets/Scripts/Almanac/AlmanacCatalog.cs` | Modify | Crop page states; pest pages skip masked crops. |
| `Assets/Scripts/UI/AlmanacPopupUITK.cs` | Modify | `OpenEntry` ignores locked entries. |
| `Assets/Scripts/CompostBay.cs` | Modify | Multiplies compost by `compostMultiplier`. |
| `Assets/Scripts/Reputation/ReputationManager.cs` | Modify | Crop availability = owned crops; Welcome basket. |
| `Assets/Scripts/UI/TownRequestsPopupUITK.cs` | Modify | Crop list = owned crops; basket can't be skipped. |
| `Assets/Scripts/UI/InboxPopupUITK.cs` | Modify | `OpenPlantsShop` CTA. |
| `Assets/Scripts/Tutorial/OnboardingTutorials.cs` | Modify | `OnFirstRegrow`, new Collect/Sell trigger, `OnBarnOpened` spend tip. |
| `Assets/Scripts/UI/BarnPopupUITK.cs` | Modify | `FirstSpendableButton`; calls `OnBarnOpened`. |
| `Assets/Scripts/Plant.cs` | Modify | Calls `OnboardingTutorials.OnFirstRegrow()` in `StartRegrowth`. |

---

### Task 1: Pure ladder and shop rules (EconomyCore)

**Files:**
- Create: `Assets/Scripts/EconomyCore/SeedLadder.cs`
- Create: `Assets/Scripts/EconomyCore/SeedShopRules.cs`
- Modify: `Assets/Scripts/EconomyCore/CropTraits.cs:20-36`
- Test: `Assets/Tests/EditMode/SeedLadderTests.cs`, `Assets/Tests/EditMode/CropTraitsTests.cs`

**Interfaces:**
- Produces:
  - `SeedLadder.Entries` (`IReadOnlyList<SeedLadder.Entry>`), where `Entry` has fields `string cropName`, `int cost`, `string featureFlag`, `bool starter`, `int coinValue`, `float compostMultiplier`.
  - `SeedLadder.Order` (`IReadOnlyList<string>`).
  - `enum SeedState { Owned, Priced, Masked }`.
  - `SeedShopRules.State(bool isStarter, bool owned, bool hasCondition, bool conditionMet) → SeedState`.
  - `SeedShopRules.OwnershipKey(string cropName) → string`.
  - `SeedShopRules.FilterInOrder<T>(IEnumerable<T> ordered, Func<T,bool> keep) → List<T>`.
  - `SeedShopRules.MaskedHint` const = `"Discovered through research"`.
  - `CropTraits.Tags(..., float compostMultiplier = 1f)` adds `"Great compost"` when above 1.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/SeedLadderTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;

public class SeedLadderTests
{
    private static readonly string[] Expected =
        { "Radish", "Carrot", "Green Beans", "Corn", "Strawberry", "Tomato", "Blueberry", "Green Pepper", "Red Pepper" };

    [Test]
    public void Order_IsTheFixedLadder()
    {
        CollectionAssert.AreEqual(Expected, SeedLadder.Order.ToArray());
    }

    [Test]
    public void Ladder_HasExactlyOneStarter_Radish()
    {
        var starters = SeedLadder.Entries.Where(e => e.starter).Select(e => e.cropName).ToArray();
        CollectionAssert.AreEqual(new[] { "Radish" }, starters);
    }

    [Test]
    public void Ladder_PricesAndFlags_MatchSpec()
    {
        var byName = SeedLadder.Entries.ToDictionary(e => e.cropName);
        Assert.AreEqual(10, byName["Carrot"].cost);
        Assert.AreEqual(100, byName["Green Beans"].cost);
        Assert.AreEqual(100, byName["Corn"].cost);
        Assert.AreEqual(1000, byName["Red Pepper"].cost);
        Assert.AreEqual("composting_basics", byName["Corn"].featureFlag);
        Assert.AreEqual("cannery_unlocked", byName["Strawberry"].featureFlag);
        Assert.AreEqual(2f, byName["Corn"].compostMultiplier);
        Assert.AreEqual(1, byName["Radish"].coinValue);
        Assert.AreEqual(4, byName["Green Pepper"].coinValue);
    }

    [TestCase(true,  false, false, false, SeedState.Owned)]   // starter
    [TestCase(false, true,  true,  false, SeedState.Owned)]   // bought beats an unmet condition
    [TestCase(false, false, false, false, SeedState.Priced)]  // price-only
    [TestCase(false, false, true,  true,  SeedState.Priced)]  // condition met -> priced
    [TestCase(false, false, true,  false, SeedState.Masked)]  // condition unmet -> masked
    public void State_CoversEveryCase(bool starter, bool owned, bool hasCond, bool condMet, SeedState expected)
    {
        Assert.AreEqual(expected, SeedShopRules.State(starter, owned, hasCond, condMet));
    }

    [TestCase("Radish", "seed_radish")]
    [TestCase("Green Beans", "seed_green_beans")]
    [TestCase("Red Pepper", "seed_red_pepper")]
    public void OwnershipKey_Slugs(string name, string key)
    {
        Assert.AreEqual(key, SeedShopRules.OwnershipKey(name));
    }

    [Test]
    public void FilterInOrder_KeepsLadderOrder_WhateverThePurchaseOrder()
    {
        // Bought Tomato first, then Carrot: the result must still follow the ladder.
        var owned = new[] { "Tomato", "Radish", "Carrot" };
        var result = SeedShopRules.FilterInOrder(SeedLadder.Order, n => owned.Contains(n));
        CollectionAssert.AreEqual(new[] { "Radish", "Carrot", "Tomato" }, result);
    }
}
```

Append to `Assets/Tests/EditMode/CropTraitsTests.cs`, inside the class:

```csharp
    [Test]
    public void Tags_GreatCompost_OnlyAboveOne()
    {
        CollectionAssert.Contains(CropTraits.Tags(275, 80, false, false, 1f, 1f, 1f, compostMultiplier: 2f), "Great compost");
        CollectionAssert.DoesNotContain(CropTraits.Tags(275, 80, false, false, 1f, 1f, 1f), "Great compost");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode tests (see "How to build, test and play").
Expected: compile errors (`SeedLadder`, `SeedShopRules`, `SeedState` not found; `Tags` has no `compostMultiplier`), so there is no `RESULT: Passed` line.

- [ ] **Step 3: Implement**

Create `Assets/Scripts/EconomyCore/SeedLadder.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The one fixed crop order for every player, plus each crop's starting price / unlock condition /
/// coins-per-harvest / compost multiplier (spec: docs/superpowers/specs/2026-09-29-seed-progression-design.md).
/// The live, Inspector-editable values sit on each CropData; Farm Game > Seeds > Apply Seed Progression
/// copies these onto them and reorders CropDatabase.allCrops to match <see cref="Order"/>.
/// </summary>
public static class SeedLadder
{
    public struct Entry
    {
        public string cropName;
        public int cost;
        public string featureFlag;
        public bool starter;
        public int coinValue;
        public float compostMultiplier;
    }

    private static Entry E(string name, int cost, int coins, string flag = "", bool starter = false, float compost = 1f) =>
        new Entry { cropName = name, cost = cost, featureFlag = flag, starter = starter, coinValue = coins, compostMultiplier = compost };

    public static readonly IReadOnlyList<Entry> Entries = new[]
    {
        E("Radish",       0,    1, starter: true),
        E("Carrot",       10,   2),
        E("Green Beans",  100,  2),
        E("Corn",         100,  3, flag: "composting_basics", compost: 2f),
        E("Strawberry",   100,  2, flag: "cannery_unlocked"),
        E("Tomato",       1000, 3),
        E("Blueberry",    1000, 3),
        E("Green Pepper", 1000, 4),
        E("Red Pepper",   1000, 4),
    };

    public static readonly IReadOnlyList<string> Order = Entries.Select(e => e.cropName).ToArray();
}
```

Create `Assets/Scripts/EconomyCore/SeedShopRules.cs`:

```csharp
using System;
using System.Collections.Generic;

/// <summary>What a seed packet looks like in Hazel's stall.</summary>
public enum SeedState { Owned, Priced, Masked }

/// <summary>Pure rules for crop ownership and the Plants stall. No Unity types.</summary>
public static class SeedShopRules
{
    public const string MaskedHint = "Discovered through research";

    /// <summary>Owned if it's the starter or bought; otherwise Masked while an unlock condition is
    /// unmet, else Priced (visible, locked, shows its price).</summary>
    public static SeedState State(bool isStarter, bool owned, bool hasCondition, bool conditionMet)
    {
        if (isStarter || owned) return SeedState.Owned;
        if (hasCondition && !conditionMet) return SeedState.Masked;
        return SeedState.Priced;
    }

    /// <summary>UpgradeManager permanent-level id that records ownership, e.g. "seed_green_beans".
    /// New ids on purpose: the old "*_unlock" crop ids are never read, so old saves start fresh.</summary>
    public static string OwnershipKey(string cropName) =>
        "seed_" + (cropName ?? "").Trim().ToLowerInvariant().Replace(' ', '_');

    /// <summary>Filters an already-ordered list and keeps its order. Every crop list goes through
    /// this (or an equivalent Where) instead of sorting, so the order is the same for every player.</summary>
    public static List<T> FilterInOrder<T>(IEnumerable<T> ordered, Func<T, bool> keep)
    {
        var result = new List<T>();
        if (ordered == null) return result;
        foreach (T item in ordered) if (keep(item)) result.Add(item);
        return result;
    }
}
```

In `Assets/Scripts/EconomyCore/CropTraits.cs`, change the `Tags` signature and add the tag before the pest tags:

```csharp
    public static List<string> Tags(float growSeconds, int maxHp, bool regrows, bool cannable,
                                    float thirst, float deerAppetite, float crowAppetite,
                                    float compostMultiplier = 1f)
    {
        var tags = new List<string>();
        if (regrows) tags.Add("Regrows");
        if (growSeconds <= QuickGrowSeconds) tags.Add("Quick grower");
        else if (growSeconds >= SlowGrowSeconds) tags.Add("Slow grower");
        if (cannable) tags.Add("Cannable");
        if (compostMultiplier > 1f) tags.Add("Great compost");
        if (maxHp >= SturdyHp) tags.Add("Sturdy");
        else if (maxHp <= FragileHp) tags.Add("Fragile");
        if (thirst >= ThirstyAt) tags.Add("Thirsty");
        else if (thirst <= DroughtHardyAt) tags.Add("Drought-hardy");
        AddPestTag(tags, "Deer", deerAppetite);
        AddPestTag(tags, "Crows", crowAppetite);
        return tags;
    }
```

- [ ] **Step 4: Recompile and run the tests**

Expected: `RESULT: Passed` with the new tests included (previous total 275 → 275 + 13 = 288).

- [ ] **Step 5: Checkpoint.** Tests are green. Don't commit (project rule).

---

### Task 2: CropData fields + runtime `CropOwnership`

**Files:**
- Modify: `Assets/Scripts/CropData.cs` (after the `crowAppetite` field)
- Modify: `Assets/Scripts/UpgradeManager.cs` (after `PurchasePermanentUpgrade`)
- Create: `Assets/Scripts/Economy/CropOwnership.cs`

**Interfaces:**
- Consumes: `SeedShopRules.State`, `SeedShopRules.OwnershipKey`, `SeedState` (Task 1).
- Produces:
  - Fields `CropData.unlockCost` (int), `CropData.unlockFeatureFlag` (string), `CropData.isStarter` (bool), `CropData.compostMultiplier` (float, default 1).
  - `UpgradeManager.GrantPermanentLevel(string id, int level)`.
  - `CropOwnership.IsOwned(CropData) → bool`.
  - `CropOwnership.StateOf(CropData) → SeedState`.
  - `CropOwnership.Owned(CropDatabase) → List<CropData>` (in `allCrops` order).
  - `CropOwnership.TryBuy(CropData) → bool`.
  - `CropOwnership.OnOwnershipChanged` (static `event Action`).

- [ ] **Step 1: Add the CropData fields**

In `Assets/Scripts/CropData.cs`, after the `crowAppetite` field, add:

```csharp
    [Header("Seed Progression (Hazel's stall)")]
    [Tooltip("Coins to buy this crop's seed packet at Hazel's stall. Ignored for the starter.")]
    public int unlockCost = 100;
    [Tooltip("Optional research feature flag. While it's locked the packet is MASKED in the stall (a mystery) and its Almanac page is locked.")]
    public string unlockFeatureFlag = "";
    [Tooltip("The free starter crop (Radish). Always owned.")]
    public bool isStarter;
    [Tooltip("Compost Bay yield multiplier when this crop is lost. 2 = 'Great compost' (Corn).")]
    public float compostMultiplier = 1f;
```

- [ ] **Step 2: Add `GrantPermanentLevel` to UpgradeManager**

In `Assets/Scripts/UpgradeManager.cs`, after `PurchasePermanentUpgrade(...)`, add:

```csharp
    /// <summary>
    /// Sets a permanent level directly, without spending Coins or blocking during a run. Used by
    /// callers that handle their own payment (CropOwnership: seeds can be bought on a mid-run
    /// Market trip; ownership only matters at seed selection). Fires OnUpgradePurchased so shop UIs refresh.
    /// </summary>
    public void GrantPermanentLevel(string upgradeID, int level)
    {
        if (string.IsNullOrEmpty(upgradeID)) return;
        if (level <= 0) permanentLevels.Remove(upgradeID);
        else permanentLevels[upgradeID] = level;
        OnUpgradePurchased?.Invoke(upgradeID);
    }
```

- [ ] **Step 3: Create `Assets/Scripts/Economy/CropOwnership.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which crops a farm can plant. Ownership = UpgradeManager permanent level under
/// SeedShopRules.OwnershipKey ("seed_carrot"); the starter is always owned. Lists always come back in
/// CropDatabase.allCrops order (the one fixed crop order) — never re-sorted.
/// </summary>
public static class CropOwnership
{
    public static event Action OnOwnershipChanged;

    public static bool IsOwned(CropData crop)
    {
        if (crop == null) return false;
        if (crop.isStarter) return true;
        return UpgradeManager.Instance != null
            && UpgradeManager.Instance.GetPermanentLevel(SeedShopRules.OwnershipKey(crop.cropName)) > 0;
    }

    public static SeedState StateOf(CropData crop)
    {
        bool hasCondition = crop != null && !string.IsNullOrEmpty(crop.unlockFeatureFlag);
        bool conditionMet = hasCondition && ResearchManager.Instance != null
                            && ResearchManager.Instance.IsFeatureUnlocked(crop.unlockFeatureFlag);
        return SeedShopRules.State(crop != null && crop.isStarter, IsOwned(crop), hasCondition, conditionMet);
    }

    /// <summary>Owned crops, in allCrops order.</summary>
    public static List<CropData> Owned(CropDatabase db) =>
        db == null ? new List<CropData>() : SeedShopRules.FilterInOrder(db.allCrops, c => c != null && IsOwned(c));

    /// <summary>Buys a Priced packet with Coins. Masked or already-owned packets can't be bought.</summary>
    public static bool TryBuy(CropData crop)
    {
        if (crop == null || StateOf(crop) != SeedState.Priced) return false;
        if (UpgradeManager.Instance == null || CurrencyManager.Instance == null) return false;
        if (!CurrencyManager.Instance.SpendCoins(crop.unlockCost)) return false;

        UpgradeManager.Instance.GrantPermanentLevel(SeedShopRules.OwnershipKey(crop.cropName), 1);
        Debug.Log($"[Seeds] Bought {crop.cropName} for {crop.unlockCost} Coins");
        OnOwnershipChanged?.Invoke();
        NarrativeDirector.Raise("seed_bought");
        if (crop.canRegrow) NarrativeDirector.Raise("seed_bought:regrow");
        return true;
    }
}
```

- [ ] **Step 4: Recompile and run the EditMode tests.** Expected: no compile errors, `RESULT: Passed` (288).

- [ ] **Step 5: Checkpoint.** Don't commit.

---

### Task 3: Editor tool — apply the ladder to the assets, plus the order guard test

**Files:**
- Create: `Assets/Editor/SeedProgressionTools.cs`
- Modify: `Assets/Tests/EditMode/SeedLadderTests.cs` (add the guard test)

**Interfaces:**
- Consumes: `SeedLadder.Entries`, `SeedLadder.Order` (Task 1); the `CropData` fields (Task 2).
- Produces:
  - Menu item `Farm Game/Seeds/Apply Seed Progression`, idempotent.
  - After it runs:
    - `CropDatabase.asset` `allCrops` is in ladder order and `startingCrops` = [Radish].
    - Every `CropData` has its ladder values.
    - The welcome letter gives 50 Coins with the new body.
    - The compost letter body mentions Corn.

- [ ] **Step 1: Write the failing guard test**

Append to `SeedLadderTests` (it needs `using UnityEditor;` and `using UnityEngine;` at the top of the file):

```csharp
    // Guards the one fixed crop order: a stray drag in the CropDatabase Inspector must fail the suite
    // instead of silently reshuffling the stall, seed menu and Almanac. Reads via SerializedObject so
    // this EconomyCore-only test assembly needn't reference CropDatabase/CropData types.
    [Test]
    public void CropDatabaseAsset_AllCrops_FollowTheLadder()
    {
        var db = AssetDatabase.LoadMainAssetAtPath("Assets/Data/Crops/CropDatabase.asset");
        Assert.IsNotNull(db, "CropDatabase.asset missing");
        var list = new SerializedObject(db).FindProperty("allCrops");
        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object crop = list.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.IsNotNull(crop, $"allCrops[{i}] is empty");
            names.Add(new SerializedObject(crop).FindProperty("cropName").stringValue);
        }
        CollectionAssert.AreEqual(SeedLadder.Order.ToArray(), names.ToArray());
    }
```

- [ ] **Step 2: Run the tests and verify the guard fails.** Expected: `CropDatabaseAsset_AllCrops_FollowTheLadder` FAILS, because the current order starts Carrot, Tomato, Radish….

- [ ] **Step 3: Create `Assets/Editor/SeedProgressionTools.cs`**

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm Game > Seeds > Apply Seed Progression: copies SeedLadder onto the CropData assets, reorders
/// CropDatabase.allCrops into the one fixed crop order, makes Radish the only starting crop, and
/// applies the welcome / compost letter copy changes. Idempotent — safe to run again after tuning
/// SeedLadder (it overwrites the per-crop values with the ladder's).
/// </summary>
public static class SeedProgressionTools
{
    private const string DatabasePath = "Assets/Data/Crops/CropDatabase.asset";
    private const string CatalogPath = "Assets/Resources/LetterCatalog.asset";

    public const string WelcomeBody =
        "Dear {farmName},\n\nWelcome to the valley! This old farm has sat quiet for years, and we're all so glad someone is bringing it back to life.\n\n" +
        "I've left a sack of radish seeds in your shed. They're quick to grow and hard to get wrong - just the thing for a first harvest. Choose a field, and your helpers will plant, water and harvest for you.\n\n" +
        "Every harvest earns Money to buy more seeds, plus Coins you get to keep. Hazel at the seed stall in the Market has more kinds when you're ready - here's a little something toward your first packet.\n\n" +
        "- Mayor Bramble";

    public const string CompostBody =
        "Hey {farmName}!\n\nHeard you've been reading up on composting. Good news - I can put together a Compost Bay for you now. Swing by the shop and grab one; your soil will thank you.\n\n" +
        "Oh! And ask Hazel about corn. Those big stalks make twice the compost when a plant doesn't make it.\n\n- Pippa";

    [MenuItem("Farm Game/Seeds/Apply Seed Progression")]
    public static void Apply()
    {
        var db = AssetDatabase.LoadAssetAtPath<CropDatabase>(DatabasePath);
        if (db == null) { Debug.LogError("[Seeds] CropDatabase.asset not found"); return; }

        var byName = AssetDatabase.FindAssets("t:CropData")
            .Select(g => AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .ToDictionary(c => c.cropName);

        var ordered = new System.Collections.Generic.List<CropData>();
        foreach (SeedLadder.Entry e in SeedLadder.Entries)
        {
            if (!byName.TryGetValue(e.cropName, out CropData crop))
            {
                Debug.LogError($"[Seeds] No CropData named '{e.cropName}' - fix the asset or SeedLadder.");
                return;
            }
            Undo.RecordObject(crop, "Apply Seed Progression");
            crop.unlockCost = e.cost;
            crop.unlockFeatureFlag = e.featureFlag ?? "";
            crop.isStarter = e.starter;
            crop.coinValue = e.coinValue;
            crop.compostMultiplier = e.compostMultiplier;
            EditorUtility.SetDirty(crop);
            ordered.Add(crop);
        }

        Undo.RecordObject(db, "Apply Seed Progression");
        db.allCrops = ordered;
        db.startingCrops = ordered.Where(c => c.isStarter).ToList();
        EditorUtility.SetDirty(db);

        ApplyLetterCopy();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Seeds] Applied seed progression to {ordered.Count} crops: {string.Join(", ", ordered.Select(c => c.cropName))}");
    }

    private static void ApplyLetterCopy()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LetterCatalogSO>(CatalogPath);
        if (catalog == null || catalog.letters == null) { Debug.LogWarning("[Seeds] LetterCatalog not found"); return; }
        Undo.RecordObject(catalog, "Apply Seed Progression copy");
        foreach (LetterDef l in catalog.letters)
        {
            if (l == null) continue;
            if (l.id == "welcome") { l.body = WelcomeBody; l.rewardKind = RewardKind.Coins; l.rewardAmount = 50; }
            if (l.id == "compost_bay_unlock") l.body = CompostBody;
        }
        EditorUtility.SetDirty(catalog);
    }
}
```

(`RewardKind` is `{ None, Coins, Gems, Compost }`, and the welcome letter's asset already stores `rewardKind: 1` = Coins.)

- [ ] **Step 4: Recompile, run the menu item, and check the assets**

Run: `echo "Farm Game/Seeds/Apply Seed Progression" > Temp/menu.request`. Check `Temp/ui_drive_result.txt` for `OK: ran menu`, and the Editor.log for `[Seeds] Applied seed progression to 9 crops: Radish, Carrot, Green Beans, Corn, Strawberry, Tomato, Blueberry, Green Pepper, Red Pepper`.

Then verify:

```bash
grep -n "unlockCost\|isStarter\|coinValue\|compostMultiplier\|unlockFeatureFlag" Assets/Data/Crops/Crop_Corn.asset
grep -A3 "  - id: welcome" -m1 Assets/Resources/LetterCatalog.asset; grep -n "rewardAmount: 50" Assets/Resources/LetterCatalog.asset
```

Expected for Corn: `unlockCost: 100`, `unlockFeatureFlag: composting_basics`, `isStarter: 0`, `coinValue: 3`, `compostMultiplier: 2`.

- [ ] **Step 5: Run the EditMode tests.** Expected: `RESULT: Passed` (289), with the guard test now passing.

- [ ] **Step 6: Checkpoint.** Don't commit.

---

### Task 4: Seed menu — owned crops only, runs start with at least one field, Hazel tile

**Files:**
- Modify: `Assets/Scripts/SeedSelectionData.cs` (`IsCropAssigned` usage + new `RemoveCropsWhere`)
- Modify: `Assets/Scripts/SeedSelectionPopup.cs:114-177` (Show / IsReadyToRun / HasAnyCropEquipped / LoadAndApplySavedSelections), `:288-338` (BuildSeedRail / RefreshSeedRailStates), `:406-417` (OnSeedTileClicked)

**Interfaces:**
- Consumes: `CropOwnership.IsOwned(CropData)`, `CropOwnership.Owned(CropDatabase)`, `CropOwnership.OnOwnershipChanged` (Task 2).
- Produces:
  - `SeedSelectionData.RemoveCropsWhere(Func<string,bool> remove) → bool` (true if anything was removed).
  - The Hazel tile is a UITK element named `hazel-tile` inside `seed-rail`.
  - `SeedSelectionPopup.OpenPlantsStall()` (public).

- [ ] **Step 1: Add `RemoveCropsWhere` to `SeedSelectionData`**

```csharp
    /// <summary>Clears every field whose crop matches <paramref name="remove"/> (e.g. crops the player
    /// no longer owns). Returns true if anything changed; caller saves.</summary>
    public bool RemoveCropsWhere(System.Func<string, bool> remove)
    {
        var doomed = new List<int>();
        foreach (var kvp in zoneAssignments) if (remove(kvp.Value)) doomed.Add(kvp.Key);
        foreach (int z in doomed) zoneAssignments.Remove(z);
        return doomed.Count > 0;
    }
```

- [ ] **Step 2: Clear unowned crops everywhere the selection is read**

In `SeedSelectionPopup.cs` add a helper:

```csharp
    // Old saves (and any crop no longer owned) must never plant an unowned crop.
    private SeedSelectionData LoadOwnedSelection()
    {
        SeedSelectionData data = SeedSelectionData.Load();
        if (cropDatabase != null && data.RemoveCropsWhere(name => !CropOwnership.IsOwned(cropDatabase.GetCropByName(name))))
            data.Save();
        return data;
    }
```

Replace each `SeedSelectionData.Load()` in `Show()`, `IsReadyToRun()`, `HasAnyCropEquipped()` and `LoadAndApplySavedSelections()` with `LoadOwnedSelection()`.

- [ ] **Step 3: Start a run with at least one field filled (one crop per field stays)**

In `SeedSelectionData.cs`, add:

```csharp
    /// <summary>A run may start once at least one field has a crop: with seed progression a farm can
    /// own fewer crops than it has fields, and those fields simply sit empty that run.</summary>
    public bool CanStartRun() => zoneAssignments.Count > 0;
```

In `SeedSelectionPopup.IsReadyToRun()`, return `LoadOwnedSelection().CanStartRun();` instead of `AreAllUnlockedZonesFilled()`. Leave `OnSeedTileClicked` and its `IsCropAssigned` guard unchanged (one crop per field).

Check the other callers of the old rule and switch them to "at least one":

```bash
grep -rn "AreAllUnlockedZonesFilled\|IsReadyToRun\|Not all zones" Assets/Scripts --include=*.cs
```

Expected callers: `RunManager.cs:103` (keep the call; it now means "at least one") and `SeedSelectionPopup.cs`. If `RunUI` shows a "fill all fields" warning, change its text to "Choose at least one crop".

Empty-field hint: in `RefreshFieldTile(int zone)`, when the zone is unlocked, has no crop, and every owned crop is already assigned elsewhere, set the crop slot's placeholder label (the one that normally reads "Tap a seed") to "More seeds at Hazel's". Find the label with `grep -n "Tap a seed" Assets/Scripts/SeedSelectionPopup.cs Assets/UI -r`.

- [ ] **Step 4: Build the rail from owned crops, plus the Hazel tile**

In `BuildSeedRail()`, change the loop source from `cropDatabase.allCrops` to `CropOwnership.Owned(cropDatabase)`. After the loop, before `RefreshSeedRailStates()`, add:

```csharp
        seedRail.Add(BuildHazelTile());
```

In `RefreshSeedRailStates()`, iterate `CropOwnership.Owned(cropDatabase)` so it lines up with the tiles. The Hazel tile is not in `seedRailTiles`.

Add:

```csharp
    // Trailing rail tile: where to get more seeds. Same size as a seed tile; a key icon, not an emoji.
    private VisualElement BuildHazelTile()
    {
        TemplateContainer tile = seedTileTemplate.Instantiate();
        tile.name = "hazel-tile";
        VisualElement img = tile.Q<VisualElement>("tile-image");
        Label label = tile.Q<Label>("tile-label");
        Button btn = tile.Q<Button>();
        if (img != null && keyIcon != null) img.style.backgroundImage = new StyleBackground(keyIcon);
        if (label != null) label.text = "More seeds";
        if (btn != null) btn.clicked += OpenPlantsStall;
        return tile;
    }

    /// <summary>Closes the picker and opens Hazel's stall at the Market.</summary>
    public void OpenPlantsStall()
    {
        Hide();
        var pan = FindFirstObjectByType<CameraPanController>();
        if (pan != null && pan.CurrentLocation != CameraPanController.Location.Market) pan.PanTo(CameraPanController.Location.Market);
        ShopPopupUITK.TryOpen(ShopPopupUITK.Section.Plants);
    }
```

Add the field `[SerializeField] private Sprite keyIcon;` at the top of the class. Assign it in the scene to the same key sprite `FieldTileTemplate` uses. Find it with `grep -rn "key" Assets/UI/SeedSelection* --include=*.uxml --include=*.uss -i`, or reuse `Assets/Sprites/UI/Icons/Icons_Essential/Key.png` if present (`ls Assets/Sprites/UI/Icons/Icons_Essential | grep -i key`). Set it in the Inspector, or with GladeKit `set_object_reference` on the SeedSelectionPopup GameObject.

Subscribe to rebuild when ownership changes: in `OnEnable` add `CropOwnership.OnOwnershipChanged += OnOwnershipChanged;`, in `OnDisable` add the matching `-=`, and add:

```csharp
    private void OnOwnershipChanged() { if (isOpen) BuildSeedRail(); }
```

- [ ] **Step 5: Recompile and play-verify on the dev save**

With the dev save (it owns no `seed_*` ids): enter play, open `SeedSelectionPopup` via `open_popup`, and take a screenshot.

Expected:
- The rail shows **Radish** and the **More seeds** tile only.
- Fields previously holding Carrot/Tomato are cleared.
- Tapping Radish fills Field 1.
- Field 2 (if bought) shows "More seeds at Hazel's".

Press Save, then `ui_click StartRunButton`. The run starts. Exit play and restore the save.

- [ ] **Step 6: Run the EditMode tests.** Expected: `RESULT: Passed`. **Checkpoint**, no commit.

---

### Task 5: Hazel's Plants stall

**Files:**
- Modify: `Assets/Scripts/UI/ShopPopupUITK.cs` (`Refresh`, `Open`, new crop-row path, emoji removal)
- Modify: the scene (FarmMain): the Plants `ShopPopupUITK` component's `headerTitle` → `Hazel's Seeds`; its `cropDatabase` field → `Assets/Data/Crops/CropDatabase.asset`. Use GladeKit `set_component_property` / `set_object_reference`.

**Interfaces:**
- Consumes: `CropOwnership.StateOf`, `CropOwnership.TryBuy`, `CropOwnership.OnOwnershipChanged` (Task 2); `SeedShopRules.MaskedHint` (Task 1); `AlmanacPopupUITK.InfoBadge(AlmanacKind, string, bool, float)`; `OnboardingTutorials.OnMenuOpened(string)`.
- Produces:
  - The Plants section lists all `allCrops` in order.
  - Row classes `market-row--owned`, `market-row--buy`, `market-row--cant-afford`, plus the new `market-row--masked`.
  - It fires `tip_seed_stall` on open.

- [ ] **Step 1: Add the crop source and the tip hook**

Add the field `[SerializeField] private CropDatabase cropDatabase;` under `[Header("Data")]`.

In `Open()`, after `Refresh();`:

```csharp
        if (section == Section.Plants) OnboardingTutorials.OnMenuOpened("tip_seed_stall"); // one-time how-to (new players)
```

Subscribe `CropOwnership.OnOwnershipChanged += OnOwnershipChanged;` in `TrySubscribeEvents` (unconditionally, since it's static), unsubscribe in `UnsubscribeEvents`, and add `private void OnOwnershipChanged() => MarkDirty();`.

- [ ] **Step 2: Build Plants rows from the CropDatabase**

At the top of `Refresh()`, after `rowsList.Clear();`:

```csharp
        if (section == Section.Plants && cropDatabase != null)
        {
            // All crops, always in allCrops order (the one fixed crop order) — never re-sorted by state.
            foreach (CropData crop in cropDatabase.allCrops)
                if (crop != null) SpawnCropRow(rowsList, crop);
            return;
        }
```

Add:

```csharp
    private void SpawnCropRow(VisualElement parent, CropData crop)
    {
        TemplateContainer rowContainer = rowTemplate.Instantiate();
        parent.Add(rowContainer);

        VisualElement rowRoot = rowContainer.Q(className: "market-row") ?? rowContainer.contentContainer;
        VisualElement iconImg = rowContainer.Q<VisualElement>("row-icon");
        Label iconFallback    = rowContainer.Q<Label>("row-icon-fallback");
        Label titleLabel      = rowContainer.Q<Label>("row-title");
        Label descLabel       = rowContainer.Q<Label>("row-desc");
        Label statusLabel     = rowContainer.Q<Label>("row-status");
        Label costLabel       = rowContainer.Q<Label>("row-cost");

        SeedState state = CropOwnership.StateOf(crop);
        bool masked = state == SeedState.Masked;

        if (iconFallback != null) iconFallback.style.display = DisplayStyle.None;
        if (iconImg != null)
        {
            iconImg.style.display = DisplayStyle.Flex;
            if (crop.seedPacketSprite != null) iconImg.style.backgroundImage = new StyleBackground(crop.seedPacketSprite);
            // Masked: a dark silhouette — you can tell there's a packet, not which one.
            iconImg.style.unityBackgroundImageTintColor = masked ? new Color(0f, 0f, 0f, 0.8f) : Color.white;
        }
        if (titleLabel != null) titleLabel.text = masked ? "???" : crop.cropName;

        rowRoot.RemoveFromClassList("market-row--owned");
        rowRoot.RemoveFromClassList("market-row--buy");
        rowRoot.RemoveFromClassList("market-row--cant-afford");
        rowRoot.RemoveFromClassList("market-row--masked");

        switch (state)
        {
            case SeedState.Owned:
                rowRoot.AddToClassList("market-row--owned");
                if (descLabel != null)   descLabel.text   = crop.description;
                if (statusLabel != null) statusLabel.text = "Owned";
                if (costLabel != null)   costLabel.text   = "";
                break;
            case SeedState.Masked:
                rowRoot.AddToClassList("market-row--masked");
                if (descLabel != null)   descLabel.text   = SeedShopRules.MaskedHint;
                if (statusLabel != null) statusLabel.text = "LOCKED";
                if (costLabel != null)   costLabel.text   = "";
                break;
            default:
                bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(crop.unlockCost);
                rowRoot.AddToClassList(canAfford ? "market-row--buy" : "market-row--cant-afford");
                if (descLabel != null)   descLabel.text   = crop.description;
                if (statusLabel != null) statusLabel.text = canAfford ? "BUY" : "LOCKED";
                if (costLabel != null)   costLabel.text   = FormatCoinCost(crop.unlockCost);
                if (canAfford)
                {
                    CropData captured = crop;
                    rowRoot.RegisterCallback<ClickEvent>(_ => CropOwnership.TryBuy(captured));
                    WirePressedFeedback(rowRoot, "market-row--pressed");
                }
                break;
        }

        // "What's this crop good at?" — owned and priced rows only; a masked row reveals nothing.
        if (!masked) rowRoot.Add(AlmanacPopupUITK.InfoBadge(AlmanacKind.Crop, crop.cropName, topLeft: true));
    }
```

- [ ] **Step 3: Remove the emoji from the existing equipment rows**

In `SpawnRow`, replace `"✓ Purchased"` with `"Purchased"` and `"🔒 LOCKED"` with `"LOCKED"` (see the no-emoji rule in Global Constraints).

- [ ] **Step 4: Style the masked row**

Find the row stylesheet with `grep -rln "market-row--cant-afford" Assets/UI`. Add next to the existing `--cant-afford` rule, reusing that rule's brown for the background:

```css
.market-row--masked { opacity: 0.75; }
.market-row--masked #row-title { color: rgb(90,68,38); -unity-font-style: bold; }
```

- [ ] **Step 5: Wire the scene fields, recompile, and play-verify**

1. Set the Plants `ShopPopupUITK` `cropDatabase` and `headerTitle = "Hazel's Seeds"` in FarmMain, and save the scene.
2. Back up the save and enter play on the dev save.
3. Pan to the Market (`ui_click` the Market pan button; find its name with `ui_pick`) and tap the Plants stall, or run `ShopPopupUITK.TryOpen|Plants`. Take a screenshot.

   Expected:
   - 9 rows in the fixed order, with Radish "Owned".
   - Every other crop priced. The dev save already has `composting_basics` and cannery research, so Corn and Strawberry show as priced here; the **masked** look is verified on a fresh farm in Task 11.
4. Buy Carrot: Coins drop by 10 and the row shows "Owned".
5. Open `SeedSelectionPopup`: the rail shows Radish, Carrot, More seeds.
6. Exit play and restore the save.

- [ ] **Step 6: Run the EditMode tests.** Expected: `RESULT: Passed`. **Checkpoint.**

---

### Task 6: Almanac crop states and pest pages

**Files:**
- Modify: `Assets/Scripts/Almanac/AlmanacCatalog.cs:79-108` (`CropEntry`), `:236` (pest page crop list)
- Modify: `Assets/Scripts/UI/AlmanacPopupUITK.cs:94-99` (`OpenEntry`)

**Interfaces:**
- Consumes: `CropOwnership.StateOf` (Task 2); `SeedShopRules.MaskedHint`; `CropTraits.Tags(..., compostMultiplier)` (Task 1).
- Produces:
  - Crop entries with `unlocked=false` + `unlockHint = SeedShopRules.MaskedHint` when Masked.
  - A "Sold at Hazel's stall - N Coins" note first when Priced.

- [ ] **Step 1: Crop page states**

In `CropEntry(CropData c)`, after constructing `e`:

```csharp
        SeedState state = CropOwnership.StateOf(c);
        if (state == SeedState.Masked)
        {
            e.unlocked = false;                    // list shows "???" + silhouette; page can't open
            e.unlockHint = SeedShopRules.MaskedHint;
        }
```

Pass the compost multiplier to the tags:

```csharp
        e.tags.AddRange(CropTraits.Tags(c.TotalGrowthTime, c.maxHP, c.canRegrow, c.canBeCanned,
            c.moistureDepletionRate, c.deerAppetite, c.crowAppetite, c.compostMultiplier));
```

Before the existing `if (c.canRegrow)` notes, add:

```csharp
        if (state == SeedState.Priced)
            e.notes.Add($"Sold at Hazel's stall - {c.unlockCost:N0} Coins.");
        if (c.compostMultiplier > 1f)
            e.notes.Add($"Makes {c.compostMultiplier:0.#}x compost at the Compost Bay when a plant is lost.");
```

- [ ] **Step 2: Pest pages never name a masked crop**

At line 236, change the source:

```csharp
        foreach (CropData c in Crops().Where(c => CropOwnership.StateOf(c) != SeedState.Masked)
                                      .OrderByDescending(c => c.PestAppetite(p.threatType)))
```

The appetite ordering here is a ranking inside a pest's page, and it's the same for every player. It is not a crop-list order.

- [ ] **Step 3: `OpenEntry` ignores locked entries**

```csharp
    public void OpenEntry(AlmanacKind kind, string id)
    {
        Open(kind);
        AlmanacEntry entry = AlmanacCatalog.Find(kind, id);
        if (entry != null && entry.unlocked) ShowEntry(entry); // a masked crop never opens its page
    }
```

- [ ] **Step 4: Recompile and play-verify.** Open `AlmanacPopupUITK` on the dev save.

Expected:
- The Crops tab lists 9 crops in the fixed order.
- Carrot's page shows "Sold at Hazel's stall - 10 Coins." first in its notes.
- Corn shows the "Great compost" tag and the 2x compost note.

Exit and restore.

- [ ] **Step 5: Run the EditMode tests.** Expected: `RESULT: Passed`. **Checkpoint.**

---

### Task 7: Compost multiplier

**Files:**
- Modify: `Assets/Scripts/Plant.cs:449` (event signature) and `:463` (invoke)
- Modify: `Assets/Scripts/CompostBay.cs:28-36`

**Interfaces:**
- Consumes: `CropData.compostMultiplier` (Task 2).
- Produces: `Plant.OnPlantDied` becomes `event Action<int, CropData, Vector3>` (zoneId, crop, worldPos). `CompostBay` is its only subscriber (`grep -rn OnPlantDied Assets/Scripts`).

- [ ] **Step 1: Pass the crop, not just its tier**

`Plant.cs:449`:

```csharp
    public static event System.Action<int, CropData, Vector3> OnPlantDied;
```

`Plant.cs:463`:

```csharp
            OnPlantDied?.Invoke(parentTile.ZoneID, cropData, transform.position);
```

- [ ] **Step 2: Apply the multiplier in `CompostBay`**

Change the handler signature and the yield line:

```csharp
    private void HandlePlantDied(int zoneID, CropData crop, Vector3 worldPos)
```

Inside, use `int cropTier = crop != null ? crop.tier : 1;` where the old parameter was used, and:

```csharp
        float cropMult = crop != null && crop.compostMultiplier > 0f ? crop.compostMultiplier : 1f;
        int yield = Mathf.Max(1, Mathf.RoundToInt(cropTier * conversion * FarmUpgrades.CompostMultiplier * cropMult));
```

- [ ] **Step 3: Recompile and play-verify**

1. On the dev save (it has a Compost Bay), start a run with Corn in the field that has the Compost Bay equipped.
2. Buy Corn first with `CropOwnership`: open the stall and tap Corn, or run `CurrencyManager.AddCoins|200` then buy.
3. Let a plant dry out, and watch the floating "+N compost".

Expected: a Corn loss yields twice what the same field gives for a Radish loss (compare both in one run by planting Radish in a second field that also has a Compost Bay, or across two runs).

- [ ] **Step 4: Run the EditMode tests.** **Checkpoint.**

---

### Task 8: Town board — owned crops + Welcome basket

**Files:**
- Modify: `Assets/Scripts/EconomyCore/ReputationCore.cs:18-24` (`DeliveryRequest`)
- Modify: `Assets/Scripts/Reputation/ReputationManager.cs` (`Update`, `TryFulfill`, `TrySkip`, `IsCropUnlocked`, the `AnyUnlockedCrop` case)
- Modify: `Assets/Scripts/UI/TownRequestsPopupUITK.cs:751-752` (crop list), plus hiding the skip button for the basket
- Test: `Assets/Tests/EditMode/SeedLadderTests.cs` (reward test)

**Interfaces:**
- Consumes: `CropOwnership.Owned(CropDatabase)`, `CropOwnership.IsOwned` (Task 2); `ReputationMath.PointCost(int)`; `NarrativeManager.HasFired/MarkFired`.
- Produces:
  - `DeliveryRequest.isWelcomeBasket` (bool).
  - `ReputationManager.WelcomeBasketFlag = "first_request_done"`.
  - `ReputationManager.BoardIntroLetterFlag = "letter:town_board_intro"`.
  - `ReputationMath.WelcomeBasketReward(int barProgress, int pointsEarned) → int`.

- [ ] **Step 1: Write the failing test**

Add to `SeedLadderTests`:

```csharp
    [TestCase(0, 0, 100)]   // fresh bar: exactly the first point (85 + 15*1)
    [TestCase(40, 0, 60)]   // partially filled bar: tops it up to the point
    [TestCase(0, 1, 115)]   // (edge) a point already earned: fills the next one
    public void WelcomeBasketReward_FillsExactlyOnePoint(int bar, int points, int expected)
    {
        Assert.AreEqual(expected, ReputationMath.WelcomeBasketReward(bar, points));
    }
```

- [ ] **Step 2: Run the tests and verify they fail** (compile error: `WelcomeBasketReward` missing).

- [ ] **Step 3: Implement**

In `Assets/Scripts/EconomyCore/ReputationMath.cs`:

```csharp
    /// <summary>Rep that exactly completes the next Barn point — the Welcome basket's reward, so a
    /// new player's first delivery always ends with a point to spend.</summary>
    public static int WelcomeBasketReward(int barProgress, int pointsEarned) =>
        Mathf.Max(1, PointCost(pointsEarned + 1) - Mathf.Max(0, barProgress));
```

In `DeliveryRequest` (ReputationCore.cs), add `public bool isWelcomeBasket;`.

In `ReputationManager.cs`, add:

```csharp
    public const string BoardIntroLetterFlag = "letter:town_board_intro";
    public const string WelcomeBasketFlag = "first_request_done";

    // A new player's first request after the board letter: 8 Radish (the one crop every farm owns),
    // from the Mayor, worth exactly one Barn point. Pinned to slot 0 until delivered; can't be skipped.
    private void EnsureWelcomeBasket()
    {
        var nm = NarrativeManager.Instance;
        if (nm == null || !nm.HasFired(BoardIntroLetterFlag) || nm.HasFired(WelcomeBasketFlag)) return;
        DeliveryRequest current = core.GetSlotRequest(0);
        if (current != null && current.isWelcomeBasket) return;
        core.SetSlotRequest(0, new DeliveryRequest
        {
            items = new[] { new DeliveryLineItem { itemId = "Radish", count = 8 } },
            repReward = ReputationMath.WelcomeBasketReward(core.BarProgress, core.PointsEarned),
            requesterName = "Mayor Bramble",
            flavorText = "A welcome basket for the new families in town. Radishes, if you can spare them!",
            isWelcomeBasket = true,
        });
        OnChanged?.Invoke();
    }
```

Call `EnsureWelcomeBasket();` as the first line of `Update()`.

In `TryFulfill`, after `core.OnFulfilled(...)`:

```csharp
        if (request.isWelcomeBasket) NarrativeManager.Instance?.MarkFired(WelcomeBasketFlag);
```

In `TrySkip`, after the null check:

```csharp
        if (request.isWelcomeBasket) return false; // the tutorial request can't be skipped
```

Replace `IsCropUnlocked`:

```csharp
    private bool IsCropUnlocked(string cropName)
    {
        CropData crop = cropDatabase != null ? cropDatabase.GetCropByName(cropName) : null;
        return crop != null && CropOwnership.IsOwned(crop);
    }
```

In the `AnyUnlockedCrop` case, replace `foreach (CropData crop in cropDatabase.startingCrops)` with `foreach (CropData crop in CropOwnership.Owned(cropDatabase))`.

- [ ] **Step 4: Town Requests popup**

At `TownRequestsPopupUITK.cs:751-752`, replace the `startingCrops` loop with `foreach (CropData crop in CropOwnership.Owned(cropDatabase))`.

Find the skip button construction (`grep -n "Skip" Assets/Scripts/UI/TownRequestsPopupUITK.cs`). Hide it when `request.isWelcomeBasket`:

```csharp
        if (skipButton != null) skipButton.style.display = request.isWelcomeBasket ? DisplayStyle.None : DisplayStyle.Flex;
```

Use the local variable names found at that site.

- [ ] **Step 5: Run the tests.** Expected: `RESULT: Passed`, with 3 new cases. **Checkpoint.**

---

### Task 9: Narrative — Hazel, letters, tips, the Plants CTA, copy doc

**Files:**
- Modify: `Assets/Scripts/EconomyCore/LetterContent.cs:9` (append `OpenPlantsShop`)
- Modify: `Assets/Scripts/EconomyCore/NarrativeDefaults.cs` (cast, letters, tips, updated copy)
- Modify: `Assets/Scripts/UI/InboxPopupUITK.cs` (`OnCta`, `CtaLabel`)
- Modify: `Assets/Resources/LetterCatalog.asset`, via the Seed Missing Copy menu + `asset_string`
- Modify: `docs/narrative/cast-and-copy.md`, via the Write Copy Reference menu
- Test: `Assets/Tests/EditMode/NarrativeDefaultsTests.cs`

**Interfaces:**
- Consumes: `SeedSelectionPopup.OpenPlantsStall()` (Task 4).
- Produces:
  - Letter ids `seed_stall_intro`, `regrow_bought`, `town_board_intro`.
  - Tip ids `tip_seed_stall`, `tip_regrow`, `tip_barn_spend`.
  - `NarrativeDefaults.Hazel = "Hazel"`.
  - `CtaKind.OpenPlantsShop`.

- [ ] **Step 1: Write the failing tests**

Add to `NarrativeDefaultsTests`:

```csharp
    [TestCase("seed_stall_intro", "run_ended:1")]
    [TestCase("regrow_bought", "seed_bought:regrow")]
    [TestCase("town_board_intro", "run_ended:4")]
    public void SeedProgressionLetters_Exist_WithTriggers(string id, string trigger)
    {
        var letter = NarrativeDefaults.Letters.FirstOrDefault(l => l.id == id);
        Assert.IsNotNull(letter, id);
        Assert.AreEqual(trigger, letter.triggerEvent);
        Assert.IsTrue(letter.newPlayersOnly);
        Assert.IsTrue(NarrativeDefaults.Cast.Any(c => c.displayName == letter.senderName), letter.senderName);
    }

    [TestCase("tip_seed_stall")]
    [TestCase("tip_regrow")]
    [TestCase("tip_barn_spend")]
    public void SeedProgressionTips_Exist(string id)
    {
        Assert.IsTrue(NarrativeDefaults.Tips.Any(t => t.id == id && !string.IsNullOrWhiteSpace(t.text)), id);
    }

    [Test]
    public void NoCopy_UsesTheWordGold()
    {
        foreach (var l in NarrativeDefaults.Letters) StringAssert.DoesNotContain(" Gold", l.body, l.id);
        foreach (var t in NarrativeDefaults.Tips) StringAssert.DoesNotContain(" Gold", t.text, t.id);
    }
```

It needs `using System.Linq;`. Add it if the file lacks it.

- [ ] **Step 2: Run the tests and verify they fail** (the new letters and tips are missing).

- [ ] **Step 3: Add the CTA kind**

Append it last in `LetterContent.cs` (append-only, because values are serialized ints):

```csharp
public enum CtaKind { None, OpenEquipment, OpenResearch, OpenShop, OpenFarmUpgrades, OpenCarpenter, OpenBarn, OpenAnimals, OpenFieldPicker, OpenMarket, OpenTownRequests, OpenPlantsShop }
```

In `InboxPopupUITK.OnCta`, add:

```csharp
            case CtaKind.OpenPlantsShop:   Close(); PanTo(CameraPanController.Location.Market); ShopPopupUITK.TryOpen(ShopPopupUITK.Section.Plants); break;
```

In `CtaLabel`, add:

```csharp
            case CtaKind.OpenPlantsShop:   return "Visit Hazel's Stall";
```

- [ ] **Step 4: Add Hazel, the letters and the tips to `NarrativeDefaults`**

Add the constant `public const string Hazel = "Hazel";`. Add to `Cast`, after Marta:

```csharp
        new CastMember { id = "hazel", displayName = Hazel,
            role = "Runs the seed stall at the Market. Sells every crop's seed packet.",
            voice = "Warm, practical, loves a bargain and a good tip about what grows well. Opens \"Hi {farmName}!\" and signs \"- Hazel\"." },
```

Add to `Letters`:

```csharp
        new LetterDef { id = "seed_stall_intro", triggerEvent = "run_ended:1", newPlayersOnly = true,
            senderName = Hazel, subject = "Fresh seeds at my stall",
            body = "Hi {farmName}!\n\nHazel here - I run the seed stall at the Market. Saw your radishes come up. Lovely work!\n\nWhen you're ready to try something new, come see me. Carrots are next to nothing, and they sell for nearly twice as much.\n\nOnce you buy a packet, that crop is yours to plant every run after.\n\n- Hazel",
            ctaKind = CtaKind.OpenPlantsShop },

        new LetterDef { id = "regrow_bought", triggerEvent = "seed_bought:regrow", newPlayersOnly = true,
            senderName = Hazel, subject = "These ones keep giving",
            body = "Hi {farmName}!\n\nGood choice! That one's a regrower. Pick it, and as long as the plant survives, it grows back and gives you another harvest - no new seed needed.\n\nKeep it watered and keep the pests off, and it'll pay for itself many times over.\n\n- Hazel" },

        new LetterDef { id = "town_board_intro", triggerEvent = "run_ended:4", newPlayersOnly = true,
            senderName = Mayor, subject = "The town could use a hand",
            body = "Dear {farmName},\n\nWord's getting around about your farm! A few of us in town could use a hand now and then, so we post what we need on the community board by the Market.\n\nHelp out, and folks won't forget it. Every delivery earns you Reputation, and a good name in this valley opens doors - your Barn will show you how.\n\nI've pinned the first request myself.\n\n- Mayor Bramble",
            ctaKind = CtaKind.OpenTownRequests },
```

Add to `Tips`, with the other `tip_*` entries:

```csharp
        new TipDef { id = "tip_seed_stall", when = "First time Hazel's seed stall opens",
            text = "Buy a seed packet once and that crop is yours to plant every run. The mystery packets are still waiting to be discovered!" },
        new TipDef { id = "tip_regrow", when = "First regrowing crop harvested in a run",
            text = "This crop regrows! As long as the plant survives after it's picked, it grows more - no new seed needed." },
        new TipDef { id = "tip_barn_spend", when = "Barn opened with a skill point to spend (points at a + button)",
            text = "You have a point to spend! Tap + to level up a skill. Each level makes your farm a little better at that job." },
```

(`TipDef` fields are `id`, `text`, `when`.)

Update these existing defaults in place:

- `tip_collect_sell` text:

  ```
  Sell turns each harvest into Money right away, to keep your run going. Collect keeps harvests as items, for town requests and your jars.
  ```

- `tip_collect_sell` when: `First run after the town-board letter (points at the Collect / Sell switch)`.
- `tip_town_requests` text:

  ```
  Townsfolk post what they need here. Deliveries earn Reputation, and a full bar gives you a point to spend at your Barn. Switch to Collect during a run to keep harvests for requests.
  ```

- `cannery_unlock` body: insert before `Save me a jar of strawberry?`:

  ```
  Hazel's got strawberry seeds in now, too - they make the quickest jam there is!\n\n
  ```

- [ ] **Step 5: Push the copy into the asset and regenerate the doc**

1. Recompile. Run `echo "Farm Game/Narrative/Seed Missing Copy" > Temp/menu.request`. This appends the 3 letters, 3 tips and the Hazel cast member, and never overwrites.
2. Update the **existing** asset entries whose copy changed with `asset_string`. Find each index with:

   ```bash
   python - <<'EOF'
   import re
   s = open('Assets/Resources/LetterCatalog.asset', encoding='utf-8').read()
   # Section order in the asset is letters:, cast:, tips: (grep -n "^  [a-z]*:$" to confirm).
   letters = s[s.index('  letters:'):s.index('  cast:')]
   tips = s[s.index('  tips:'):]
   for name, block in (('letters', letters), ('tips', tips)):
       print(name, {k: n for n, k in enumerate(re.findall(r'\n  - id: (\S+)', block))})
   EOF
   ```

   Then write one request per field, waiting about 2 seconds between them:
   - `tips.Array.data[<tip_collect_sell>].text`
   - `tips.Array.data[<tip_collect_sell>].when`
   - `tips.Array.data[<tip_town_requests>].text`
   - `letters.Array.data[<cannery_unlock>].body`

   Values are exactly the strings from Step 4, with `\n` as real newlines.
3. Run `echo "Farm Game/Narrative/Write Copy Reference" > Temp/menu.request` and check that `docs/narrative/cast-and-copy.md` now lists Hazel, the 3 letters and the 3 tips.

- [ ] **Step 6: Run the tests.** Expected: `RESULT: Passed`. **Checkpoint.**

---

### Task 10: Tutorial hooks — regrow, Collect/Sell timing, Barn spend

**Files:**
- Modify: `Assets/Scripts/Tutorial/OnboardingTutorials.cs` (`OnRunStarted`, `OnSequenceCompleted`, new `OnFirstRegrow`, `OnBarnOpened`)
- Modify: `Assets/Scripts/Plant.cs` (`StartRegrowth`)
- Modify: `Assets/Scripts/UI/BarnPopupUITK.cs` (`Open`, `+` button creation around line 645)

**Interfaces:**
- Consumes:
  - `TutorialTargets.FromUITK(VisualElement)`.
  - `TutorialStep` with `cardAtBottom`, `dimOpacity`.
  - `ReputationManager.UnspentPoints`.
  - `ReputationManager.BoardIntroLetterFlag` (Task 8).
  - Tip ids from Task 9.
- Produces:
  - `OnboardingTutorials.OnFirstRegrow()`.
  - `OnboardingTutorials.OnBarnOpened()`.
  - `BarnPopupUITK.FirstSpendableButton` (`VisualElement`, may be null).

- [ ] **Step 1: Regrow tip**

In `OnboardingTutorials.cs`:

```csharp
    /// <summary>Plant.StartRegrowth: a regrowing crop was just picked and is growing back.</summary>
    public static void OnFirstRegrow()
    {
        if (!IsNewPlayer) return;
        Tip("tip_regrow");
    }
```

In `Plant.StartRegrowth()`, as its first line:

```csharp
        OnboardingTutorials.OnFirstRegrow(); // one-time "it grows back" teach (new players)
```

- [ ] **Step 2: Move the Collect/Sell spotlight to after the board letter**

In `OnRunStarted()`, replace the `RunsCompleted >= 1` line with:

```csharp
        // After the Mayor's town-board letter, point out the Collect / Sell switch (requests need items).
        if (NarrativeManager.Instance != null && NarrativeManager.Instance.HasFired(ReputationManager.BoardIntroLetterFlag))
            TryCollectSellTip();
```

- [ ] **Step 3: Barn spend tip**

In `BarnPopupUITK`, add `public VisualElement FirstSpendableButton { get; private set; }`. Set it to `null` at the start of `BuildContent()`. Where `plusBtn` is created, after it's built, add:

```csharp
        if (canLevel && FirstSpendableButton == null) FirstSpendableButton = plusBtn;
```

In `Open()`, after the existing `OnboardingTutorials.OnMenuOpened("tip_barn");` line, add `OnboardingTutorials.OnBarnOpened();`.

In `OnboardingTutorials.cs`:

```csharp
    /// <summary>BarnPopupUITK.Open: with a point to spend, spotlight a "+" (after the Barn how-to).</summary>
    public static void OnBarnOpened()
    {
        if (!IsNewPlayer || !Done("tip_barn")) return; // the how-to card shows first; see OnSequenceCompleted
        TryBarnSpendTip();
    }

    private static void TryBarnSpendTip()
    {
        if (Done("tip_barn_spend")) return;
        if (ReputationManager.Instance == null || ReputationManager.Instance.UnspentPoints <= 0) return;
        VisualElement plus = BarnPopupUITK.Instance != null ? BarnPopupUITK.Instance.FirstSpendableButton : null;
        if (plus == null) return;
        Start("tip_barn_spend", new TutorialStep
        {
            text = Text("tip_barn_spend"),
            advance = TutorialAdvance.TapAnywhere,
            getTargetScreenRect = TutorialTargets.FromUITK(plus),
            dimOpacity = MenuTipDim,
        });
    }
```

In `OnSequenceCompleted`'s switch, add:

```csharp
            case "tip_barn":
                if (BarnPopupUITK.Instance != null && BarnPopupUITK.Instance.IsOpen) TryBarnSpendTip();
                break;
```

`OnboardingTutorials.cs` needs `using UnityEngine.UIElements;` for `VisualElement`.

- [ ] **Step 4: Recompile and play-verify on the dev save with replay armed**

1. Back up the save and enter play.
2. Run `OnboardingTutorials.DevReplayAll`, then `ReputationManager.DevAddPoints|1`.
3. Open `BarnPopupUITK`. The `tip_barn` card shows at the bottom. Dismiss it with `ui_press "540,1200"`.
4. Expected: the `tip_barn_spend` spotlight on the Harvesting "+" button. Take a screenshot.
5. Run `NarrativeDirector.Raise|run_ended:4`, then `ui_click StartRunButton` after saving seeds. Expected: the Collect/Sell spotlight at run start.
6. Exit and restore.

- [ ] **Step 5: Run the tests.** Expected: `RESULT: Passed`. **Checkpoint.**

---

### Task 11: Starter check + full fresh-farm verification + final tuning

**Files:**
- Possibly modify: `Assets/Scripts/EconomyCore/SeedLadder.cs` (swap Radish/Carrot, or adjust `coinValue`), then re-run the Task 3 menu item.
- Modify: `C:\Users\rjcla\.claude\projects\C--Users-rjcla-IdleFarm---Silo\memory\` (new `project_seed_progression.md` + a `MEMORY.md` index line).

**Interfaces:**
- Consumes: everything above.
- Produces: the verified feature, plus a short report for the user.

- [ ] **Step 1: Starter check — real runs**

1. Back up the save.
2. Make a fresh-farm save: rename `gamedata.json` away, and delete the `runs_completed_h*` PlayerPrefs value.
3. Enter play and name the farm.
4. Play run 1 with **Radish** in Field 1 to the end (bankrupt or `RunManager.EndRun`). Note from the Run Complete panel: farm time, harvests, Money earned, Coins banked.
5. Buy Carrot (10 Coins).
6. Play run 2 with **Carrot** only, and note the same numbers.

Decision rule:
- Carrot's run should earn **more Coins than Radish's**, and last as long or longer: "feels different, slightly more money".
- If Carrot earns fewer Coins, raise Carrot's `coinValue` in `SeedLadder` (e.g. 2 → 3) and re-run `Apply Seed Progression`.
- If Radish's first run is so short it feels bad (under about 5 minutes), swap Radish and Carrot in `SeedLadder` (starter flag, costs and coin values move with the row). Then fix the order test's `Expected` array, the welcome letter body (radish → carrot) and the Welcome basket item (`"Radish"` → `"Carrot"`).

- [ ] **Step 2: Fresh-farm chain on the same fresh save**

Check each step with a screenshot:

1. The seed menu shows only Radish + More seeds.
2. Run 1 ends: Hazel's `seed_stall_intro` letter arrives, and its CTA opens the stall.
3. The stall:
   - 9 rows in order, with `tip_seed_stall` at the bottom.
   - Radish Owned; Carrot and the price-only packets priced.
   - **Corn and Strawberry masked** (silhouette, "???", "Discovered through research", no "i").
4. Almanac Crops tab: Corn and Strawberry show "???" and their tiles don't open.
5. Buy Green Beans (use `CurrencyManager.AddCoins|200` if short). The `regrow_bought` letter arrives. In a run, the first regrowing harvest shows `tip_regrow`.
6. Run `NarrativeDirector.Raise|run_ended:4` (or play to run 4). The `town_board_intro` letter's CTA opens the board.
   - Slot 0 is the Welcome basket (8 Radish, no skip button, reward fills the bar exactly).
   - Next run start: the Collect/Sell spotlight.
   - Collect 8 Radish and deliver: "The town is grateful" arrives, and the Barn shows 1 point.
   - Barn: `tip_barn` then `tip_barn_spend` on a "+".
7. Research-gated reveal: run `ResearchManager` dev unlock for `composting_basics` if available (`grep -n "public .*Unlock" Assets/Scripts/ResearchManager.cs`). Otherwise skip, since it's covered by `SeedShopRules` tests. Corn turns priced in the stall and its Almanac tile opens.

- [ ] **Step 3: Existing-save check**

Restore the dev save and enter play. Expected:
- The seed menu offers Radish only.
- Coins and upgrades are unchanged.
- Buying Carrot works during a mid-run Market trip.

Exit, restore the save, and delete the test PlayerPrefs values.

- [ ] **Step 4: Full test suite.** Expected: `RESULT: Passed`, 0 failed.

- [ ] **Step 5: Memory**

Write `project_seed_progression.md` (type: project) covering:
- The ladder.
- `seed_<slug>` ids (no migration).
- The fixed order and its guard test.
- The shared-crops-across-fields change.
- The Welcome basket flags.
- The Apply Seed Progression tool.
- The starter-check result.

Add one line to `MEMORY.md` under the feature index.

- [ ] **Step 6: Report to the user.** Say what was built, what the starter check found, any tuning changes, and what's still open (the offline sim ignores regrow and traits). Don't commit.
