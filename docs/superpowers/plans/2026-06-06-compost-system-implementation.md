# Compost System Implementation Plan (Plan 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Compost economy that accelerates Research: a global Compost currency, a per-zone Compost Bay equipment that converts dead crops, a Cow animal that generates compost (passive + run-eating), boost tokens that pay compost to multiply a research slot's speed, and Market gating of the Compost Bay behind the "Composting Basics" binary research.

**Scope:** This is the second of two plans for the Research/Compost design at `docs/superpowers/specs/2026-06-06-research-catalog-design.md`. Plan 1 (Research V1) shipped 38 catalog researches + popup + consumer wiring + the `Composting Basics` binary research itself. Plan 2 wires up everything that depends on Compost: currency, Bay equipment, Cow, boost UI, Market gating.

**Architecture:**
- **Currency:** new field on `CurrencyManager` (no separate manager — matches Gems pattern). Persisted in `GameData.json`.
- **Compost Bay:** new `EquipmentData` asset + new `CompostBay.cs` component. Listens for a global `Plant.OnPlantDied` event and credits compost based on the dying crop's tier × Conversion Efficiency × research bonus, scoped to its zone.
- **Cow:** new `Cow.cs` component, spawned on the Cow's `AnimalVisual` prefab. Passive idle generates compost based on real-world `UtcNow` elapsed since `lastCompostTickUtc` (works offline). During a run, Cow walks to random mature crops and eats them — granting a per-eat compost lump scaled by `cow_run_yield`, and removing the crop (player tradeoff: more compost vs less harvest).
- **Boost tokens:** added to `ResearchSlotState` (the fields already exist from Plan 1's forward-compat). A new `CompostBoostModalUITK` shows a fixed pricing table (`{2x,3x,4x} × {4h, 12h}` = 6 options). The Boost button on each active slot card opens the modal.
- **Market gating:** `UnlockData` gets a new `requiredFeatureFlag` field. `ShopPopupUITK.RebuildRows` filters out unlocks whose required flag isn't set. Compost Bay's unlock asset requires `composting_basics`.

**Tech Stack:** Unity 2D (URP, C#), Unity UI Toolkit (modal), JsonUtility save, existing `EquipmentManager` / `AnimalManager` patterns from Plan 1.

---

## File Structure

| Path | Role |
|---|---|
| `Assets/Scripts/CurrencyManager.cs` *(modify)* | add `currentCompost`, `OnCompostChanged`, `AddCompost`, `SpendCompost`, `CanAffordCompost`, `Compost`, `SetCompost` |
| `Assets/Scripts/GameData.cs` *(modify)* | add `compost` field + constructor param |
| `Assets/Scripts/SaveManager.cs` *(modify)* | save/load compost |
| `Assets/Scripts/CurrencyUI.cs` *(modify)* | add compost row to top currency bar |
| `Assets/Scripts/Research/StatKey.cs` *(modify)* | add `CowPassiveCompost`, `CowRunYield`, `CompostBayConversion` constants |
| `Assets/Editor/ResearchCatalogGenerator.cs` *(modify)* | add 3 new SO entries; regenerate |
| `Assets/Scripts/Plant.cs` *(modify)* | fire static `OnPlantDied(zoneID, cropTier)` from death paths only (NOT harvest) |
| `Assets/Scripts/UnlockData.cs` *(modify)* | add `requiredFeatureFlag` field |
| `Assets/Scripts/UI/ShopPopupUITK.cs` *(modify)* | filter rows by `requiredFeatureFlag` via `ResearchManager.IsFeatureUnlocked` |
| `Assets/Data/Unlocks/CompostBay_Unlock.asset` *(new — via editor menu)* | Market unlock entry; `requiredFeatureFlag = "composting_basics"` |
| `Assets/Data/Equipment/CompostBay.asset` *(new — via editor menu)* | EquipmentData for the Bay |
| `Assets/Data/Equipment/EquipmentRegistry.asset` *(modify)* | append CompostBay entry |
| `Assets/Scripts/CompostBay.cs` *(new)* | per-zone behavior: subscribes to `Plant.OnPlantDied`, credits compost |
| `Assets/Editor/CompostBaySetupGenerator.cs` *(new)* | one-shot menu to create CompostBay_Unlock and CompostBay.asset |
| `Assets/Scripts/Cow.cs` *(new)* | passive idle compost (UTC-based) + during-run eating |
| `Assets/Scripts/AnimalManager.cs` *(modify)* | run Cow's passive accumulator alongside the egg/gem timer |
| `Assets/Scripts/AnimalData.cs` *(modify)* | add `compostPerMinute` field (Cow uses it; others leave 0) |
| `Assets/Scripts/ResearchManager.cs` *(modify)* | add `TryApplyBoost(slotIndex, multiplier, durationSecs, compostCost)` |
| `Assets/UI/CompostBoostModalUITK/CompostBoostModalUITK.uxml` *(new)* | modal layout |
| `Assets/UI/CompostBoostModalUITK/CompostBoostModalUITK.uss` *(new)* | modal styles |
| `Assets/Scripts/UI/CompostBoostModalUITK.cs` *(new)* | modal controller |
| `Assets/Scripts/UI/ResearchPopupUITK.cs` *(modify)* | add Boost button to active slot card; opens modal |

---

## Task 1: Compost currency on CurrencyManager

**Files:**
- Modify: `Assets/Scripts/CurrencyManager.cs`

- [ ] **Step 1.1: Add the field, event, accessor**

After the existing currency declarations and events (near line 18–32), add:

```csharp
[SerializeField] private int currentCompost = 0;
public event Action<int> OnCompostChanged;
public int Compost => currentCompost;
```

- [ ] **Step 1.2: Add the mutator methods**

At the bottom of the class (after `SetGems`), append:

```csharp
public void AddCompost(int amount)
{
    if (amount <= 0) return;
    currentCompost += amount;
    OnCompostChanged?.Invoke(currentCompost);
}

public bool SpendCompost(int amount)
{
    if (amount <= 0) return true;
    if (currentCompost < amount) return false;
    currentCompost -= amount;
    OnCompostChanged?.Invoke(currentCompost);
    return true;
}

public bool CanAffordCompost(int amount) => currentCompost >= amount;

public void SetCompost(int amount)
{
    currentCompost = Mathf.Max(0, amount);
    OnCompostChanged?.Invoke(currentCompost);
}
```

- [ ] **Step 1.3: Commit**

```bash
git add Assets/Scripts/CurrencyManager.cs
git commit -m "feat(compost): add Compost currency to CurrencyManager"
```

---

## Task 2: GameData + SaveManager wire compost

**Files:**
- Modify: `Assets/Scripts/GameData.cs`
- Modify: `Assets/Scripts/SaveManager.cs`

- [ ] **Step 2.1: Add compost field to GameData**

Find the field declarations in `GameData.cs` (the existing `public int coins; public int gems;` lines). Insert below `gems`:

```csharp
public int compost;
```

Add to both constructors:
- In the default constructor (`public GameData()`), add `compost = 0;`
- In the parameterized constructor signature, add `int currentCompost` as a new parameter immediately after `currentGems`; add `compost = currentCompost;` in the body.

- [ ] **Step 2.2: Update SaveManager.SaveGame to pass compost**

In `SaveManager.SaveGame()`, find the `new GameData(...)` call. Add `CurrencyManager.Instance.Compost` as the third argument (right after `Gems`).

- [ ] **Step 2.3: Update SaveManager.LoadGame to apply compost**

In `LoadGame()`, find the `CurrencyManager.Instance.SetGems(data.gems);` line. Append immediately after:

```csharp
CurrencyManager.Instance.SetCompost(data.compost);
```

- [ ] **Step 2.4: Verify compile**

```bash
git status
```

Then in Unity, check Console for compile errors. There should be none.

- [ ] **Step 2.5: Commit**

```bash
git add Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs
git commit -m "feat(compost): persist compost in GameData JSON"
```

---

## Task 3: Compost in top currency bar

**Files:**
- Modify: `Assets/Scripts/CurrencyUI.cs`

- [ ] **Step 3.1: Inspect existing CurrencyUI structure**

Read the file. Identify the pattern used to display Coins / Gems (likely a `Text` or `TextMeshProUGUI` reference + a value listener subscribed to `OnCoinsChanged` / `OnGemsChanged`).

- [ ] **Step 3.2: Add compost label + subscriber**

Following the same pattern, add a `[SerializeField]` field for the compost label (e.g. `TMPro.TextMeshProUGUI compostLabel`). In `OnEnable` (or wherever Coins/Gems events are wired), add:

```csharp
if (CurrencyManager.Instance != null)
    CurrencyManager.Instance.OnCompostChanged += OnCompostChanged;
```

In `OnDisable`, unsubscribe. Add the handler:

```csharp
private void OnCompostChanged(int amount)
{
    if (compostLabel != null) compostLabel.text = amount.ToString();
}
```

In `Start` (or wherever the initial values are set), call `OnCompostChanged(CurrencyManager.Instance.Compost)`.

- [ ] **Step 3.3: Wire the UI element in Inspector**

This step requires Unity. After scripts compile, find the CurrencyUI GameObject in the scene. Add a new `TextMeshProUGUI` child (clone Coins/Gems display) showing "🌱 0". Drag it into `compostLabel`.

- [ ] **Step 3.4: Commit**

```bash
git add Assets/Scripts/CurrencyUI.cs
git commit -m "feat(compost): display compost in top currency bar"
```

---

## Task 4: New StatKey constants for Cow + Compost Bay

**Files:**
- Modify: `Assets/Scripts/Research/StatKey.cs`

- [ ] **Step 4.1: Add the constants**

In the `Animals` block, append:

```csharp
public const string CowPassiveCompost = "cow_passive_compost";
public const string CowRunYield = "cow_run_yield";
```

In the `Equipment` block, append:

```csharp
public const string CompostBayConversion = "compost_bay_conversion";
```

- [ ] **Step 4.2: Commit**

```bash
git add Assets/Scripts/Research/StatKey.cs
git commit -m "feat(compost): add Cow + Compost Bay stat keys"
```

---

## Task 5: Catalog generator adds 3 new researches; regenerate

**Files:**
- Modify: `Assets/Editor/ResearchCatalogGenerator.cs`

- [ ] **Step 5.1: Add Cow's two researches under the Animals section**

Find the Animals comment block. Append:

```csharp
CreateStd("cow_passive_compost", "Cow: Passive Compost Rate", StatKey.CowPassiveCompost, ResearchTier.Tier100Standard, "animals", 0.005f, animalID:"cow");
CreateStd("cow_run_yield",       "Cow: Run Yield",            StatKey.CowRunYield,       ResearchTier.Tier25,          "animals", 0.010f, animalID:"cow");
```

Update the comment header for Animals from "(6 — gated by ownership; Cow ships in Plan 2)" to "(8 — gated by ownership)".

- [ ] **Step 5.2: Add Compost Bay's research under the Equipment section**

Find the Equipment comment block. Append:

```csharp
CreateStd("compost_bay_conversion", "Compost Bay: Conversion Efficiency", StatKey.CompostBayConversion, ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"compostbay_unlock");
```

Update the comment header from "(9 — Compost Bay deferred to Plan 2)" to "(10)".

- [ ] **Step 5.3: Run the generator**

In Unity: `Farm Game → Research → Generate Catalog`. Console should show `Catalog written to Assets/Resources/Research`. Verify the folder now contains 40 `Research_*.asset` files (was 37 + 3 = 40).

- [ ] **Step 5.4: Commit**

```bash
git add Assets/Editor/ResearchCatalogGenerator.cs Assets/Resources/Research
git commit -m "feat(compost): add 3 Cow/Compost Bay catalog entries; regenerate"
```

---

## Task 6: Plant.OnPlantDied event

**Files:**
- Modify: `Assets/Scripts/Plant.cs`

The Compost Bay needs a signal when a plant dies. We fire a static event from each death path (dry-out, rot, threat) but NOT from harvest. This way harvested crops don't yield compost — only wasted ones do.

- [ ] **Step 6.1: Add the static event at the top of Plant.cs**

After the class declaration's opening brace, add:

```csharp
/// <summary>
/// Fired exactly when a plant's lifecycle ends WITHOUT being harvested
/// (dry-out, rot, lightning/wind/threat damage). zoneID = plant's zone;
/// cropTier = crop.tier (used by Compost Bay for yield calc).
/// </summary>
public static event System.Action<int, int> OnPlantDied;
```

- [ ] **Step 6.2: Find and identify all RemovePlant call sites**

The non-harvest call sites are inside `UpdateMoisture` (dry-out death), `UpdateRot` (rot death), and `TakeDamage` (HP reaches 0). The harvest path is `Harvest()` (around line 295–299).

- [ ] **Step 6.3: Add a helper that fires the event then removes**

Right above `RemovePlant`, add:

```csharp
private void Die()
{
    if (parentTile != null && cropData != null)
        OnPlantDied?.Invoke(parentTile.ZoneID, cropData.tier);
    RemovePlant();
}
```

- [ ] **Step 6.4: Replace the death-path RemovePlant calls with Die**

In `UpdateMoisture` (the dry-out branch where it removes the plant), `UpdateRot` (the rot-out branch), and `TakeDamage` (where HP hits 0), replace `RemovePlant();` with `Die();`. Do NOT change the call inside `Harvest()` — that one stays as `RemovePlant()`.

Use Grep to verify: `git grep -n "RemovePlant" Assets/Scripts/Plant.cs` should show 4 calls — 3 became `Die()` and 1 in Harvest stays `RemovePlant()`. Then re-grep specifically: `git grep -n "Die()" Assets/Scripts/Plant.cs` should show 3 call sites plus the method definition.

- [ ] **Step 6.5: Confirm `CropData.tier` exists**

```
Grep: pattern "public int tier" path Assets/Scripts/CropData.cs
```

If the field doesn't exist with that exact name, look for the closest equivalent (e.g. `tier`, `cropTier`, `priceTier`). If none exists, add `public int tier = 1;` to `CropData.cs` and leave it at 1 for all assets — Compost Bay still works, all yields will be equal. Note this caveat in the commit.

- [ ] **Step 6.6: Commit**

```bash
git add Assets/Scripts/Plant.cs Assets/Scripts/CropData.cs
git commit -m "feat(compost): fire Plant.OnPlantDied on non-harvest removals"
```

---

## Task 7: UnlockData feature-flag gating

**Files:**
- Modify: `Assets/Scripts/UnlockData.cs`

- [ ] **Step 7.1: Add the field**

Read `UnlockData.cs` first. Add a new public field near the other unlock fields:

```csharp
[Tooltip("Optional. If set, this unlock is hidden in shops until the matching research feature flag is true (ResearchManager.IsFeatureUnlocked).")]
public string requiredFeatureFlag = "";
```

- [ ] **Step 7.2: Commit**

```bash
git add Assets/Scripts/UnlockData.cs
git commit -m "feat(compost): add requiredFeatureFlag to UnlockData"
```

---

## Task 8: ShopPopupUITK filters by feature flag

**Files:**
- Modify: `Assets/Scripts/UI/ShopPopupUITK.cs`

- [ ] **Step 8.1: Find the row-render loop**

Open `ShopPopupUITK.cs`. Find where it loops the `unlocks` array to build rows (search for `unlocks` or `rowsList`). The loop will look like `foreach (UnlockData u in unlocks) { ... }` or similar.

- [ ] **Step 8.2: Add the gating predicate**

At the very top of the loop body (before any row is created), add:

```csharp
if (!string.IsNullOrEmpty(u.requiredFeatureFlag))
{
    if (ResearchManager.Instance == null) continue;
    if (!ResearchManager.Instance.IsFeatureUnlocked(u.requiredFeatureFlag)) continue;
}
```

Also subscribe to `ResearchManager.Instance.OnFeatureFlagUnlocked` in `TrySubscribeEvents` and call `MarkDirty` from the handler — that way the shop refreshes when the player finishes Composting Basics.

In `TrySubscribeEvents` (or equivalent), after the existing subscriptions:

```csharp
if (ResearchManager.Instance != null)
{
    ResearchManager.Instance.OnFeatureFlagUnlocked += OnFeatureFlagUnlocked;
}
```

And the handler:

```csharp
private void OnFeatureFlagUnlocked(string _) => MarkDirty();
```

Mirror an unsubscribe in `UnsubscribeEvents`.

- [ ] **Step 8.3: Commit**

```bash
git add Assets/Scripts/UI/ShopPopupUITK.cs
git commit -m "feat(compost): shop filters unlocks by required research feature flag"
```

---

## Task 9: Compost Bay setup (UnlockData + EquipmentData assets)

**Files:**
- Create: `Assets/Editor/CompostBaySetupGenerator.cs`

We use an editor menu so the asset wiring is reproducible and we don't depend on manual SerializeReference edits.

- [ ] **Step 9.1: Write the generator**

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CompostBaySetupGenerator
{
    [MenuItem("Farm Game/Compost/Generate Compost Bay Assets")]
    public static void Generate()
    {
        // 1. UnlockData
        const string unlockPath = "Assets/Data/Unlocks/CompostBay_Unlock.asset";
        var unlock = AssetDatabase.LoadAssetAtPath<UnlockData>(unlockPath);
        if (unlock == null)
        {
            unlock = ScriptableObject.CreateInstance<UnlockData>();
            AssetDatabase.CreateAsset(unlock, unlockPath);
        }
        unlock.unlockID = "compostbay_unlock";
        unlock.displayName = "Compost Bay";
        unlock.coinCost = 800;
        unlock.lockedDescription = "Converts dead crops in its zone into Compost.";
        unlock.unlockedMessage = "Compost Bay Unlocked!";
        unlock.requiredFeatureFlag = Research.FeatureFlag.CompostingBasics;
        EditorUtility.SetDirty(unlock);

        // 2. EquipmentData
        const string eqPath = "Assets/Data/Equipment/CompostBay.asset";
        var eq = AssetDatabase.LoadAssetAtPath<EquipmentData>(eqPath);
        if (eq == null)
        {
            eq = ScriptableObject.CreateInstance<EquipmentData>();
            AssetDatabase.CreateAsset(eq, eqPath);
        }
        eq.unlockID = "compostbay_unlock";
        // Conversion is in upgrade fields so EquipmentManager.GetEffective* picks up research.
        eq.aoeUpgradeID       = "";    // n/a — no area, whole zone
        eq.cooldownUpgradeID  = "";    // n/a
        eq.capacityUpgradeID  = "";    // n/a
        eq.waterPowerUpgradeID = "compost_bay_conversion"; // hijack the "power" channel for conversion %
        eq.baseAoERadius = 0f;
        eq.baseCooldownSeconds = 0f;
        eq.baseRepelCapacity = 0;
        eq.baseMoisturePowerPerSecond = 1.0f; // base conversion = 1.0x (multiplied by crop.tier in CompostBay.cs)
        eq.waterPowerBonusPerLevel = 0f;       // research bonus drives further scaling
        EditorUtility.SetDirty(eq);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CompostBaySetupGenerator] CompostBay_Unlock and CompostBay assets ready.");
    }
}
#endif
```

- [ ] **Step 9.2: Run the menu**

In Unity: `Farm Game → Compost → Generate Compost Bay Assets`. Verify the two assets exist:
- `Assets/Data/Unlocks/CompostBay_Unlock.asset`
- `Assets/Data/Equipment/CompostBay.asset`

- [ ] **Step 9.3: Append to EquipmentRegistry**

Open `Assets/Data/Equipment/EquipmentRegistry.asset` in the Inspector. Add a new entry referencing `CompostBay.asset`. (Or: read the registry's serialized list to confirm the field name, then write a tiny editor extension to do it. Simplest: Inspector drag.)

- [ ] **Step 9.4: Add to the Equipment shop**

Find the `ShopPopupUITK` instance for `Section.Equipment` in the scene. In its Inspector, drag `CompostBay_Unlock.asset` into its `unlocks` array. The shop will hide it until Composting Basics completes.

- [ ] **Step 9.5: Commit**

```bash
git add Assets/Editor/CompostBaySetupGenerator.cs "Assets/Data/Unlocks/CompostBay_Unlock.asset" "Assets/Data/Unlocks/CompostBay_Unlock.asset.meta" "Assets/Data/Equipment/CompostBay.asset" "Assets/Data/Equipment/CompostBay.asset.meta"
git commit -m "feat(compost): Compost Bay unlock + equipment assets, generated"
```

---

## Task 10: CompostBay component

**Files:**
- Create: `Assets/Scripts/CompostBay.cs`

The Bay listens for `Plant.OnPlantDied` and credits Compost for plants in its zone.

- [ ] **Step 10.1: Create the component**

```csharp
using UnityEngine;

/// <summary>
/// Per-zone Compost Bay. Subscribes to Plant.OnPlantDied and credits compost
/// for dying plants in its assigned zone. Yield per kill = crop.tier × baseConversion
/// (read from EquipmentData via EquipmentManager.GetEffectiveWaterPower, which folds
/// in the Compost Bay Conversion Efficiency research bonus).
/// </summary>
public class CompostBay : MonoBehaviour
{
    [SerializeField] private int zoneID = 0;
    [SerializeField] private EquipmentData data;
    public int ZoneID => zoneID;

    public void Initialize(int zone, EquipmentData eq)
    {
        zoneID = zone;
        data = eq;
    }

    private void OnEnable()  => Plant.OnPlantDied += HandlePlantDied;
    private void OnDisable() => Plant.OnPlantDied -= HandlePlantDied;

    private void HandlePlantDied(int diedZone, int cropTier)
    {
        if (diedZone != zoneID) return;
        if (data == null || EquipmentManager.Instance == null || CurrencyManager.Instance == null) return;

        float conversion = EquipmentManager.Instance.GetEffectiveWaterPower(data); // base 1.0 × (1 + research)
        int yield = Mathf.Max(1, Mathf.RoundToInt(cropTier * conversion));
        CurrencyManager.Instance.AddCompost(yield);
    }
}
```

- [ ] **Step 10.2: Commit**

```bash
git add Assets/Scripts/CompostBay.cs
git commit -m "feat(compost): CompostBay listens for OnPlantDied in its zone"
```

- [ ] **Step 10.3: Spawn one Bay per zone in the scene**

In the scene hierarchy, find each Zone GameObject (typically `Zone_1`, `Zone_2`, etc. — search by `Zone` if unsure). For each zone:
1. Add a child GameObject named `CompostBay`.
2. Add the `CompostBay` component.
3. Set `zoneID` to match the zone (1, 2, 3, 4).
4. Drag `Assets/Data/Equipment/CompostBay.asset` into the `Data` field.
5. Disable the GameObject (`SetActive(false)`) — it'll be activated when the player purchases the equipment.

Then in `EquipmentManager` find where it activates zone equipment GameObjects on purchase. Add Compost Bay to that activation logic if it's not already pattern-matched. (If `EquipmentManager` uses `EquipmentData.visualPrefab` to instantiate equipment, the Bay's data needs a visual prefab too — minimal: a quad with a brown color, or just an empty for invisible.)

- [ ] **Step 10.4: Commit scene changes**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat(compost): place Compost Bay GameObject in each zone (inactive)"
```

---

## Task 11: AnimalData adds compostPerMinute

**Files:**
- Modify: `Assets/Scripts/AnimalData.cs`

- [ ] **Step 11.1: Add the field**

Append a new field to the SO:

```csharp
[Header("Compost (Cow only)")]
[Tooltip("Base compost generated per real-world minute while equipped. Other animals leave at 0.")]
public float compostPerMinute = 0f;
```

- [ ] **Step 11.2: Set Cow's value**

Open `Assets/Data/Animals/Animal_Cow.asset` in the Inspector. Set `compostPerMinute = 0.5` (= 30 compost/hour at baseline).

- [ ] **Step 11.3: Commit**

```bash
git add Assets/Scripts/AnimalData.cs "Assets/Data/Animals/Animal_Cow.asset"
git commit -m "feat(compost): AnimalData.compostPerMinute (Cow = 0.5/min)"
```

---

## Task 12: AnimalManager runs Cow passive accumulator

**Files:**
- Modify: `Assets/Scripts/AnimalManager.cs`

Cow's passive compost ticks every second (or on boot, computing offline elapsed). It uses `UtcNow` so it works while the app is closed.

- [ ] **Step 12.1: Add the timestamp + tick fields**

Near the existing private fields (around `lastEggClaimTime`), add:

```csharp
private DateTime lastCompostTickUtc = DateTime.MinValue;
private float compostTickAccumulator;
private const float COMPOST_TICK_INTERVAL_SECS = 5f;
```

- [ ] **Step 12.2: Initialize on equip / load**

In whichever method handles equipping or loading an animal (`EquipAnimal` / `LoadState`), if the equipped animal has `compostPerMinute > 0`, set `lastCompostTickUtc = DateTime.UtcNow` when first equipped (or take the saved value if loading).

Add to `LoadState` signature a new last-arg `string lastCompostTimeISO`, parse it via `DateTime.TryParse`, store in `lastCompostTickUtc`. Also add `GetLastCompostTimeISO()` accessor returning `lastCompostTickUtc.ToString("o")`.

- [ ] **Step 12.3: Add the tick logic in Update**

In `Update()` (the existing one — search for `eggCheckTimer`), after the existing egg/gem tick block, add:

```csharp
compostTickAccumulator += Time.unscaledDeltaTime;
if (compostTickAccumulator >= COMPOST_TICK_INTERVAL_SECS)
{
    compostTickAccumulator = 0f;
    TickCompost();
}
```

Add the method:

```csharp
private void TickCompost()
{
    AnimalData equipped = GetEquippedAnimal();
    if (equipped == null || equipped.compostPerMinute <= 0f) return;
    if (CurrencyManager.Instance == null) return;
    if (lastCompostTickUtc == DateTime.MinValue) lastCompostTickUtc = DateTime.UtcNow;

    double elapsedMin = (DateTime.UtcNow - lastCompostTickUtc).TotalMinutes;
    if (elapsedMin <= 0) return;

    float ratePerMin = equipped.compostPerMinute;
    if (ResearchManager.Instance != null)
        ratePerMin *= 1f + ResearchManager.Instance.GetBonus(Research.StatKey.CowPassiveCompost);

    int amount = Mathf.FloorToInt((float)(elapsedMin * ratePerMin));
    if (amount <= 0) return;

    CurrencyManager.Instance.AddCompost(amount);
    // Advance the timestamp by the amount we just credited (avoids drift).
    double minutesAwarded = amount / ratePerMin;
    lastCompostTickUtc = lastCompostTickUtc.AddMinutes(minutesAwarded);
}
```

- [ ] **Step 12.4: Wire SaveManager (compost timestamp)**

In `SaveManager.SaveGame()`, fetch the new timestamp and pass into the GameData call. In `GameData`, add a `string lastCompostClaimTime = "";` field + constructor param. In `SaveManager.LoadGame()`, pass the loaded value into `AnimalManager.LoadState`.

- [ ] **Step 12.5: Commit**

```bash
git add Assets/Scripts/AnimalManager.cs Assets/Scripts/SaveManager.cs Assets/Scripts/GameData.cs
git commit -m "feat(compost): AnimalManager ticks Cow passive compost (offline-safe via UtcNow)"
```

---

## Task 13: Cow during-run eating

**Files:**
- Create: `Assets/Scripts/Cow.cs`

The Cow visual prefab needs a `Cow` component. During a run, periodically (every 30–60s) it picks a random mature crop, walks to it, and eats it: removes the plant and grants a lump of compost.

- [ ] **Step 13.1: Create the script**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// During-run behavior for the Cow animal. Once a run is active, periodically picks
/// a random mature crop, walks to it, eats it (= removes plant, awards compost lump).
/// The compost lump scales with the Cow Run Yield research bonus and the Cow's passive
/// rate (so upgrading passive also helps run-eating).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Cow : MonoBehaviour
{
    [Header("Eating Behavior")]
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float minIntervalSecs = 30f;
    [SerializeField] private float maxIntervalSecs = 60f;
    [Tooltip("Compost lump per eaten crop at L0 Run Yield. Cow's compostPerMinute scales this too.")]
    [SerializeField] private int baseLumpPerEat = 15;

    private Coroutine loop;

    private void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += BeginEatingLoop;
            RunManager.Instance.OnRunEnded   += EndEatingLoop;
            if (RunManager.Instance.IsRunActive) BeginEatingLoop();
        }
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= BeginEatingLoop;
            RunManager.Instance.OnRunEnded   -= EndEatingLoop;
        }
        EndEatingLoop();
    }

    private void BeginEatingLoop()
    {
        EndEatingLoop();
        loop = StartCoroutine(EatLoop());
    }

    private void EndEatingLoop()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    private IEnumerator EatLoop()
    {
        while (true)
        {
            float wait = Random.Range(minIntervalSecs, maxIntervalSecs);
            yield return new WaitForSeconds(wait);

            Plant target = PickRandomMaturePlant();
            if (target == null) continue;

            yield return WalkTo(target.transform.position);
            if (target == null) continue; // may have been harvested mid-walk

            EatPlant(target);
        }
    }

    private Plant PickRandomMaturePlant()
    {
        if (FarmGrid.Instance == null) return null;
        var occupied = FarmGrid.Instance.GetOccupiedTiles();
        var matures = new List<Plant>();
        foreach (var tile in occupied)
        {
            if (tile.CurrentPlant == null) continue;
            Plant p = tile.CurrentPlant.GetComponent<Plant>();
            if (p != null && p.IsHarvestable) matures.Add(p);
        }
        if (matures.Count == 0) return null;
        return matures[Random.Range(0, matures.Count)];
    }

    private IEnumerator WalkTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private void EatPlant(Plant plant)
    {
        if (plant == null || CurrencyManager.Instance == null) return;

        int lump = baseLumpPerEat;
        if (ResearchManager.Instance != null)
            lump = Mathf.RoundToInt(lump * (1f + ResearchManager.Instance.GetBonus(Research.StatKey.CowRunYield)));

        CurrencyManager.Instance.AddCompost(lump);
        FloatingTextManager.ShowText($"+{lump} 🌱", plant.transform.position, Color.green);

        // Remove the plant — re-using the Harvest harvested path is wrong (gives money).
        // We just destroy it through its tile so the plant is gone but no money awarded.
        if (plant.parentTile != null) plant.parentTile.ClearPlant();
        Destroy(plant.gameObject);
    }
}
```

- [ ] **Step 13.2: Check FloatingTextManager API**

Look at `Assets/Scripts/FloatingTextManager.cs` to confirm a `ShowText(string, Vector3, Color)` method exists. If not, replace the call with whichever overload exists (e.g. `ShowMoney` or `ShowText`). If no plain text overload exists, drop that line — the audio/visual feedback for Cow eating is a nice-to-have, not core.

- [ ] **Step 13.3: Plant.parentTile accessibility**

In `Cow.cs` we read `plant.parentTile`. If `parentTile` is private in `Plant`, add a `public SoilTile ParentTile => parentTile;` accessor to `Plant.cs` and update `Cow.cs` to call `plant.ParentTile`.

- [ ] **Step 13.4: Add Cow component to Cow's visualPrefab**

In Unity, find the Cow visual prefab (referenced from `Animal_Cow.asset → visualPrefab`). Open it. Add the `Cow` component. Set defaults: `walkSpeed 1.2`, `minIntervalSecs 30`, `maxIntervalSecs 60`, `baseLumpPerEat 15`.

- [ ] **Step 13.5: Commit**

```bash
git add Assets/Scripts/Cow.cs Assets/Scripts/Plant.cs Assets/Prefabs
git commit -m "feat(compost): Cow eats random mature crops during runs for compost lumps"
```

---

## Task 14: ResearchManager.TryApplyBoost

**Files:**
- Modify: `Assets/Scripts/ResearchManager.cs`

- [ ] **Step 14.1: Add the boost mutator**

Below `CancelResearch`, add:

```csharp
/// <summary>
/// Buy a boost token for an active research slot. Multiplier replaces any active boost
/// (tokens are not stackable). durationSecs is the boost window starting NOW.
/// </summary>
public bool TryApplyBoost(int slotIndex, float multiplier, float durationSecs, int compostCost)
{
    if (!IsValidSlot(slotIndex)) return false;
    var s = slots[slotIndex];
    if (s.IsIdle) return false;
    if (multiplier <= 1.0f || durationSecs <= 0f) return false;
    if (CurrencyManager.Instance == null) return false;
    if (!CurrencyManager.Instance.SpendCompost(compostCost)) return false;

    s.boostMultiplier = multiplier;
    s.boostExpiresUtcTicks = DateTime.UtcNow.Ticks + (long)(durationSecs * TimeSpan.TicksPerSecond);
    OnSlotStateChanged?.Invoke(slotIndex);
    return true;
}
```

- [ ] **Step 14.2: Verify boost is already counted in Tick**

The existing `ComputeElapsedSeconds` (added in Plan 1) already credits `(boostMultiplier - 1) × boostSeconds` to the slot's elapsed time. No change needed there.

- [ ] **Step 14.3: Commit**

```bash
git add Assets/Scripts/ResearchManager.cs
git commit -m "feat(compost): ResearchManager.TryApplyBoost spends compost for slot speedup"
```

---

## Task 15: CompostBoostModalUITK assets

**Files:**
- Create: `Assets/UI/CompostBoostModalUITK/CompostBoostModalUITK.uxml`
- Create: `Assets/UI/CompostBoostModalUITK/CompostBoostModalUITK.uss`

- [ ] **Step 15.1: Make the folder**

```bash
mkdir -p "Assets/UI/CompostBoostModalUITK"
```

- [ ] **Step 15.2: UXML**

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="CompostBoostModalUITK.uss" />
    <ui:VisualElement name="modal-root" class="modal-root" style="display: none;">
        <ui:VisualElement name="modal-backdrop" class="modal-backdrop" />
        <ui:VisualElement name="modal-card" class="modal-card">
            <ui:VisualElement name="modal-header" class="modal-header">
                <ui:Label name="modal-title" text="Boost Research" class="modal-title" />
                <ui:Button name="modal-close" class="modal-close" text="X" />
            </ui:VisualElement>
            <ui:Label name="modal-subtitle" text="Spend Compost to multiply this slot's research speed for a fixed window." class="modal-subtitle" />
            <ui:ScrollView name="boost-list" class="boost-list" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 15.3: USS**

```css
.modal-root {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    align-items: center;
    justify-content: center;
}

