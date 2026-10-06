# Store 2.0 (Tabs, Skins, Sets, Harvest Blessing, Research Finish-Now) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (user preference: always Native). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tabbed Store (Featured / Gems / Skins / Boosts) selling gem-priced animal & farmhouse skins that really change the farm, real-money premium sets, a Starter Bundle, a permanent Harvest Blessing, and a gem "Finish now" for Research.

**Architecture:** Pure rules in EconomyCore (`SkinDefaults`, `SkinOwnershipCore`, `ResearchGemPrice`, expanded `StoreDefaults`), EditMode-tested. A self-bootstrapping `SkinManager` owns ownership/equip and saves via GameData. Skins render through `SkinSwapper` (animals: LateUpdate frame remap onto the variant texture using the base frame's rect) and `BuildingSkinApplier` (farmhouse: width-fitted sprite). The Store popup is rebuilt per tab with in-place label refresh. Real-money items reuse StoreManager delivery and the chest reveal.

**Tech Stack:** Unity 6000.3, C#, code-built UI Toolkit, LeanTween, NUnit EditMode (`IdleFarm.EditModeTests` → `IdleFarm.EconomyCore`).

**Spec:** `docs/superpowers/specs/2026-10-05-store-skins-design.md` (builds on `2026-10-04-free-gift-and-store-design.md`)

## Global Constraints

- **No commits** (user commits on request). Every task ends with a Checkpoint.
- **Copy:** short and plain. No emoji or surrogates in UITK text.
- **Colours:** browns, walnut and gold only. No blue, no one-sided accent borders.
- **Tick rule:** never rebuild a list on a tick. Update labels in place. A tab or chip switch may rebuild that tab.
- **Pixel font:** only via `MonetizationUI.PixelText` (bake-size multiples, no bold). Big numbers use the SDF bold font.
- **Dim:** `MonetizationUI.Dim()` for backdrops.
- **Enum safety:** `StoreSection` is serialized by index in `StoreCatalog.asset`. **Append** `Sets = 3, Boosts = 4`; never reorder.
- **Prices:**

  | Tier | Gems |
  |---|---|
  | Common | 1,000 |
  | Uncommon | 1,500 |
  | Cottage | 2,000 |
  | Steel / Red / Green / Grey Barn | 2,500 |
  | Ranch | 3,000 |
  | Country Manor, Mountain Lodge | 3,500 |

  | Product | Price |
  |---|---|
  | Golden Farm Set | $4.99 |
  | Puppy Pack | $2.99 |
  | Starter Bundle | $2.99 (800 gems + Cottage) |
  | Harvest Blessing | $9.99 (×1.25 harvest Coins) |

  Research finish costs 20 gems per hour left (ceil, minimum 1).
- **Skin targets:** `chicken`, `rooster`, `cow`, `pig`, `farm_dog` (the AnimalData ids) and `farmhouse`. The Classic id is `<target>_classic`: always owned, and the default when nothing is equipped.
- **Procedures:** use the scripts in `.superpowers/sdd/2026-10-05-store-skins/`:
  - `compile.sh`, `run_tests.sh` and `shot.sh` (copied from the previous plan's workspace).
  - Editor menus via `Temp/menu.request`, play mode via `Temp/enter_play_mode.request` / `Temp/exit_play_mode.request`, state via `Temp/monet.request`.
  - **Never run tests and play mode at the same time.** Wait for edit mode before restoring saves.

## Review Focus

1. **Equip Classic while a skin frame is showing:** the animal must snap back to base frames, not keep a stale skin frame. *(Task 5 reverse map; Task 8 play step.)*
2. **Variant texture with a different size from the base** (bad catalog data): the swapper must refuse and log, not draw garbage. *(Task 5 guard; Task 1 test that every default animal skin path is in its target's family folder.)*
3. **Re-delivery/restore of a set the save already owns:** no duplicate gems, and skins stay owned. *(Task 3 `ShouldGrant` with `IsProductOwned`; Task 8 restore step.)*
4. **Accidental 1,000+ gem spend from a stray tap:** the first tap arms "Buy?", and only a second tap within 3s buys. *(Task 7; Task 8 step.)*
5. **Finish-now when the level would complete anyway / slot paused / idle:** no charge and no exception. *(Task 2 tests for 0/negative seconds; `ResearchManager.GetFinishGemCost` returns 0 for idle/paused; Task 8 step.)*

---

### Task 1: SkinDefaults + SkinOwnershipCore (pure)

**Files:**
- Create: `Assets/Scripts/EconomyCore/SkinDefaults.cs`
- Create: `Assets/Scripts/EconomyCore/SkinOwnershipCore.cs`
- Test: `Assets/Tests/EditMode/SkinOwnershipCoreTests.cs`, `Assets/Tests/EditMode/SkinDefaultsTests.cs`

**Interfaces (produces):**
- `enum SkinKind { AnimalSheet, BuildingSprite }`
- `[Serializable] class SkinDef`:
  - Fields: `id`, `displayName`, `target`, `SkinKind kind`, `assetPath`, `int gemPrice`, `string setId`, `int sortOrder`, `Texture2D texture`, `Sprite sprite`.
  - Properties: `bool IsSetOnly => !string.IsNullOrEmpty(setId)`.
- `static class SkinDefaults`:
  - `string[] Targets`
  - `string ClassicId(string target)`, `bool IsClassic(string id)`, `string TargetOfClassic(string id)`
  - `SkinDef[] All`
- `enum SkinBuyResult { Ok, AlreadyOwned, SetOnly, NotEnoughGems, Unknown }`
- `class SkinOwnershipCore`:
  - `SkinOwnershipCore(IEnumerable<SkinDef>)`
  - Queries: `bool IsOwned(id)`, `string EquippedFor(target)`, `bool IsEquipped(id)`
  - Buying: `SkinBuyResult CheckBuy(id, int gems)`, `int PriceOf(id)`, `void MarkBought(id)` (owns + equips)
  - `void Grant(IEnumerable<string>)`, `bool Equip(id)`
  - Save: `string[] ExportOwned()`, `string[] ExportEquipped()`, `void Import(string[] owned, string[] equipped)`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/SkinOwnershipCoreTests.cs
using NUnit.Framework;

public class SkinOwnershipCoreTests
{
    private static SkinDef D(string id, string target, int price, string set = "") =>
        new SkinDef { id = id, displayName = id, target = target, gemPrice = price, setId = set };

    private static SkinOwnershipCore Core() => new SkinOwnershipCore(new[]
    {
        D("chicken_brown", "chicken", 1000),
        D("chicken_golden", "chicken", 0, "set_golden_farm"),
        D("house_cottage", "farmhouse", 2000),
    });

    [Test] public void Classic_IsAlwaysOwned_AndEquippedByDefault()
    {
        var c = Core();
        Assert.IsTrue(c.IsOwned("chicken_classic"));
        Assert.AreEqual("chicken_classic", c.EquippedFor("chicken"));
        Assert.IsTrue(c.IsEquipped("chicken_classic"));
    }

    [Test] public void CheckBuy_Covers_AllRefusals()
    {
        var c = Core();
        Assert.AreEqual(SkinBuyResult.NotEnoughGems, c.CheckBuy("chicken_brown", 999));
        Assert.AreEqual(SkinBuyResult.Ok, c.CheckBuy("chicken_brown", 1000));
        Assert.AreEqual(SkinBuyResult.SetOnly, c.CheckBuy("chicken_golden", 99999));
        Assert.AreEqual(SkinBuyResult.Unknown, c.CheckBuy("nope", 99999));
        Assert.AreEqual(SkinBuyResult.AlreadyOwned, c.CheckBuy("chicken_classic", 99999));
        c.MarkBought("chicken_brown");
        Assert.AreEqual(SkinBuyResult.AlreadyOwned, c.CheckBuy("chicken_brown", 99999));
    }

    [Test] public void MarkBought_OwnsAndEquips()
    {
        var c = Core();
        c.MarkBought("chicken_brown");
        Assert.IsTrue(c.IsOwned("chicken_brown"));
        Assert.AreEqual("chicken_brown", c.EquippedFor("chicken"));
        Assert.IsFalse(c.IsEquipped("chicken_classic"));
    }

    [Test] public void Equip_RequiresOwnership_ClassicAlwaysWorks()
    {
        var c = Core();
        Assert.IsFalse(c.Equip("house_cottage"));
        Assert.AreEqual("farmhouse_classic", c.EquippedFor("farmhouse"));
        c.Grant(new[] { "house_cottage" });
        Assert.IsTrue(c.Equip("house_cottage"));
        Assert.IsTrue(c.Equip("farmhouse_classic"));
        Assert.AreEqual("farmhouse_classic", c.EquippedFor("farmhouse"));
    }

    [Test] public void Grant_IgnoresUnknownAndClassic()
    {
        var c = Core();
        c.Grant(new[] { "chicken_golden", "bogus", "chicken_classic", null });
        CollectionAssert.AreEqual(new[] { "chicken_golden" }, c.ExportOwned());
    }

    [Test] public void ExportImport_RoundTrips_AndDropsBadData()
    {
        var c = Core();
        c.MarkBought("chicken_brown");
        c.Grant(new[] { "house_cottage" });
        var copy = Core();
        copy.Import(c.ExportOwned(), c.ExportEquipped());
        Assert.IsTrue(copy.IsOwned("house_cottage"));
        Assert.AreEqual("chicken_brown", copy.EquippedFor("chicken"));

        var bad = Core();
        bad.Import(new[] { "bogus", null, "chicken_brown" }, new[] { "chicken=bogus", "farmhouse=house_cottage", "garbage", null, "cow=cow_classic" });
        CollectionAssert.AreEqual(new[] { "chicken_brown" }, bad.ExportOwned());
        Assert.AreEqual("chicken_classic", bad.EquippedFor("chicken"));      // unknown skin dropped
        Assert.AreEqual("farmhouse_classic", bad.EquippedFor("farmhouse"));  // not owned -> dropped
    }

    [Test] public void PriceOf_ReturnsGemPrice_OrZero()
    {
        var c = Core();
        Assert.AreEqual(2000, c.PriceOf("house_cottage"));
        Assert.AreEqual(0, c.PriceOf("nope"));
    }
}
```

```csharp
// Assets/Tests/EditMode/SkinDefaultsTests.cs
using System.Linq;
using NUnit.Framework;

public class SkinDefaultsTests
{
    [Test] public void Ids_AreUnique_AndTargetsValid()
    {
        var ids = SkinDefaults.All.Select(s => s.id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
        foreach (var s in SkinDefaults.All) CollectionAssert.Contains(SkinDefaults.Targets, s.target, s.id);
    }

    [Test] public void GemPrices_FollowTheTiers()
    {
        int[] animalTiers = { 1000, 1500 };
        foreach (var s in SkinDefaults.All)
        {
            if (s.IsSetOnly) { Assert.AreEqual(0, s.gemPrice, s.id); continue; }
            if (s.target == "farmhouse") Assert.That(s.gemPrice, Is.InRange(2000, 3500), s.id);
            else CollectionAssert.Contains(animalTiers, s.gemPrice, s.id);
        }
    }

    [Test] public void GemSkinTotal_IsTheSpecBudget()
    {
        Assert.AreEqual(28, SkinDefaults.All.Count(s => !s.IsSetOnly));
        Assert.AreEqual(46500, SkinDefaults.All.Where(s => !s.IsSetOnly).Sum(s => s.gemPrice));
    }

    [Test] public void AnimalSkins_LiveInTheirFamilyFolder_BuildingsInBuildings()
    {
        foreach (var s in SkinDefaults.All)
        {
            if (s.kind == SkinKind.BuildingSprite) { StringAssert.StartsWith("Assets/Sprites/Buildings/", s.assetPath, s.id); continue; }
            string folder = s.target switch
            {
                "chicken" or "rooster" => "Chickens_and_Roosters_32x32",
                "cow" => "Cows_32x32",
                "pig" => "Pigs_32x32",
                "farm_dog" => "Dogs_32x32",
                _ => "??",
            };
            StringAssert.Contains("/" + folder + "/", s.assetPath, s.id);
        }
    }

    [Test] public void Classic_Helpers()
    {
        Assert.AreEqual("cow_classic", SkinDefaults.ClassicId("cow"));
        Assert.IsTrue(SkinDefaults.IsClassic("farm_dog_classic"));
        Assert.AreEqual("farm_dog", SkinDefaults.TargetOfClassic("farm_dog_classic"));
        Assert.IsFalse(SkinDefaults.IsClassic("cow_caramel"));
    }

    [Test] public void Copy_HasNoSurrogates()
    {
        foreach (var s in SkinDefaults.All) foreach (char ch in s.displayName) Assert.IsFalse(char.IsSurrogate(ch), s.id);
    }
}
```

- [ ] **Step 2: Run tests, verify RED** (compile errors: `SkinDef`, `SkinOwnershipCore` not found).

- [ ] **Step 3: Implement**

```csharp
// Assets/Scripts/EconomyCore/SkinDefaults.cs
using System;
using UnityEngine;

public enum SkinKind { AnimalSheet, BuildingSprite }

/// <summary>One purchasable look. Animal skins are a colour-variant sheet with the base sheet's exact
/// layout; building skins are a whole sprite. Art refs are filled by Farm Game > Monetization > Build Skin Catalog.</summary>
[Serializable]
public class SkinDef
{
    public string id;
    public string displayName;
    public string target;      // AnimalData.animalID or "farmhouse"
    public SkinKind kind;
    public string assetPath;   // source art, used by the editor menu
    public int gemPrice;       // 0 for set-exclusive
    public string setId;       // StoreDefaults product id when set-exclusive
    public int sortOrder;
    public Texture2D texture;  // AnimalSheet
    public Sprite sprite;      // BuildingSprite

    public bool IsSetOnly => !string.IsNullOrEmpty(setId);
}

/// <summary>The starting skin catalogue (spec §4). Prices: common 1,000, uncommon 1,500, farmhouses 2,000-3,500;
/// set-exclusive skins cost 0 gems (real-money sets only).</summary>
public static class SkinDefaults
{
    public static readonly string[] Targets = { "chicken", "rooster", "cow", "pig", "farm_dog", "farmhouse" };
    public const string ClassicSuffix = "_classic";

    public static string ClassicId(string target) => target + ClassicSuffix;
    public static bool IsClassic(string id) => !string.IsNullOrEmpty(id) && id.EndsWith(ClassicSuffix, StringComparison.Ordinal);
    public static string TargetOfClassic(string id) => IsClassic(id) ? id.Substring(0, id.Length - ClassicSuffix.Length) : null;

    private const string Hens = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32/";
    private const string Cows = "Assets/Sprites/Animals/Cows_32x32/";
    private const string Pigs = "Assets/Sprites/Animals/Pigs_32x32/";
    private const string Dogs = "Assets/Sprites/Animals/Dogs_32x32/";
    private const string Bld = "Assets/Sprites/Buildings/";

    public const string GoldenSet = "set_golden_farm";
    public const string PuppySet = "set_puppy_pack";

    public static SkinDef[] All => new[]
    {
        A("chicken_brown", "Brown Hen", "chicken", Hens + "Chicken_Brown_32x32.png", 1000, 1),
        A("chicken_brown2", "Russet Hen", "chicken", Hens + "Chicken_Brown_2_32x32.png", 1000, 2),
        A("chicken_gray", "Gray Hen", "chicken", Hens + "Chicken_Gray_32x32.png", 1000, 3),
        A("chicken_blackbrown", "Speckled Hen", "chicken", Hens + "Chicken_Black_and_Brown_32x32.png", 1500, 4),
        A("chicken_yellow", "Buttercup Hen", "chicken", Hens + "Chicken_Yellow_32x32.png", 1500, 5),
        A("chicken_golden", "Golden Hen", "chicken", Hens + "Chicken_Golden_32x32.png", 0, 6, GoldenSet),

        A("rooster_white", "White Rooster", "rooster", Hens + "Rooster_White_32x32.png", 1000, 1),
        A("rooster_brown", "Brown Rooster", "rooster", Hens + "Rooster_Brown_32x32.png", 1000, 2),
        A("rooster_brown2", "Russet Rooster", "rooster", Hens + "Rooster_Brown_2_32x32.png", 1000, 3),
        A("rooster_blackbrown", "Speckled Rooster", "rooster", Hens + "Rooster_Black_and_Brown_32x32.png", 1500, 4),
        A("rooster_yellow", "Buttercup Rooster", "rooster", Hens + "Rooster_Yellow_32x32.png", 1500, 5),
        A("rooster_golden", "Golden Rooster", "rooster", Hens + "Rooster_Golden_32x32.png", 0, 6, GoldenSet),

        A("cow_caramel", "Caramel Cow", "cow", Cows + "Cow_Caramel_32x32.png", 1000, 1),
        A("cow_big_white", "Big White Cow", "cow", Cows + "Cow_Big_White_32x32.png", 1500, 2),
        A("cow_big_black", "Big Black Cow", "cow", Cows + "Cow_Big_Black_32x32.png", 1500, 3),
        A("cow_big_caramel", "Big Caramel Cow", "cow", Cows + "Cow_Big_Caramel_32x32.png", 1500, 4),

        A("pig_light", "Light Pink Pig", "pig", Pigs + "Pig_Pink_Light_32x32.png", 1000, 1),
        A("pig_pinkbrown", "Patchy Pig", "pig", Pigs + "Pig_Pink_and_Brown_32x32.png", 1000, 2),
        A("pig_spotted_pink", "Spotted Pig", "pig", Pigs + "Pig_Spotted_Pink_32x32.png", 1500, 3),
        A("pig_spotted_gray", "Gray Spotted Pig", "pig", Pigs + "Pig_Spotted_Gray_32x32.png", 1500, 4),

        A("dog_shepherd_dark", "Dark Shepherd", "farm_dog", Dogs + "Dog_German_Shepherd_Dark_Brown_32x32.png", 1000, 1),
        A("dog_shepherd_gray", "Gray Shepherd", "farm_dog", Dogs + "Dog_German_Shepherd_Gray_32x32.png", 1000, 2),
        A("dog_lab_brown", "Brown Lab", "farm_dog", Dogs + "Dog_Labrador_Brown_32x32.png", 0, 3, PuppySet),
        A("dog_lab_dark", "Chocolate Lab", "farm_dog", Dogs + "Dog_Labrador_Dark_Brown_32x32.png", 0, 4, PuppySet),
        A("dog_lab_white", "White Lab", "farm_dog", Dogs + "Dog_Labrador_White_32x32.png", 0, 5, PuppySet),

        B("house_cottage", "Cottage", Bld + "Farmer_House_1_32x32.png", 2000, 1),
        B("house_steel_barn", "Steel Barn", Bld + "Barn_Small_32x32.png", 2500, 2),
        B("house_red_barn", "Red Barn", Bld + "Hayloft/Front_Hayloft_Red_32x32.png", 2500, 3),
        B("house_green_barn", "Green Barn", Bld + "Hayloft/Front_Hayloft_Green_32x32.png", 2500, 4),
        B("house_grey_barn", "Grey Barn", Bld + "Hayloft/Front_Hayloft_Grey_32x32.png", 2500, 5),
        B("house_ranch", "Ranch House", Bld + "Additional Houses/24_Additional_Houses_One_Story_House_32x32.png", 3000, 6),
        B("house_manor", "Country Manor", Bld + "Additional Houses/24_Additional_Houses_Country_House_32x32.png", 3500, 7),
        B("house_lodge", "Mountain Lodge", Bld + "Additional Houses/24_Additional_Houses_Japanese_House_32x32.png", 3500, 8),
        B("house_yellow_barn", "Golden Barn", Bld + "Hayloft/Front_Hayloft_Yellow_32x32.png", 0, 9, GoldenSet),
    };

    private static SkinDef A(string id, string name, string target, string path, int gems, int order, string set = "") =>
        new SkinDef { id = id, displayName = name, target = target, kind = SkinKind.AnimalSheet, assetPath = path, gemPrice = gems, sortOrder = order, setId = set };

    private static SkinDef B(string id, string name, string path, int gems, int order, string set = "") =>
        new SkinDef { id = id, displayName = name, target = "farmhouse", kind = SkinKind.BuildingSprite, assetPath = path, gemPrice = gems, sortOrder = order, setId = set };
}
```

```csharp
// Assets/Scripts/EconomyCore/SkinOwnershipCore.cs
using System;
using System.Collections.Generic;
using System.Linq;

public enum SkinBuyResult { Ok, AlreadyOwned, SetOnly, NotEnoughGems, Unknown }

/// <summary>Which skins are owned and which is equipped per target. Classic (target + "_classic") is always
/// owned and is what EquippedFor returns when nothing else is equipped. Unknown ids are dropped on import.</summary>
public sealed class SkinOwnershipCore
{
    private readonly Dictionary<string, SkinDef> defs = new Dictionary<string, SkinDef>(StringComparer.Ordinal);
    private readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> equipped = new Dictionary<string, string>(StringComparer.Ordinal);

    public SkinOwnershipCore(IEnumerable<SkinDef> catalog)
    {
        if (catalog == null) return;
        foreach (SkinDef d in catalog) if (d != null && !string.IsNullOrEmpty(d.id)) defs[d.id] = d;
    }

    public bool IsOwned(string id) => SkinDefaults.IsClassic(id) || (id != null && owned.Contains(id));

    public string EquippedFor(string target) =>
        target != null && equipped.TryGetValue(target, out string id) ? id : SkinDefaults.ClassicId(target);

    public bool IsEquipped(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        string target = SkinDefaults.IsClassic(id) ? SkinDefaults.TargetOfClassic(id) : (defs.TryGetValue(id, out SkinDef d) ? d.target : null);
        return target != null && EquippedFor(target) == id;
    }

    public int PriceOf(string id) => id != null && defs.TryGetValue(id, out SkinDef d) ? d.gemPrice : 0;

    public SkinBuyResult CheckBuy(string id, int gems)
    {
        if (IsOwned(id)) return SkinBuyResult.AlreadyOwned;
        if (id == null || !defs.TryGetValue(id, out SkinDef d)) return SkinBuyResult.Unknown;
        if (d.IsSetOnly) return SkinBuyResult.SetOnly;
        return gems >= d.gemPrice ? SkinBuyResult.Ok : SkinBuyResult.NotEnoughGems;
    }

    /// <summary>After the caller spent the gems: own it and wear it.</summary>
    public void MarkBought(string id)
    {
        if (id == null || !defs.ContainsKey(id)) return;
        owned.Add(id);
        Equip(id);
    }

    public void Grant(IEnumerable<string> ids)
    {
        if (ids == null) return;
        foreach (string id in ids) if (id != null && defs.ContainsKey(id)) owned.Add(id);
    }

    public bool Equip(string id)
    {
        if (SkinDefaults.IsClassic(id)) { equipped.Remove(SkinDefaults.TargetOfClassic(id)); return true; }
        if (id == null || !owned.Contains(id) || !defs.TryGetValue(id, out SkinDef d)) return false;
        equipped[d.target] = id;
        return true;
    }

    public string[] ExportOwned() => owned.OrderBy(s => s, StringComparer.Ordinal).ToArray();
    public string[] ExportEquipped() => equipped.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value).ToArray();

    public void Import(string[] ownedIds, string[] equippedPairs)
    {
        owned.Clear(); equipped.Clear();
        Grant(ownedIds);
        if (equippedPairs == null) return;
        foreach (string pair in equippedPairs)
        {
            if (string.IsNullOrEmpty(pair)) continue;
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            Equip(pair.Substring(eq + 1)); // validates ownership + derives the target from the skin
        }
    }
}
```

- [ ] **Step 4: Compile + run the suite → GREEN.** Expected: all `SkinOwnershipCoreTests` and `SkinDefaultsTests` pass, and the rest of the suite stays green.
- [ ] **Step 5: Checkpoint** (no commit).

---

### Task 2: Research gem Finish-now

**Files:**
- Create: `Assets/Scripts/EconomyCore/ResearchGemPrice.cs`
- Test: `Assets/Tests/EditMode/ResearchGemPriceTests.cs`
- Modify: `Assets/Scripts/Research/ResearchTuning.cs` (add `gemsPerHourToFinish = 20f`)
- Modify: `Assets/Scripts/ResearchManager.cs` (add `GetSecondsRemaining`, `GetFinishGemCost`, `TryFinishWithGems`)
- Modify: `Assets/Scripts/UI/CompostBoostModalUITK.cs` (static Finish row, built once)

**Interfaces:**
- Produces:
  - `static int ResearchGemPrice.GemsToFinish(double secondsLeft, float gemsPerHour)`
  - `ResearchManager.GetSecondsRemaining(int slot) : double`
  - `ResearchManager.GetFinishGemCost(int slot) : int` (0 = unavailable)
  - `ResearchManager.TryFinishWithGems(int slot) : bool`

- [ ] **Step 1: Failing tests**

```csharp
// Assets/Tests/EditMode/ResearchGemPriceTests.cs
using NUnit.Framework;

public class ResearchGemPriceTests
{
    [TestCase(0.0, 0)]
    [TestCase(-50.0, 0)]
    [TestCase(1.0, 1)]          // any time left costs at least 1
    [TestCase(180.0, 1)]        // 3 min at 20/h = 1
    [TestCase(181.0, 2)]        // ceil
    [TestCase(3600.0, 20)]
    [TestCase(86400.0, 480)]
    public void GemsToFinish_CeilsHoursTimesRate(double secs, int expected)
    {
        Assert.AreEqual(expected, ResearchGemPrice.GemsToFinish(secs, 20f));
    }

    [Test] public void GemsToFinish_ZeroRate_IsFree() => Assert.AreEqual(0, ResearchGemPrice.GemsToFinish(3600, 0f));
}
```

- [ ] **Step 2: RED** (compile error: `ResearchGemPrice` not found).

- [ ] **Step 3: Implement**

```csharp
// Assets/Scripts/EconomyCore/ResearchGemPrice.cs
using System;

/// <summary>Gem cost to finish the current research level now (spec §7): ceil(hours left x rate), min 1 when
/// any time is left, 0 when none (or the rate is 0). A repeatable, never-ending gem sink.</summary>
public static class ResearchGemPrice
{
    public static int GemsToFinish(double secondsLeft, float gemsPerHour)
    {
        if (secondsLeft <= 0 || gemsPerHour <= 0f) return 0;
        double gems = Math.Ceiling(secondsLeft / 3600.0 * gemsPerHour - 1e-9);
        return (int)Math.Max(1, gems);
    }
}
```

In `ResearchTuning.cs`, after the Tick Cadence header block, add:
```csharp
        [Header("Gem Finish-Now")]
        [Tooltip("Gems per hour of research left to finish a level instantly (spec 2026-10-05 §7). 20 => a day costs 480.")]
        public float gemsPerHourToFinish = 20f;
```

In `ResearchManager.cs`, after `GetSlotElapsedSeconds`, add:
```csharp
    /// <summary>Seconds left on the slot's current level (boost-aware); 0 for idle/paused/unknown.</summary>
    public double GetSecondsRemaining(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return 0;
        var s = slots[slotIndex];
        if (s == null || s.IsIdle || s.startUtcTicks <= 0) return 0;
        var rd = GetResearch(s.activeResearchID);
        if (rd == null || s.currentLevel >= rd.MaxLevel) return 0;
        float secs = GetSecondsForLevel(rd, s.currentLevel + 1);
        return Math.Max(0, secs - ComputeElapsedSeconds(s, DateTime.UtcNow.Ticks));
    }

    public int GetFinishGemCost(int slotIndex) =>
        ResearchGemPrice.GemsToFinish(GetSecondsRemaining(slotIndex), tuning != null ? tuning.gemsPerHourToFinish : 20f);

    /// <summary>Spend gems to complete the current level now: shifts the level's start back by the time left so
    /// the normal Tick path levels it up (auto-repeat, unlocks and events all unchanged).</summary>
    public bool TryFinishWithGems(int slotIndex)
    {
        int cost = GetFinishGemCost(slotIndex);
        if (cost <= 0 || CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendGems(cost)) return false;
        double remaining = GetSecondsRemaining(slotIndex);
        slots[slotIndex].startUtcTicks -= (long)Math.Ceiling(remaining * TimeSpan.TicksPerSecond) + TimeSpan.TicksPerMillisecond;
        Debug.Log($"[Research] Finished slot {slotIndex} now for {cost} gems ({remaining:F0}s left)");
        Tick();
        OnSlotStateChanged?.Invoke(slotIndex);
        return true;
    }
```

(Check that `IsValidSlot` exists. `GetSlot` uses it, so it does.)

In `CompostBoostModalUITK.cs`:
- Add fields: `private VisualElement finishRow; private Label finishCost;`
- At the end of `CacheAndWire()`, call `BuildFinishRow();`
- Add the methods below.
- In `Rebuild()`, after the active-banner block, call `RefreshFinishRow();`

```csharp
    // Gem Finish-now row: built ONCE above the boost list (Rebuild runs every second and would eat taps).
    private void BuildFinishRow()
    {
        if (boostList == null || boostList.parent == null || finishRow != null) return;
        finishRow = new VisualElement { name = "finish-now-row" };
        finishRow.AddToClassList("boost-row");
        var label = new Label("Finish now");
        label.AddToClassList("boost-row__label");
        var cost = new VisualElement();
        cost.AddToClassList("boost-row__cost");
        finishCost = new Label("0");
        cost.Add(finishCost);
        Sprite gem = MonetizationArt.Instance != null ? MonetizationArt.Instance.gemIcon : null;
        VisualElement icon = MonetizationUI.Icon(gem, 26);
        icon.style.marginLeft = 5;
        cost.Add(icon);
        finishRow.Add(label);
        finishRow.Add(cost);
        finishRow.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            if (finishRow.ClassListContains("boost-row--disabled")) return;
            if (ResearchManager.Instance != null && ResearchManager.Instance.TryFinishWithGems(targetSlotIndex)) Close();
        });
        VisualElement parent = boostList.parent;
        parent.Insert(parent.IndexOf(boostList), finishRow);
    }

    private void RefreshFinishRow()
    {
        if (finishRow == null) return;
        int cost = ResearchManager.Instance != null ? ResearchManager.Instance.GetFinishGemCost(targetSlotIndex) : 0;
        int gems = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gems : 0;
        finishRow.style.display = cost > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        finishCost.text = cost.ToString("N0");
        finishRow.EnableInClassList("boost-row--disabled", gems < cost);
    }
```

- [ ] **Step 4: Compile + suite GREEN** (the new `ResearchGemPriceTests` pass).
- [ ] **Step 5: Checkpoint.**

---

### Task 3: Store products: sets, starter, Harvest Blessing; StoreManager ownership + skin grants; Plant hook

**Files:**
- Modify: `Assets/Scripts/EconomyCore/StoreDefaults.cs` (append `Sets`, `Boosts`; `grantsSkinIds`; new products; drop the retired teasers)
- Modify: `Assets/Tests/EditMode/StoreDefaultsTests.cs`
- Modify: `Assets/Scripts/Monetization/StoreManager.cs` (generic non-consumable ownership, skin grants, `HarvestCoinMultiplier`)
- Modify: `Assets/Scripts/GameData.cs` (`ownedProductIds`)
- Modify: `Assets/Scripts/Plant.cs:329-331` (apply the multiplier)
- Modify: `Assets/Editor/MonetizationTools.cs` (BuildAssets: retire ids, icons for new products)

**Interfaces:**
- Produces:
  - `StoreSection.Sets` / `StoreSection.Boosts`
  - `StoreProductDef.grantsSkinIds`
  - Constants `StoreDefaults.StarterId = "bundle_starter"`, `HarvestBlessingId = "boost_harvest_blessing"`, `HarvestBlessingMultiplier = 1.25f`, `RetiredIds`
  - `StoreManager.IsProductOwned(string)`, `static float StoreManager.HarvestCoinMultiplier`
- Consumes (Task 4, called by name; compiles once Task 4 lands): `SkinManager.Instance?.Grant(string[])`. **Order note:** Task 3's StoreManager edit references `SkinManager`, so implement Task 4's `SkinManager` stub signature first if compiling in between. Simplest is to do Steps 3-5 of this task after Task 4 Step 3. The executor may reorder; ledger it.

- [ ] **Step 1: Failing test updates.** Replace `Pass_IsTheOnlyBuyableNonConsumable_With500Gems` and `ComingSoon_HasFourDisabledTeasers` in `StoreDefaultsTests.cs` with:

```csharp
    [Test]
    public void Pass_Has500Gems_AndIsNonConsumable()
    {
        var pass = StoreDefaults.Products.Single(p => p.id == StoreDefaults.PassId);
        Assert.AreEqual(ProductKind.NonConsumable, pass.kind);
        Assert.AreEqual(500, pass.gems);
        Assert.AreEqual("$9.99", pass.fallbackPrice);
        Assert.AreEqual(StoreDefaults.PassDescription, pass.description);
    }

    [Test]
    public void NewProducts_MatchSpec()
    {
        var golden = StoreDefaults.Products.Single(p => p.id == SkinDefaults.GoldenSet);
        Assert.AreEqual("$4.99", golden.fallbackPrice);
        CollectionAssert.AreEquivalent(new[] { "chicken_golden", "rooster_golden", "house_yellow_barn" }, golden.grantsSkinIds);
        var puppy = StoreDefaults.Products.Single(p => p.id == SkinDefaults.PuppySet);
        Assert.AreEqual("$2.99", puppy.fallbackPrice);
        CollectionAssert.AreEquivalent(new[] { "dog_lab_brown", "dog_lab_dark", "dog_lab_white" }, puppy.grantsSkinIds);
        var starter = StoreDefaults.Products.Single(p => p.id == StoreDefaults.StarterId);
        Assert.AreEqual(800, starter.gems);
        CollectionAssert.AreEqual(new[] { "house_cottage" }, starter.grantsSkinIds);
        var blessing = StoreDefaults.Products.Single(p => p.id == StoreDefaults.HarvestBlessingId);
        Assert.AreEqual(StoreSection.Boosts, blessing.section);
        Assert.AreEqual("$9.99", blessing.fallbackPrice);
        Assert.AreEqual(1.25f, StoreDefaults.HarvestBlessingMultiplier);
        foreach (var p in new[] { golden, puppy, starter, blessing }) Assert.AreEqual(ProductKind.NonConsumable, p.kind, p.id);
    }

    [Test]
    public void EverySetOnlySkin_IsGrantedByItsSet()
    {
        foreach (var s in SkinDefaults.All.Where(s => s.IsSetOnly))
        {
            var set = StoreDefaults.Products.SingleOrDefault(p => p.id == s.setId);
            Assert.IsNotNull(set, s.id);
            CollectionAssert.Contains(set.grantsSkinIds, s.id);
        }
    }

    [Test]
    public void ComingSoon_IsFarmThemesOnly_AndRetiredTeasersGone()
    {
        var soon = StoreDefaults.Products.Where(p => p.section == StoreSection.ComingSoon).ToArray();
        CollectionAssert.AreEqual(new[] { "soon_farm_themes" }, soon.Select(p => p.id).ToArray());
        foreach (string retired in StoreDefaults.RetiredIds) Assert.IsFalse(StoreDefaults.Products.Any(p => p.id == retired), retired);
    }

    [Test]
    public void EnumValues_AreAppendOnly()
    {
        Assert.AreEqual(0, (int)StoreSection.Pass); Assert.AreEqual(1, (int)StoreSection.Gems);
        Assert.AreEqual(2, (int)StoreSection.ComingSoon); Assert.AreEqual(3, (int)StoreSection.Sets);
        Assert.AreEqual(4, (int)StoreSection.Boosts);
    }
```

- [ ] **Step 2: RED** (compile errors: `Sets`, `grantsSkinIds`, `StarterId` missing).

- [ ] **Step 3: StoreDefaults changes**
  - `public enum StoreSection { Pass, Gems, ComingSoon, Sets, Boosts }`
  - In `StoreProductDef`, add `[Tooltip("Skins this purchase unlocks (SkinDefaults ids).")] public string[] grantsSkinIds = new string[0];`
  - In `StoreDefaults`, add:

```csharp
    public const string StarterId = "bundle_starter";
    public const string HarvestBlessingId = "boost_harvest_blessing";
    public const float HarvestBlessingMultiplier = 1.25f;
    /// <summary>Ids removed from the catalog (Build Assets deletes them from StoreCatalog.asset).</summary>
    public static readonly string[] RetiredIds = { "soon_harvest_blessing", "soon_animal_skins", "soon_building_skins" };
```

  Replace the four `Soon(...)` lines in `Products` with:

```csharp
        new StoreProductDef { id = SkinDefaults.GoldenSet, displayName = "Golden Farm Set", kind = ProductKind.NonConsumable,
            section = StoreSection.Sets, fallbackPrice = "$4.99", description = "Golden hen, golden rooster and a golden barn.",
            ribbon = "Set", grantsSkinIds = new[] { "chicken_golden", "rooster_golden", "house_yellow_barn" } },
        new StoreProductDef { id = SkinDefaults.PuppySet, displayName = "Puppy Pack", kind = ProductKind.NonConsumable,
            section = StoreSection.Sets, fallbackPrice = "$2.99", description = "Three Labradors for your farm dog.",
            ribbon = "Set", grantsSkinIds = new[] { "dog_lab_brown", "dog_lab_dark", "dog_lab_white" } },
        new StoreProductDef { id = StarterId, displayName = "Starter Bundle", kind = ProductKind.NonConsumable,
            section = StoreSection.Sets, gems = 800, fallbackPrice = "$2.99", description = "800 gems and the Cottage farmhouse.",
            ribbon = "Once", grantsSkinIds = new[] { "house_cottage" } },
        new StoreProductDef { id = HarvestBlessingId, displayName = "Harvest Blessing", kind = ProductKind.NonConsumable,
            section = StoreSection.Boosts, fallbackPrice = "$9.99", description = "+25% Coins from every harvest, forever.", ribbon = "" },
        Soon("soon_farm_themes", "Farm Themes", "A whole new feel."),
```

  In `Bundle(...)` and `Soon(...)`, set `grantsSkinIds = new string[0]`. Also set it on the pass entry.

- [ ] **Step 4: GameData + StoreManager.** In `GameData.cs`, after `deliveredTransactionIds`, add `public string[] ownedProductIds;`

In `StoreManager.cs`:
- Add the field `private readonly HashSet<string> ownedProducts = new HashSet<string>();` (`using System.Collections.Generic;`).
- Add:

```csharp
    /// <summary>Permanent purchases. The pass keeps its own legacy flag (farmersPassOwned).</summary>
    public bool IsProductOwned(string productId) =>
        productId == StoreDefaults.PassId ? passOwned : (productId != null && ownedProducts.Contains(productId));

    /// <summary>Harvest Coins multiplier from Harvest Blessing (1 when not owned).</summary>
    public static float HarvestCoinMultiplier =>
        Instance != null && Instance.IsProductOwned(StoreDefaults.HarvestBlessingId) ? StoreDefaults.HarvestBlessingMultiplier : 1f;

    private void SetProductOwned(string productId, bool owned)
    {
        if (productId == StoreDefaults.PassId) { passOwned = owned; return; }
        if (owned) ownedProducts.Add(productId); else ownedProducts.Remove(productId);
    }
```

- `Purchase`: replace `if (productId == StoreDefaults.PassId && passOwned) return;` with `if (def.kind == ProductKind.NonConsumable && IsProductOwned(productId)) return;`.
- `Deliver`: replace the body from `bool isPass` through the reveal with:

```csharp
        bool isPass = def.id == StoreDefaults.PassId;
        bool permanent = def.kind == ProductKind.NonConsumable;
        bool ledgerNew = ledger.TryMarkDelivered(result.transactionId);
        bool grant = PurchaseLedgerCore.ShouldGrant(ledgerNew, permanent, permanent && IsProductOwned(def.id));

        if (permanent) SetProductOwned(def.id, true);
        if (isPass) FreeGiftManager.Instance?.MarkPitchShown();
        if (def.grantsSkinIds != null && def.grantsSkinIds.Length > 0) SkinManager.Instance?.Grant(def.grantsSkinIds); // idempotent: restores skins too
        if (grant && def.gems > 0) CurrencyManager.Instance?.AddGems(def.gems);
        Debug.Log($"[Store] Delivered {def.id} (tx {result.transactionId}) grant={grant}");

        SaveManager.Instance?.SaveGame();
        MonetizationServices.Store.ConfirmDelivered(result.transactionId);
        OnEntitlementsChanged?.Invoke();
        FreeGiftManager.Instance?.NotifyChanged(); // HUD tag drops "AD" once the pass is owned

        if (!grant) return;
        var request = new ChestRevealRequest { banner = isPass ? PassBanner : (permanent ? def.displayName + " unlocked!" : null) };
        if (def.gems > 0) request.lines.Add(new RewardLine(RewardCurrency.Gems, def.gems));
        ChestRevealUITK.Show(request); // granted already; the chest is the celebration
```

- `InitStore` reconcile: after the pass block, add a loop over the catalog's other non-consumables (excluding the pass and `comingSoon`). When `store.IsOwned(id) == true` and it isn't owned locally: `SetProductOwned(id, true)` and `SkinManager.Instance?.Grant(def.grantsSkinIds)`. When it's `false` and owned locally: `SetProductOwned(id, false)`. Then save and invoke `OnEntitlementsChanged` if anything changed.
- `CaptureTo`: `d.ownedProductIds = new List<string>(ownedProducts).ToArray();`
- `LoadFrom`: `ownedProducts.Clear(); if (d.ownedProductIds != null) foreach (var id in d.ownedProductIds) if (!string.IsNullOrEmpty(id)) ownedProducts.Add(id);`
- `DevResetPass`: also `ownedProducts.Clear();`. Rename the Settings row text to "Reset Store Purchases".

`Plant.cs`: after `coinGain *= goldenMultiplier;`, add:
```csharp
            coinGain = Mathf.RoundToInt(coinGain * StoreManager.HarvestCoinMultiplier); // Harvest Blessing (Store)
```

`MonetizationTools.BuildAssets`:
- Before the append loop: `products.RemoveAll(p => p != null && System.Array.IndexOf(StoreDefaults.RetiredIds, p.id) >= 0);`
- Icons:
  - `p.section == StoreSection.Boosts` gets `LoadSprite("Assets/Sprites/UI/Icons/Cute/RpgThings/CoinSack_Brown.png")`.
  - The `StarterId` entry gets `chestClosed`.
  - Sets stay null (the Store draws previews of their skins).
- For existing entries whose `grantsSkinIds` is null or empty, copy them from the matching default.

- [ ] **Step 5: Compile + GREEN; run Build Assets** via `Temp/menu.request`. Verify `StoreCatalog.asset` has `set_golden_farm`, `set_puppy_pack`, `bundle_starter`, `boost_harvest_blessing`, and none of the retired ids.
- [ ] **Step 6: Checkpoint.**

---

### Task 4: SkinCatalogSO + SkinManager + save + catalog menu

**Files:**
- Create: `Assets/Scripts/Monetization/Skins/SkinCatalogSO.cs`
- Create: `Assets/Scripts/Monetization/Skins/SkinManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (`ownedSkinIds`, `equippedSkins`)
- Modify: `Assets/Scripts/SaveManager.cs` (capture/load next to StoreManager)
- Modify: `Assets/Editor/MonetizationTools.cs` (`Build Skin Catalog` menu)

**Interfaces:**
- `SkinCatalogSO { static Instance; SkinDef[] skins; SkinDef Get(id); }` (Resources/SkinCatalog; falls back to `SkinDefaults.All` with no art)
- `SkinManager`:
  - `static Instance`, `event Action OnSkinsChanged`
  - `IReadOnlyList<SkinDef> All`, `SkinDef Get(id)`, `SkinDef EquippedDef(target)` (null = Classic)
  - Queries: `bool IsOwned(id)`, `bool IsEquipped(id)`
  - Actions: `SkinBuyResult TryBuy(id)`, `bool Equip(id)`, `void Grant(string[] ids)`
  - Save: `CaptureTo`/`LoadFrom`
  - Dev: `DevResetSkins()`

- [ ] **Step 1: Write SkinCatalogSO**

```csharp
// Assets/Scripts/Monetization/Skins/SkinCatalogSO.cs
using UnityEngine;

/// <summary>Skins with their art (Resources/SkinCatalog). Built by Farm Game > Monetization > Build Skin Catalog,
/// which appends missing SkinDefaults ids and never overwrites edited prices/names.</summary>
public class SkinCatalogSO : ScriptableObject
{
    public SkinDef[] skins = new SkinDef[0];

    private static SkinCatalogSO cached;
    public static SkinCatalogSO Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<SkinCatalogSO>("SkinCatalog");
            if (cached == null)
            {
                Debug.LogWarning("[Skins] Resources/SkinCatalog missing; using SkinDefaults without art. Run Farm Game > Monetization > Build Skin Catalog.");
                cached = CreateInstance<SkinCatalogSO>();
                cached.skins = SkinDefaults.All;
            }
            return cached;
        }
    }

    public SkinDef Get(string id)
    {
        if (skins == null || string.IsNullOrEmpty(id)) return null;
        foreach (SkinDef s in skins) if (s != null && s.id == id) return s;
        return null;
    }
}
```

- [ ] **Step 2: Write SkinManager**

```csharp
// Assets/Scripts/Monetization/Skins/SkinManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Skin ownership + equip (spec 2026-10-05 §5). Gem purchases spend here, real-money sets grant via
/// StoreManager. Saves in GameData. Self-bootstrapping per scene with a save (like StoreManager); attaches the
/// farmhouse applier on Start. Animal visuals get a SkinSwapper from AnimalManager.</summary>
public sealed class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }
    public event Action OnSkinsChanged;

    private SkinOwnershipCore core;
    private SkinCatalogSO catalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (Instance != null || SaveManager.Instance == null) return;
        new GameObject("SkinManager").AddComponent<SkinManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        catalog = SkinCatalogSO.Instance;
        core = new SkinOwnershipCore(catalog.skins);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        BarnBuilding house = FindFirstObjectByType<BarnBuilding>(FindObjectsInactive.Include);
        if (house != null && house.GetComponent<BuildingSkinApplier>() == null)
            house.gameObject.AddComponent<BuildingSkinApplier>().Init("farmhouse");
    }

    public IReadOnlyList<SkinDef> All => catalog.skins;
    public SkinDef Get(string id) => catalog.Get(id);
    public bool IsOwned(string id) => core.IsOwned(id);
    public bool IsEquipped(string id) => core.IsEquipped(id);

    /// <summary>The equipped skin's def for a target, or null for Classic.</summary>
    public SkinDef EquippedDef(string target)
    {
        string id = core.EquippedFor(target);
        return SkinDefaults.IsClassic(id) ? null : catalog.Get(id);
    }

    public SkinBuyResult TryBuy(string id)
    {
        CurrencyManager cm = CurrencyManager.Instance;
        SkinBuyResult r = core.CheckBuy(id, cm != null ? cm.Gems : 0);
        if (r != SkinBuyResult.Ok) return r;
        if (!cm.SpendGems(core.PriceOf(id))) return SkinBuyResult.NotEnoughGems;
        core.MarkBought(id);
        Debug.Log($"[Skins] Bought + equipped {id} for {core.PriceOf(id)} gems");
        Changed();
        return r;
    }

    public bool Equip(string id)
    {
        if (!core.Equip(id)) return false;
        Changed();
        return true;
    }

    public void Grant(string[] ids)
    {
        core.Grant(ids);
        Changed();
    }

    private void Changed()
    {
        SaveManager.Instance?.SaveGame();
        OnSkinsChanged?.Invoke();
    }

    public void CaptureTo(GameData d)
    {
        d.ownedSkinIds = core.ExportOwned();
        d.equippedSkins = core.ExportEquipped();
    }

    public void LoadFrom(GameData d)
    {
        core.Import(d.ownedSkinIds, d.equippedSkins);
        OnSkinsChanged?.Invoke();
    }

    public void DevResetSkins()
    {
        core.Import(null, null);
        Changed();
    }
}
```

- [ ] **Step 3: GameData + SaveManager.** In `GameData.cs`, after `ownedProductIds`, add `public string[] ownedSkinIds; public string[] equippedSkins;`. In `SaveManager.cs`:
  - **Capture:** `if (SkinManager.Instance != null) SkinManager.Instance.CaptureTo(data);` (after StoreManager).
  - **Load:** `if (SkinManager.Instance != null) SkinManager.Instance.LoadFrom(data);` before the StoreManager load, so a restored set can grant into a loaded core.

- [ ] **Step 4: Build Skin Catalog menu** (in `MonetizationTools`):

```csharp
    private const string SkinCatalogPath = "Assets/Resources/SkinCatalog.asset";

    [MenuItem("Farm Game/Monetization/Build Skin Catalog")]
    public static void BuildSkinCatalog()
    {
        var cat = AssetDatabase.LoadAssetAtPath<SkinCatalogSO>(SkinCatalogPath);
        if (cat == null) { cat = ScriptableObject.CreateInstance<SkinCatalogSO>(); AssetDatabase.CreateAsset(cat, SkinCatalogPath); }
        var list = new List<SkinDef>(cat.skins ?? new SkinDef[0]);
        int added = 0, missing = 0;
        foreach (SkinDef d in SkinDefaults.All)
            if (list.TrueForAll(s => s == null || s.id != d.id)) { list.Add(d); added++; }
        foreach (SkinDef s in list)
        {
            if (s == null) continue;
            if (s.kind == SkinKind.AnimalSheet && s.texture == null) s.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(s.assetPath);
            if (s.kind == SkinKind.BuildingSprite && s.sprite == null) s.sprite = LoadSprite(s.assetPath);
            if ((s.kind == SkinKind.AnimalSheet && s.texture == null) || (s.kind == SkinKind.BuildingSprite && s.sprite == null))
            { missing++; Debug.LogWarning("[Skins] Art not found for " + s.id + " at " + s.assetPath); }
        }
        cat.skins = list.ToArray();
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Monetization] Built skin catalog: {cat.skins.Length} skins (+{added}), {missing} missing art.");
    }
```

- [ ] **Step 5: Compile** (Task 5's `BuildingSkinApplier` is referenced, so add Task 5's files before compiling, or temporarily stub `BuildingSkinApplier` with `Init(string)`; ledger it). **Run Build Skin Catalog** and expect `[Monetization] Built skin catalog: 34 skins (+34), 0 missing art.` Then GREEN.
- [ ] **Step 6: Checkpoint.**

---

### Task 5: SkinSwapper (animals) + BuildingSkinApplier (farmhouse) + spawn hook + previews

**Files:**
- Create: `Assets/Scripts/Monetization/Skins/SkinSwapper.cs`
- Create: `Assets/Scripts/Monetization/Skins/BuildingSkinApplier.cs`
- Create: `Assets/Scripts/Monetization/Skins/SkinArt.cs` (preview sprites for the Store)
- Modify: `Assets/Scripts/AnimalManager.cs` (`SpawnAnimalVisual`: attach the swapper)

**Interfaces:**
- `SkinSwapper.Attach(GameObject visual, string target)`
- `BuildingSkinApplier.Init(string target)`, `static Sprite FarmhouseBase`
- `SkinArt.Preview(SkinDef def)` / `SkinArt.ClassicPreview(string target)` → `Sprite` (cached)

- [ ] **Step 1: SkinSwapper**

```csharp
// Assets/Scripts/Monetization/Skins/SkinSwapper.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws an animal in its equipped colour variant. Variant sheets share the base sheet's exact pixel
/// layout, so each base frame maps to a sprite cut from the skin texture with the SAME rect, pivot and PPU.
/// Runs in LateUpdate, after the Animator has written this frame's sprite. Other textures (eggs, etc.) pass through.</summary>
public sealed class SkinSwapper : MonoBehaviour
{
    private string target;
    private SpriteRenderer sr;
    private Texture2D baseTexture;
    private Texture2D skinTexture;
    private readonly Dictionary<Sprite, Sprite> toSkin = new Dictionary<Sprite, Sprite>();
    private readonly Dictionary<Sprite, Sprite> toBase = new Dictionary<Sprite, Sprite>();
    private bool subscribed;

    public static void Attach(GameObject visual, string target)
    {
        if (visual == null || string.IsNullOrEmpty(target)) return;
        var s = visual.GetComponent<SkinSwapper>() ?? visual.AddComponent<SkinSwapper>();
        s.target = target;
        s.sr = visual.GetComponentInChildren<SpriteRenderer>();
        s.baseTexture = s.sr != null && s.sr.sprite != null ? s.sr.sprite.texture : null;
        s.Refresh();
    }

    private void OnEnable() => TrySubscribe();

    private void TrySubscribe()
    {
        if (subscribed || SkinManager.Instance == null) return;
        SkinManager.Instance.OnSkinsChanged += Refresh;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && SkinManager.Instance != null) SkinManager.Instance.OnSkinsChanged -= Refresh;
        subscribed = false;
    }

    private void OnDestroy()
    {
        foreach (Sprite s in toSkin.Values) if (s != null) Destroy(s);
    }

    private void Refresh()
    {
        TrySubscribe();
        SkinDef def = SkinManager.Instance != null ? SkinManager.Instance.EquippedDef(target) : null;
        Texture2D next = def != null ? def.texture : null;
        if (next != null && baseTexture != null && (next.width != baseTexture.width || next.height != baseTexture.height))
        {
            Debug.LogWarning($"[Skins] {def.id} texture {next.width}x{next.height} doesn't match {target}'s base {baseTexture.width}x{baseTexture.height}; showing Classic.");
            next = null;
        }
        if (next == skinTexture) return;
        // Snap a currently-shown skin frame back to its base frame before switching textures.
        if (sr != null && sr.sprite != null && toBase.TryGetValue(sr.sprite, out Sprite b)) sr.sprite = b;
        foreach (Sprite s in toSkin.Values) if (s != null) Destroy(s);
        toSkin.Clear(); toBase.Clear();
        skinTexture = next;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (skinTexture == null || sr == null) return;
        Sprite s = sr.sprite;
        if (s == null || s.texture != baseTexture) return;
        if (!toSkin.TryGetValue(s, out Sprite skinned))
        {
            Rect r = s.rect;
            skinned = Sprite.Create(skinTexture, r, new Vector2(s.pivot.x / r.width, s.pivot.y / r.height), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            skinned.name = s.name + "_skin";
            toSkin[s] = skinned;
            toBase[skinned] = s;
        }
        sr.sprite = skinned;
    }
}
```

- [ ] **Step 2: BuildingSkinApplier**

```csharp
// Assets/Scripts/Monetization/Skins/BuildingSkinApplier.cs
using UnityEngine;

/// <summary>Swaps a building's sprite for its equipped skin, drawn at the base sprite's world WIDTH with the base
/// edge kept where it was (taller skins grow upward). Transform, collider and BarnBuilding's press tween are untouched.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BuildingSkinApplier : MonoBehaviour
{
    public static Sprite FarmhouseBase { get; private set; }

    private string target;
    private SpriteRenderer sr;
    private Sprite baseSprite;
    private Sprite fitted;
    private string fittedId;
    private bool subscribed;

    public void Init(string target)
    {
        this.target = target;
        sr = GetComponent<SpriteRenderer>();
        baseSprite = sr.sprite;
        if (target == "farmhouse") FarmhouseBase = baseSprite;
        TrySubscribe();
        Apply();
    }

    private void OnEnable() => TrySubscribe();

    private void TrySubscribe()
    {
        if (subscribed || SkinManager.Instance == null || target == null) return;
        SkinManager.Instance.OnSkinsChanged += Apply;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && SkinManager.Instance != null) SkinManager.Instance.OnSkinsChanged -= Apply;
        subscribed = false;
    }

    private void OnDestroy() { if (fitted != null) Destroy(fitted); }

    private void Apply()
    {
        if (sr == null || baseSprite == null) return;
        SkinDef def = SkinManager.Instance != null ? SkinManager.Instance.EquippedDef(target) : null;
        if (def == null || def.sprite == null) { sr.sprite = baseSprite; return; }
        if (fittedId != def.id)
        {
            if (fitted != null) Destroy(fitted);
            fitted = Fit(def.sprite);
            fittedId = def.id;
        }
        sr.sprite = fitted;
    }

    private Sprite Fit(Sprite skin)
    {
        float baseW = baseSprite.rect.width / baseSprite.pixelsPerUnit;
        float baseH = baseSprite.rect.height / baseSprite.pixelsPerUnit;
        float ppu = skin.rect.width / baseW;                 // same world width as the base
        float skinH = skin.rect.height / ppu;
        Vector2 basePivot = new Vector2(baseSprite.pivot.x / baseSprite.rect.width, baseSprite.pivot.y / baseSprite.rect.height);
        // Keep the bottom edge: base bottom sits basePivot.y*baseH below the pivot -> solve for the skin's pivot.y.
        Vector2 pivot = new Vector2(basePivot.x, basePivot.y * baseH / Mathf.Max(0.0001f, skinH));
        Sprite s = Sprite.Create(skin.texture, skin.rect, pivot, ppu, 0, SpriteMeshType.FullRect);
        s.name = skin.name + "_fitted";
        return s;
    }
}
```

- [ ] **Step 3: SkinArt (Store previews)**

```csharp
// Assets/Scripts/Monetization/Skins/SkinArt.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>Preview sprites for Store cards: animals = the AnimalData icon frame's rect cut from the variant
/// texture; buildings = the skin sprite; Classic = the base art. Cached for the session.</summary>
public static class SkinArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => cache.Clear();

    public static Sprite ClassicPreview(string target)
    {
        if (target == "farmhouse") return BuildingSkinApplier.FarmhouseBase;
        AnimalData data = AnimalManager.Instance != null ? AnimalManager.Instance.GetAnimalData(target) : null;
        return data != null ? data.iconSprite : null;
    }

    public static Sprite Preview(SkinDef def)
    {
        if (def == null) return null;
        if (def.kind == SkinKind.BuildingSprite) return def.sprite;
        if (cache.TryGetValue(def.id, out Sprite s) && s != null) return s;
        Sprite icon = ClassicPreview(def.target);
        if (icon == null || def.texture == null || icon.texture.width != def.texture.width || icon.texture.height != def.texture.height) return icon;
        Rect r = icon.rect;
        s = Sprite.Create(def.texture, r, new Vector2(icon.pivot.x / r.width, icon.pivot.y / r.height), icon.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        cache[def.id] = s;
        return s;
    }
}
```

- [ ] **Step 4: Spawn hook.** In `AnimalManager.SpawnAnimalVisual`, after `visual.Initialize(data);`:
```csharp
        SkinSwapper.Attach(activeVisualInstance, data.animalID); // equipped colour variant (Store skins)
```

- [ ] **Step 5: Compile + GREEN; Checkpoint.**

---

### Task 6: Dev/test hooks

**Files:**
- Modify: `Assets/Editor/MonetizationTestBridge.cs`. Add these commands:
  - `skin <id>` → `SkinManager.TryBuy`
  - `equip <id>`
  - `grant <id>`
  - `resetskins`
  - `tab <Featured|Gems|Skins|Boosts>` → `StorePopupUITK.Open(tab)`
  - `research` → open the Research popup if `ResearchPopupUITK` has a public `Open()`
  - `finish <slot>` → `ResearchManager.TryFinishWithGems`

  Extend the status output with the equipped skin per target and the owned product ids.
- Modify: `Assets/Scripts/UI/SettingsPopupUITK.cs` (Dev: "Reset Skins" row; rename "Reset Farmer's Pass" to "Reset Store Purchases").

- [ ] **Step 1:** Add the commands (same pattern as the existing switch).
- [ ] **Step 2:** Compile; Checkpoint.

---

### Task 7: Store tabs + Skins grid UI

**Files:**
- Rewrite: `Assets/Scripts/Monetization/UI/StorePopupUITK.cs`

**Interfaces:**
- `public enum StoreTab { Featured, Gems, Skins, Boosts }`
- `StorePopupUITK.Open()` (last tab), `StorePopupUITK.Open(StoreTab tab)`, `Close()`, `IsOpen`

**Behaviour:**
- **Header:** a pixel "Store" title and an X close button.
- **Tab row:** 4 pill tabs named `store-tab-<name>`. The active tab is walnut with cream text; inactive tabs are parchment with ink text. The active tab is remembered in a static field.
- **Content:** one `ScrollView` (walnut scroller), cleared and rebuilt by `ShowTab`. The footer (Restore + fine print) is appended at the end of each tab.
- **Featured:**
  - Gift card (as v1).
  - Pass card (as v1).
  - **Golden Farm Set spotlight:** a card with the 3 previews in a row, the name, the description, and a price button. Once bought it shows "Owned".
  - **Starter Bundle row:** hidden if owned.
- **Gems:** the 5 bundle rows (as v1).
- **Skins:**
  - Chip row with `Animals` / `Buildings` (static toggle).
  - Then, for each target group (chicken, rooster, cow, pig, farm_dog under Animals; farmhouse under Buildings): a `PixelText` header ("Chicken", "Rooster", "Cow", "Pig", "Dog", "Farmhouse") and a wrap row holding `SkinCard`s at `width: 48%` with a margin of 1%.
  - The first card is Classic, then skins by `sortOrder`.
  - At the end of Animals: a **Puppy Pack** row (set purchase).
- **SkinCard:**
  - A parchment tile with a 120px preview (`SkinArt`), the name, and an action button named `skin-<id>`.
  - States, refreshed in place by `RefreshSkinCards()` on `OnSkinsChanged`, `OnGemsChanged` and `OnEntitlementsChanged`:
    - **Equipped:** gold 4px border, label "Equipped", no button.
    - **Owned:** button "Equip".
    - **Set-only:** button showing the set's price, plus a "Set" ribbon. Tapping it calls `StoreManager.Purchase(setId)`.
    - **Buyable:** button showing the gem amount (`N0`) with a gem icon, dimmed via `SetEnabled(false)` when gems are short.
  - **Buy confirm:** the first tap sets `armedSkinId` and the label "Buy?". A second tap within 3s calls `SkinManager.TryBuy`. On `NotEnoughGems`, show the toast "Not enough gems." with the chest icon.
- **Boosts:**
  - Harvest Blessing row: CoinSack icon, name, description, price or "Owned".
  - Coming-soon rows (Farm Themes).
- Events subscribed in `Subscribe()`: StoreManager `OnEntitlementsChanged` → `RefreshAll`; FreeGiftManager `OnStateChanged` → `RefreshGift`; SkinManager `OnSkinsChanged` → `RefreshSkinCards`; CurrencyManager `OnGemsChanged` → `RefreshSkinCards` (lambda kept in a field for unsubscribe).

- [ ] **Step 1: Rewrite StorePopupUITK** with the behaviour above. Reuse the v1 builders (`BuildGiftCard`, `BuildPassCard`, `BuildBundleRow`, `BuildSoonRow`, `BuildFooter`, `Panel`, `SectionTitle`). Add `BuildTabs`, `ShowTab`, `BuildSetSpotlight`, `BuildSetRow`, `BuildSkinsTab`, `BuildSkinGroup`, `BuildSkinCard` and `RefreshSkinCards`. Price buttons for real-money products stay in `priceButtons`, and `RefreshAll` sets their text and hides owned ones (an "Owned" label instead).
- [ ] **Step 2: Compile + GREEN; Checkpoint.**

---

### Task 8: Play-test (every Review Focus item)

Back up the save to `gamedata.pre-skins-test.json` (+.bak). Use `MonetizationTestBridge` + `UIDriveBridge` + `shot.sh`. Add gems with Settings "Grant Gems" via `monet.request` if needed (or `CurrencyManager.AddGems` through a `gems <n>` bridge command added in Task 6).

- [ ] 1. Open each tab (`tab Featured|Gems|Skins|Boosts`) and screenshot each. Check that the layout is in browns and the grid has 2 columns with real previews.
- [ ] 2. Skins → Animals: tap a Cow card once (shows "Buy?"), wait 4s (reverts), tap twice quickly → gems −1,000 or −1,500, card shows "Equipped". The cow in the world is the new colour while walking, idling and eating; screenshot the world. *(Focus 4.)*
- [ ] 3. Equip Classic while the cow walks → base colours return immediately, with no stale frame. *(Focus 1.)*
- [ ] 4. Buildings: buy the Ranch House → the farmhouse swaps, its base stays aligned, and tapping it still opens the Barn. Then Classic → the original returns.
- [ ] 5. Golden Farm Set → test store Confirm → chest with banner "Golden Farm Set unlocked!" → 3 skins owned; equip the Golden Hen. Restore → no duplicate grant, skins still owned. *(Focus 3.)*
- [ ] 6. Harvest Blessing purchase → harvest a crop in a run and confirm the Coins pop is ×1.25 (console or floating text).
- [ ] 7. Research: open the boost modal on an active slot → the "Finish now" row shows a gem cost; tap it → level completes, gems drop by the cost, and the modal closes. Idle slot → row hidden. *(Focus 5.)*
- [ ] 8. Exit and re-enter play → skins, equipped state and owned products persist.
- [ ] 9. Exit play, **confirm edit mode**, restore the save backup, clear the fake store account (Settings dev "Reset Store Purchases" or delete `fake_store_owned` in PlayerPrefs), run the suite: GREEN.
