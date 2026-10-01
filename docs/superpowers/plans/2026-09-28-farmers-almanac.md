# Farmer's Almanac + Crop Traits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Farmer's Almanac book (Crops · Equipment · Animals · Pests) showing honest base (+bonus) = total stats, backed by real per-crop deer/crow appetite traits.

**Architecture:** Pure `StatLine` builder + `CropTraits` helpers in `IdleFarm.EconomyCore` (unit-tested). `CropStats` / `EquipmentStats` in Assembly-CSharp build `StatLine`s from live managers, and gameplay (`Plant`, `EquipmentManager`) is refactored to read the same builders so the book can't drift from play. `AlmanacCatalog` assembles entries; `AlmanacPopupUITK` (code-built UITK) renders them; a baked HUD book button and "i" badges open it.

**Tech Stack:** Unity 6.3 (6000.3.9f1), C#, UI Toolkit (runtime, code-built), uGUI HUD, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-09-28-farmers-almanac-design.md`

## Global Constraints

- No emoji in any UI text (invisible on Android); no one-side colored "accent stripe" borders.
- Pure/testable logic lives in `Assets/Scripts/EconomyCore` (asmdef `IdleFarm.EconomyCore`, cannot reference Assembly-CSharp).
- Don't hand-edit `.asset`/`.unity` YAML — change assets through editor code (`SerializedObject`) run via menu or `Temp/menu.request` (UIDriveBridge).
- Unity only recompiles when its window has focus: verify compiles out-of-editor with Unity's Roslyn (`Library/Bee/artifacts/1300b0aE.dag/*.rsp`), and use `Library/ScriptAssemblies/*.dll` mtimes vs `Temp/compile_marker` to know when Unity has loaded new code.
- Copy (blurbs) goes in the LetterCatalog tips (`almanac_*` ids) so it appears in `docs/narrative/cast-and-copy.md`.
- No git commits unless the user asks (project rule) — "checkpoint" steps below mean: compile + tests green.
- Phone-first layout: verify at 1080x2400 via UIDriveBridge `Temp/gameview_size.request`.

---

### Task 1: StatLine builder + text formatting (pure)

**Files:**
- Create: `Assets/Scripts/EconomyCore/StatLine.cs`
- Test: `Assets/Tests/EditMode/StatLineTests.cs`

**Interfaces:**
- Produces: `enum StatFormat { Money, Coins, Number, Seconds, Multiplier, Percent, Tiles }`; `struct StatBonus { string source; float delta; }`; `class StatLine` with ctor `(string label, float baseValue, StatFormat format)`, fluent `Add(string source, float amount)`, `Multiply(string source, float factor, bool roundToInt = false)`, `Divide(string source, float divisor, bool roundToInt = false)`, `AtLeast(float min)`; props `label, baseValue, total, format, IReadOnlyList<StatBonus> Bonuses, bool HasBonus`; `static string StatText.Value(float, StatFormat)`, `static string StatText.Delta(float, StatFormat)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

public class StatLineTests
{
    [Test]
    public void NoBonuses_TotalEqualsBase()
    {
        var s = new StatLine("Health", 80, StatFormat.Number).Multiply("Research", 1f);
        Assert.AreEqual(80f, s.total);
        Assert.IsFalse(s.HasBonus);
    }

    [Test]
    public void Multiply_WithRounding_RecordsPerSourceDeltas()
    {
        // 25 -> x1.2 = 30 -> x1.25 = 37.5 -> RoundToInt (banker's) = 38
        var s = new StatLine("Money", 25, StatFormat.Money)
            .Multiply("Research", 1.2f, roundToInt: true)
            .Multiply("Farm upgrades", 1.25f, roundToInt: true);
        Assert.AreEqual(38f, s.total);
        Assert.AreEqual(2, s.Bonuses.Count);
        Assert.AreEqual("Research", s.Bonuses[0].source);
        Assert.AreEqual(5f, s.Bonuses[0].delta);
        Assert.AreEqual(8f, s.Bonuses[1].delta);
    }

    [Test]
    public void Divide_And_AtLeast()
    {
        var s = new StatLine("Cooldown", 60, StatFormat.Seconds).Add("Upgrades", -20).Divide("Research", 2f).AtLeast(25f);
        Assert.AreEqual(25f, s.total);   // 60-20=40, /2=20, floored at 25
        Assert.AreEqual(3, s.Bonuses.Count); // floor recorded as its own line
    }

    [Test]
    public void Formatting()
    {
        Assert.AreEqual("$37", StatText.Value(37, StatFormat.Money));
        Assert.AreEqual("+$12", StatText.Delta(12, StatFormat.Money));
        Assert.AreEqual("5m 25s", StatText.Value(325, StatFormat.Seconds));
        Assert.AreEqual("-40s", StatText.Delta(-40, StatFormat.Seconds));
        Assert.AreEqual("x1.25", StatText.Value(1.25f, StatFormat.Multiplier));
        Assert.AreEqual("40%", StatText.Value(0.4f, StatFormat.Percent));
        Assert.AreEqual("2.5 tiles", StatText.Value(2.5f, StatFormat.Tiles));
        Assert.AreEqual("12", StatText.Value(12, StatFormat.Number));
    }
}
```