.modal-backdrop {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.75);
}

.modal-card {
    width: 92%;
    max-height: 80%;
    background-color: rgb(38, 28, 16);
    border-color: rgb(110, 86, 50);
    border-width: 4px;
    border-top-left-radius: 22px;
    border-top-right-radius: 22px;
    border-bottom-left-radius: 22px;
    border-bottom-right-radius: 22px;
    padding: 22px;
    flex-direction: column;
}

.modal-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 10px;
    padding-bottom: 12px;
    border-bottom-width: 2px;
    border-bottom-color: rgb(110, 86, 50);
}

.modal-title { font-size: 32px; -unity-font-style: bold; color: rgb(255, 240, 210); }
.modal-close { width: 56px; height: 56px; background-color: rgba(0,0,0,0); color: rgb(220, 195, 150); font-size: 28px; -unity-font-style: bold; border-width: 0; }
.modal-subtitle { font-size: 18px; color: rgb(200, 180, 140); white-space: normal; margin-bottom: 16px; }

.boost-list { flex-grow: 1; }

.boost-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 14px 18px;
    margin-bottom: 10px;
    background-color: rgba(255, 240, 210, 0.06);
    border-color: rgba(255, 240, 210, 0.08);
    border-width: 2px;
    border-top-left-radius: 14px;
    border-top-right-radius: 14px;
    border-bottom-left-radius: 14px;
    border-bottom-right-radius: 14px;
}

.boost-row__label { color: rgb(255, 240, 210); font-size: 22px; -unity-font-style: bold; flex-grow: 1; }
.boost-row__cost  { color: rgb(140, 200, 100); font-size: 22px; -unity-font-style: bold; }
.boost-row--disabled { opacity: 0.55; }
.boost-row--disabled .boost-row__cost { color: rgb(220, 90, 90); }
```

- [ ] **Step 15.4: Commit**

```bash
git add Assets/UI/CompostBoostModalUITK
git commit -m "feat(compost): CompostBoostModalUITK UXML + USS"
```

---

## Task 16: CompostBoostModalUITK controller

**Files:**
- Create: `Assets/Scripts/UI/CompostBoostModalUITK.cs`

- [ ] **Step 16.1: Create the script**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1100)]
public class CompostBoostModalUITK : MonoBehaviour
{
    public static CompostBoostModalUITK Instance { get; private set; }

    /// <summary>Boost token pricing: (multiplier, durationSecs, compostCost).</summary>
    private static readonly (float multiplier, float durationSecs, int cost)[] Tokens = new[]
    {
        (2f,  4f * 3600f,  50),
        (3f,  4f * 3600f, 150),
        (4f,  4f * 3600f, 400),
        (2f, 12f * 3600f, 120),
        (3f, 12f * 3600f, 360),
        (4f, 12f * 3600f, 1000),
    };

    private UIDocument document;
    private VisualElement root;
    private VisualElement modalRoot;
    private VisualElement boostList;
    private Button closeButton;
    private VisualElement backdrop;

    private int targetSlotIndex = -1;
    private bool isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable() => CacheAndWire();
    private void Start()    { if (root == null) CacheAndWire(); }

    private void CacheAndWire()
    {
        root = document.rootVisualElement;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;

        modalRoot   = root.Q<VisualElement>("modal-root");
        boostList   = root.Q<ScrollView>("boost-list") as VisualElement ?? root.Q<VisualElement>("boost-list");
        closeButton = root.Q<Button>("modal-close");
        backdrop    = root.Q<VisualElement>("modal-backdrop");

        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (backdrop != null)    backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open(int slotIndex)
    {
        if (root == null) CacheAndWire();
        targetSlotIndex = slotIndex;
        isOpen = true;
        if (root != null) root.pickingMode = PickingMode.Position;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.Flex;
        Rebuild();
    }

    public void Close()
    {
        isOpen = false;
        targetSlotIndex = -1;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void Rebuild()
    {
        if (boostList == null) return;
        boostList.Clear();

        int compostBalance = CurrencyManager.Instance != null ? CurrencyManager.Instance.Compost : 0;

        foreach (var token in Tokens)
        {
            var row = new VisualElement(); row.AddToClassList("boost-row");
            string hrs = (token.durationSecs / 3600f).ToString("F0");
            var label = new Label($"{token.multiplier:F0}× for {hrs} hr"); label.AddToClassList("boost-row__label");
            var cost  = new Label($"{token.cost} 🌱"); cost.AddToClassList("boost-row__cost");
            row.Add(label); row.Add(cost);

            bool affordable = compostBalance >= token.cost;
            if (!affordable)
                row.AddToClassList("boost-row--disabled");
            else
            {
                var captured = token;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (ResearchManager.Instance != null &&
                        ResearchManager.Instance.TryApplyBoost(targetSlotIndex, captured.multiplier, captured.durationSecs, captured.cost))
                    {
                        Close();
                    }
                });
            }
            boostList.Add(row);
        }
    }
}
```