- [ ] **Step 2: Verify it fails** — out-of-editor compile of the test assembly reports `StatLine`/`StatText` not found.

- [ ] **Step 3: Implement `StatLine.cs`** (see code in repo; behaviour: every op updates `total`; a non-zero change appends `StatBonus{source, delta}`; `AtLeast` appends source `"Minimum"`; `Multiply(..., roundToInt)` uses `Mathf.RoundToInt` to match gameplay).

- [ ] **Step 4: Verify** — test assembly compiles; EditMode suite green once Unity loads it.

### Task 2: Crop traits data + helpers

**Files:**
- Create: `Assets/Scripts/EconomyCore/CropTraits.cs`
- Modify: `Assets/Scripts/CropData.cs` (replace `threatResistance` with `deerAppetite`, `crowAppetite`; add `PestAppetite(AnimalThreatType)`)
- Modify: `Assets/Scripts/AnimalThreat.cs` (`EatPlant`: `damage *= plant.CropData.PestAppetite(data.threatType)`)
- Create: `Assets/Editor/AlmanacTools.cs` (menu `Farm Game/Almanac/Apply Crop Trait Drafts` — writes the spec's 9-crop table via `SerializedObject`)
- Test: `Assets/Tests/EditMode/CropTraitsTests.cs`

**Interfaces:**
- Produces: `CropTraits.Tags(float growSeconds, int maxHp, bool regrows, bool cannable, float thirst, float deerAppetite, float crowAppetite) -> List<string>`; `CropTraits.AppetiteWord(float) -> string`; `CropTraits.AppetiteFill(float) -> float (0..1)`; `CropData.deerAppetite`, `CropData.crowAppetite`, `CropData.PestAppetite(AnimalThreatType)`.

- [ ] **Step 1: Failing tests**

```csharp
using NUnit.Framework;

public class CropTraitsTests
{
    [Test]
    public void Tags_FromThresholds()
    {
        var tags = CropTraits.Tags(140, 70, regrows: false, cannable: false, thirst: 0.8f, deerAppetite: 1f, crowAppetite: 0.5f);
        CollectionAssert.AreEquivalent(new[] { "Quick grower", "Fragile", "Drought-hardy", "Crows avoid it" }, tags);
    }

    [Test]
    public void Tags_RegrowsCannableThirstyLoved()
    {
        var tags = CropTraits.Tags(355, 95, regrows: true, cannable: true, thirst: 1.25f, deerAppetite: 1.25f, crowAppetite: 1.5f);
        CollectionAssert.AreEquivalent(new[] { "Regrows", "Slow grower", "Cannable", "Sturdy", "Thirsty", "Deer love it", "Crows love it" }, tags);
    }

    [TestCase(0.25f, "won't touch it")]
    [TestCase(0.5f, "avoids it")]
    [TestCase(0.75f, "nibbles")]
    [TestCase(1f, "normal")]
    [TestCase(1.25f, "likes it")]
    [TestCase(1.5f, "loves it")]
    public void AppetiteWord(float a, string word) => Assert.AreEqual(word, CropTraits.AppetiteWord(a));

    [Test]
    public void AppetiteFill_IsHalfOfMultiplier_Clamped()
    {
        Assert.AreEqual(0.5f, CropTraits.AppetiteFill(1f), 1e-5f);
        Assert.AreEqual(1f, CropTraits.AppetiteFill(3f), 1e-5f);
    }
}
```

- [ ] **Step 2–4:** implement `CropTraits.cs`, the `CropData` fields, the `AnimalThreat` multiplier, the editor menu; verify compile + tests.

### Task 3: Shared stat builders + gameplay parity

**Files:**
- Create: `Assets/Scripts/Almanac/CropStats.cs`, `Assets/Scripts/Almanac/EquipmentStats.cs`
- Modify: `Assets/Scripts/Plant.cs` (Initialize HP, CalculateGrowthSpeed, UpdateMoisture, Harvest money/coins use CropStats)
- Modify: `Assets/Scripts/EquipmentManager.cs` (`GetEffective*` delegate to `EquipmentStats`; `GetUpgradeLevel` → `public int UpgradeLevel(string)`)

**Interfaces:**
- Consumes: `StatLine` (Task 1), `CropData` traits (Task 2).
- Produces: `CropStats.Money(CropData, int zone = 1, int? startValue = null)`, `Coins(CropData, int zone = 1)`, `Health(CropData)`, `GrowTime(CropData)`, `GrowthSpeedMultiplier()` (float), `Thirst(CropData)`, `DrainDivisor()` (float), `HarvestWindow(CropData)`, `SeedBag(CropData)`, `BagCost(CropData)` — all `StatLine` unless noted. `EquipmentStats.Reach/Cooldown/Capacity/WaterPower/Duration/FenceCoverage(EquipmentData)`.

- [ ] Implement builders mirroring the exact current formulas (source names: "Research", "Barn skills", "Farm upgrades"); switch gameplay call sites to `.total`; out-of-editor compile.

### Task 4: Almanac catalog + copy

**Files:**
- Create: `Assets/Scripts/Almanac/AlmanacCatalog.cs`
- Modify: `Assets/Scripts/EconomyCore/NarrativeDefaults.cs` (tips `almanac_crop_<id>`, `almanac_pest_deer`, `almanac_pest_crow`, `almanac_intro`)
- Modify: `Assets/Scripts/SeedSelectionPopup.cs` (expose `Crops`, `Equipment`), `Assets/Scripts/ThreatWaveManager.cs` (expose `DeerData`, `CrowData`)
- Modify: `Assets/Editor/NarrativeCopyTool.cs` (doc groups `almanac_*` under an "Almanac" section)

**Interfaces:**
- Produces: `enum AlmanacKind { Crop, Equipment, Animal, Pest }`; `class AlmanacEntry { AlmanacKind kind; string id, name, blurb, unlockHint; Sprite icon; bool unlocked; List<string> tags; List<StatLine> stats; List<(string pest, float appetite)> appetites; List<string> notes; }`; `static List<AlmanacEntry> AlmanacCatalog.Build(AlmanacKind)`; `static AlmanacEntry AlmanacCatalog.Find(AlmanacKind, string id)`.

### Task 5: Almanac popup UI

**Files:**
- Create: `Assets/Scripts/UI/AlmanacPopupUITK.cs` (code-built UITK; BarnPopupUITK conventions: wood frame, cloned RunewoodPanelSettings sort 1000, backdrop 0.8)

**Interfaces:**
- Produces: `AlmanacPopupUITK.Instance`, `Open()`, `OpenEntry(AlmanacKind, string id)`, `Close()`, `IsOpen`.

- [ ] List view (4 tabs, icon grid, "???" locked tiles with hint on tap) → entry view (icon, name, tag chips, blurb, stat rows `label   total` + small `base  +x Research  +y Farm upgrades` sub-line, appetite bars with words) + Back/Close; auto-bootstraps if not in scene; first open fires tip `tip_almanac`.

### Task 6: Entry points

**Files:**
- Create: `Assets/Scripts/UI/AlmanacButton.cs` (uGUI button → `AlmanacPopupUITK.Open`)
- Modify: `Assets/Editor/AlmanacTools.cs` (menu `Farm Game/Almanac/Bake HUD Button` duplicates `Canvas/TopLeftLockup/InboxButton`, strips InboxButton + dot, sets `Misc_BookBlue` icon, adds AlmanacButton, saves scene)
- Modify: `Assets/Scripts/SeedSelectionPopup.cs` (seed rail tiles get an "i" badge → `OpenEntry(Crop, cropName)`, equipment tiles → `OpenEntry(Equipment, equipmentID)`)
- Modify: `Assets/Scripts/UI/AnimalPopupUITK.cs` (animal rows "i" → `OpenEntry(Animal, animalID)`)

### Task 7: Verify in play

- [ ] Unity loads code (focus); run menus: Apply Crop Trait Drafts, Seed Missing Copy, Bake HUD Button (edit mode, via `Temp/menu.request`).
- [ ] EditMode suite green.
- [ ] Play at 1080x2400 on the dev save: book button → Crops list → Corn page; Equipment → Scarecrow page with upgrade bonuses; seed-picker "i" → Tomato; Pests → Deer. Screenshot each; check no text overflow.
- [ ] Update `docs/narrative/cast-and-copy.md` and memory.