- [ ] **Step 16.2: Add GameObject in scene**

In Unity scene, add a new GameObject `CompostBoostModalUITK`. Add `UIDocument` + `CompostBoostModalUITK` components. Set the UXML source to `Assets/UI/CompostBoostModalUITK/CompostBoostModalUITK.uxml`. Use a high sort order on PanelSettings so it draws above the Research popup.

- [ ] **Step 16.3: Commit**

```bash
git add Assets/Scripts/UI/CompostBoostModalUITK.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(compost): Compost Boost modal controller + scene wiring"
```

---

## Task 17: ResearchPopupUITK adds Boost button

**Files:**
- Modify: `Assets/Scripts/UI/ResearchPopupUITK.cs`

- [ ] **Step 17.1: Add Boost button to active slot card**

In `RenderActiveSlot`, just above the `Cancel` label, add:

```csharp
Label boostBtn = new Label("⚡ Boost"); boostBtn.AddToClassList("slot-card__cancel"); // reuse style
boostBtn.style.color = new StyleColor(new Color(0.55f, 0.78f, 0.39f));
int capturedSlot = slotIndex;
boostBtn.RegisterCallback<ClickEvent>(_ =>
{
    if (CompostBoostModalUITK.Instance != null) CompostBoostModalUITK.Instance.Open(capturedSlot);
});
card.Add(boostBtn);
```

The existing boost-indicator label (`slot-card__boost` / `slot-card__boost--active`) already activates when `state.boostMultiplier > 1`. No change there.

- [ ] **Step 17.2: Commit**

```bash
git add Assets/Scripts/UI/ResearchPopupUITK.cs
git commit -m "feat(compost): Boost button on active slot cards opens the modal"
```

---

## Task 18: Smoke test checklist (manual)

- [ ] **Step 18.1: Composting Basics unlocks Compost Bay in Market**

Cheat: in Inspector, set ResearchManager.levelsByResearchID["composting_basics"] = 1 + add "composting_basics" to featureFlags. Open Market → Equipment shop → Compost Bay should now appear. Buy it.

- [ ] **Step 18.2: Compost Bay yields on plant death**

Plant a crop in zone 1, let it dry out and die. Verify the top currency bar Compost increments (yield = `cropTier × baseConversion`, default tier 1 → ~1 compost per death).

- [ ] **Step 18.3: Compost Bay does NOT yield on harvest**

Plant + harvest in zone 1. Verify Compost does NOT increment.

- [ ] **Step 18.4: Cow passive idle**

Equip Cow. Sit idle for 2 minutes. Verify Compost increases by ~1 (0.5 compost/min × 2 min). Stop play, wait 5 min in real time, restart play — verify Compost catches up.

- [ ] **Step 18.5: Cow eats during run**

Equip Cow. Start a run. Plant some crops, wait for them to mature. Wait 30–60s — Cow should walk to a mature crop, it disappears, and Compost +15 floating text appears.

- [ ] **Step 18.6: Boost token mechanic**

Assign Helper Till Speed to a slot. With ≥50 Compost, tap the new `⚡ Boost` button. Modal opens with 6 options. Pick `2× for 4 hr (50 🌱)`. Modal closes. Slot card shows boost indicator. Watch countdown — it ticks down twice as fast.

- [ ] **Step 18.7: Boost expires**

Set boost duration to 60s temporarily (modify `Tokens` array). Confirm slot returns to 1x after boost ends.

- [ ] **Step 18.8: Save round-trip**

Stop Play, restart. Compost balance, Cow accumulator timestamp, active boost state all persist.

- [ ] **Step 18.9: Commit any tuning tweaks**

```bash
git add -u
git commit -m "fix(compost): smoke-test tuning"
```

---

## Self-Review

- **Spec coverage:** ✅ Compost currency (Task 1–3); Compost Bay equipment + Market gating (Task 7–10); Cow with two researches + passive + run-eating (Task 11–13); boost token modal + ResearchManager wiring (Task 14–17); catalog now totals 40 entries (Task 5).
- **Placeholder scan:** No TBDs. Every code step shows the actual code. The two soft spots (FloatingTextManager API, `Plant.parentTile` accessor) are explicitly flagged with fallback instructions in Task 13.
- **Type consistency:** `CurrencyManager.Compost` / `AddCompost` / `SpendCompost` / `CanAffordCompost` named identically across Tasks 1, 2, 10, 13, 14, 16. `Plant.OnPlantDied` signature `(int zoneID, int cropTier)` matches Task 6 → Task 10. `ResearchManager.TryApplyBoost(int, float, float, int)` matches Task 14 → Task 16. `Research.FeatureFlag.CompostingBasics` is the existing constant from Plan 1.
- **Known soft spots:** `CropData.tier` (Task 6.5 — add if missing), `FloatingTextManager.ShowText` overload (Task 13.2), `Plant.parentTile` accessibility (Task 13.3), `EquipmentManager` zone-spawn logic for Compost Bay (Task 10.3). All have inline fallback guidance.
