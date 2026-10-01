using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AlmanacKind { Crop, Equipment, Animal, Pest }

/// <summary>One page of the Farmer's Almanac, built fresh each time it's shown (so stats are live).</summary>
public class AlmanacEntry
{
    public AlmanacKind kind;
    public string id;
    public string name;
    public Sprite icon;
    public bool unlocked = true;
    public string unlockHint;
    public string blurb;
    /// <summary>Type pills under the name (tap one to learn what it means).</summary>
    public readonly List<Pill> pills = new List<Pill>();
    /// <summary>Short bullet facts under the description (pest pages: what stops them).</summary>
    public readonly List<string> facts = new List<string>();
    public readonly List<StatLine> stats = new List<StatLine>();
    /// <summary>Crop pages: (pest name, damage multiplier). Pest pages: (crop name, multiplier).</summary>
    public readonly List<(string label, float appetite)> appetites = new List<(string, float)>();
    public readonly List<string> notes = new List<string>();

    /// <summary>Growth timeline (crop: how it grows; pest: damage by stage). Empty = none.</summary>
    public string timelineTitle;
    public readonly List<TimelineStage> stages = new List<TimelineStage>();
    /// <summary>Text in the gap between stage i and i+1 (crop pages: how long that step takes).</summary>
    public readonly List<string> stageGaps = new List<string>();
    public string timelineFooter;
}

/// <summary>One node on an Almanac growth timeline.</summary>
public struct TimelineStage
{
    public string name;
    public Sprite sprite;
    public string note;   // line under the name ("x2 damage", "pick within 40s"), or null
    public float heat;    // 0..1 tints the note from green (harmless) to red (hit hard); < 0 = plain
}

/// <summary>
/// Builds Almanac pages from live game data. Numbers come from CropStats / EquipmentStats (the same
/// code gameplay uses); blurbs from the LetterCatalog copy (`almanac_*` tip ids, see
/// docs/narrative/cast-and-copy.md) with the asset's own description as fallback.
/// </summary>
public static class AlmanacCatalog
{
    public static string Slug(string s) => (s ?? "").Trim().ToLowerInvariant().Replace(' ', '_');

    private static string Copy(string id, string fallback)
    {
        string text = OnboardingTutorials.Text(id);
        return string.IsNullOrWhiteSpace(text) ? fallback ?? "" : text;
    }

    public static List<AlmanacEntry> Build(AlmanacKind kind)
    {
        switch (kind)
        {
            case AlmanacKind.Crop: return Crops().Select(CropEntry).ToList();
            case AlmanacKind.Equipment: return EquipmentList().Select(EquipmentEntry).ToList();
            case AlmanacKind.Animal: return AnimalList().Select(AnimalEntry).ToList();
            default: return Pests().Select(PestEntry).ToList();
        }
    }

    public static AlmanacEntry Find(AlmanacKind kind, string id) =>
        Build(kind).FirstOrDefault(e => e.id == id);

    // ── Sources ──────────────────────────────────────────────────

    private static IEnumerable<CropData> Crops()
    {
        var db = SeedSelectionPopup.Instance != null ? SeedSelectionPopup.Instance.Crops : null;
        return db != null ? db.allCrops.Where(c => c != null) : Enumerable.Empty<CropData>();
    }

    private static IEnumerable<EquipmentData> EquipmentList()
    {
        var eq = SeedSelectionPopup.Instance != null ? SeedSelectionPopup.Instance.Equipment : null;
        return eq != null ? eq.Where(e => e != null) : Enumerable.Empty<EquipmentData>();
    }

    private static IEnumerable<AnimalData> AnimalList() =>
        AnimalManager.Instance != null ? AnimalManager.Instance.GetAllAnimals().Where(a => a != null) : Enumerable.Empty<AnimalData>();

    private static IEnumerable<AnimalThreatData> Pests()
    {
        var twm = ThreatWaveManager.Instance;
        if (twm == null) yield break;
        if (twm.DeerData != null) yield return twm.DeerData;
        if (twm.CrowData != null) yield return twm.CrowData;
    }

    // ── Crops ────────────────────────────────────────────────────

    private static AlmanacEntry CropEntry(CropData c)
    {
        var e = new AlmanacEntry
        {
            kind = AlmanacKind.Crop,
            id = c.cropName,
            name = c.cropName,
            icon = c.seedPacketSprite != null ? c.seedPacketSprite : c.cropSprite,
            // No description on crop pages: the pills, stats and timeline say it more plainly.
        };
        SeedState state = CropOwnership.StateOf(c);
        if (state == SeedState.Masked)
        {
            e.unlocked = false;                    // list shows "???" + silhouette; page can't open
            e.unlockHint = SeedShopRules.MaskedHint;
        }
        e.pills.AddRange(AlmanacPills.CropPills(c.TotalGrowthTime, c.maxHP, c.canRegrow, c.canBeCanned,
            c.moistureDepletionRate, c.deerAppetite, c.crowAppetite, c.compostMultiplier));

        AddGrowthTimeline(e, c);
        e.stats.Add(CropStats.Money(c));
        e.stats.Add(CropStats.Coins(c));
        e.stats.Add(CropStats.GrowTime(c));
        e.stats.Add(CropStats.HarvestWindow(c));
        e.stats.Add(CropStats.Health(c));
        e.stats.Add(CropStats.Thirst(c));
        e.stats.Add(CropStats.SeedsPerBag(c));
        e.stats.Add(CropStats.BagCost(c));

        e.appetites.Add(("Deer", c.deerAppetite));
        e.appetites.Add(("Crows", c.crowAppetite));

        if (state == SeedState.Priced)
            e.notes.Add($"Sold at Hazel's stall - {c.unlockCost:N0} Coins.");
        if (c.compostMultiplier > 1f)
            e.notes.Add($"Makes {c.compostMultiplier:0.#}x compost at the Compost Bay when a plant is lost.");
        if (c.canRegrow)
            e.notes.Add($"Grows back {StatText.Value(c.RegrowTime, StatFormat.Seconds)} after each harvest - no new seed needed.");
        if (c.canBeCanned)
            e.notes.Add($"Can be preserved at the Cannery ({(c.canneryTier == 1 ? "4h jam" : c.canneryTier == 2 ? "8h compote" : "12h sauce")}).");
        e.notes.Add("Money and Coins shown for Field 1; field upgrades can raise them further.");
        return e;
    }

    // ── Equipment ────────────────────────────────────────────────

    private static AlmanacEntry EquipmentEntry(EquipmentData d)
    {
        bool unlocked = d.IsUnlocked();
        var e = new AlmanacEntry
        {
            kind = AlmanacKind.Equipment,
            id = d.equipmentID,
            name = d.displayName,
            icon = d.iconSprite,
            unlocked = unlocked,
            unlockHint = !string.IsNullOrEmpty(d.requiredFeatureFlag)
                ? "Unlocks through Research, then in the Market."
                : "Buy it in the Market to unlock.",
            blurb = Copy("almanac_equipment_" + Slug(d.equipmentID), d.description),
        };

        bool compost = d.equipmentID.Contains("compost");
        switch (d.equipmentType)
        {
            case EquipmentType.Scarecrow:
                e.stats.Add(EquipmentStats.ReachLine(d));
                e.stats.Add(EquipmentStats.CooldownLine(d));
                e.stats.Add(EquipmentStats.CapacityLine(d));
                e.pills.Add(AlmanacPills.Simple(PillKind.Guard));
                e.pills.Add(AlmanacPills.Simple(PillKind.Crow));
                break;
            case EquipmentType.Fence:
                e.stats.Add(EquipmentStats.FenceCoverageLine(d));
                e.pills.Add(AlmanacPills.Simple(PillKind.Guard));
                e.pills.Add(AlmanacPills.Simple(PillKind.Deer));
                break;
            case EquipmentType.Sprinkler when compost:
                e.stats.Add(EquipmentStats.WaterPowerLine(d, "Compost per lost crop tier"));
                e.pills.Add(AlmanacPills.Simple(PillKind.MakesCompost));
                break;
            case EquipmentType.Sprinkler:
                e.stats.Add(EquipmentStats.ReachLine(d));
                e.stats.Add(EquipmentStats.WaterPowerLine(d));
                e.stats.Add(EquipmentStats.ActiveTimeLine(d));
                e.stats.Add(EquipmentStats.CooldownLine(d));
                e.pills.Add(AlmanacPills.Simple(PillKind.WatersCrops));
                break;
        }
        return e;
    }

    private static readonly string[] StageNames = { "Seed", "Sprout", "Young plant", "Ripe" };

    private static Sprite[] StageSprites(CropData c) =>
        new[] { c.seedSprite, c.sproutSprite, c.saplingSprite, c.harvestableSprite };

    private static void AddGrowthTimeline(AlmanacEntry e, CropData c)
    {
        e.timelineTitle = "How it grows";
        Sprite[] sprites = StageSprites(c);
        for (int i = 0; i < 4; i++)
            e.stages.Add(new TimelineStage
            {
                name = StageNames[i],
                sprite = sprites[i],
                note = i == 3 ? $"pick in {StatText.Value(c.harvestWindowSeconds, StatFormat.Seconds)}" : null,
                heat = -1f,
            });
        e.stageGaps.Add(StatText.Value(CropStats.StageSeconds(c, GrowthStage.Seed), StatFormat.Seconds));
        e.stageGaps.Add(StatText.Value(CropStats.StageSeconds(c, GrowthStage.Sprout), StatFormat.Seconds));
        e.stageGaps.Add(StatText.Value(CropStats.StageSeconds(c, GrowthStage.Sapling), StatFormat.Seconds));
        if (c.canRegrow)
            e.timelineFooter = $"After each harvest it goes back to a young plant and is ripe again in {StatText.Value(CropStats.RegrowSeconds(c), StatFormat.Seconds)}.";
    }

    // ── Animals ──────────────────────────────────────────────────

    private static AlmanacEntry AnimalEntry(AnimalData a)
    {
        bool unlocked = AnimalManager.Instance != null && AnimalManager.Instance.IsUnlocked(a.animalID);
        var e = new AlmanacEntry
        {
            kind = AlmanacKind.Animal,
            id = a.animalID,
            name = a.displayName,
            icon = a.iconSprite,
            unlocked = unlocked,
            unlockHint = $"Unlock for {a.gemCost} Gems in the Animals menu.",
            blurb = Copy("almanac_animal_" + Slug(a.animalID), a.description),
        };

        float Res(string key) => string.IsNullOrEmpty(key) || ResearchManager.Instance == null ? 0f : ResearchManager.Instance.GetBonus(key);

        // Flags: an animal can do both (the goose lays an egg AND chases pests), so check each.
        if (a.abilityType.HasFlag(AnimalAbilityType.PassiveTimer))
        {
            e.pills.Add(AlmanacPills.Simple(a.rewardGems > 0 ? PillKind.GivesGems : PillKind.GivesCoins));
            e.stats.Add(new StatLine("Gift every", a.cooldownMinutes * 60f, StatFormat.Seconds)
                .Divide(CropStats.FromResearch, Mathf.Max(0.01f, 1f + Res(AnimalManager.CooldownResearchKey(a)))));
            if (a.rewardGems > 0)
                e.stats.Add(new StatLine("Gems per gift", a.rewardGems, StatFormat.Number)
                    .Multiply(CropStats.FromResearch, 1f + Res(AnimalManager.RewardResearchKey(a))));
            else
                e.stats.Add(new StatLine("Coins per gift", a.rewardCoins, StatFormat.Coins)
                    .Multiply(CropStats.FromResearch, 1f + Res(AnimalManager.RewardResearchKey(a)), roundToInt: true)
                    .Multiply(CropStats.FromSkills, 1f + (FarmSkillsManager.Instance != null ? FarmSkillsManager.Instance.GetBonus(FarmSkillTrack.Ranching) : 0f), roundToInt: true));
        }
        if (a.compostPerMinute > 0f)
        {
            e.pills.Add(AlmanacPills.Simple(PillKind.MakesCompost));
            e.stats.Add(new StatLine("Compost per minute", a.compostPerMinute, StatFormat.Number));
        }
        AddFarmBehaviour(e, a, Res);
        return e;
    }

    /// <summary>How the animal moves and works on the farm, read from its visual prefab's
    /// behaviour component so the page shows the values gameplay actually uses.</summary>
    private static void AddFarmBehaviour(AlmanacEntry e, AnimalData a, System.Func<string, float> res)
    {
        GameObject visual = a.visualPrefab;
        var defender = visual != null ? visual.GetComponent<AnimalDefender>() : null;
        var cow = visual != null ? visual.GetComponent<Cow>() : null;

        if (defender != null)
        {
            e.pills.Add(AlmanacPills.Simple(PillKind.Guard));
            if (defender.Chases.Contains(AnimalThreatType.Deer)) e.pills.Add(AlmanacPills.Simple(PillKind.Deer));
            if (defender.Chases.Contains(AnimalThreatType.Crow)) e.pills.Add(AlmanacPills.Simple(PillKind.Crow));

            StatLine run = new StatLine("Run speed (chasing)", Tiles(defender.BaseRunSpeed), StatFormat.Speed);
            StatLine rest = new StatLine("Rest between chases", defender.BaseChaseCooldown, StatFormat.Seconds);
            if (defender is FarmDog)
            {
                run = run.Multiply(CropStats.FromResearch, FarmDog.ResearchSpeedFactor)
                         .Multiply(CropStats.FromSkills, FarmSkillsManager.DogSpeedMultiplier);
                rest = rest.Divide(CropStats.FromResearch, FarmDog.ResearchCooldownDivisor);
            }
            else
            {
                run = run.Multiply("Bonuses", defender.RunSpeedMultiplier);
                rest = rest.Divide("Bonuses", defender.ChaseCooldownDivisor);
            }
            e.stats.Add(new StatLine("Walk speed (patrol)", Tiles(defender.BaseWalkSpeed), StatFormat.Speed));
            e.stats.Add(run);
            e.stats.Add(rest);
        }
        else if (cow != null)
        {
            e.stats.Add(new StatLine("Walk speed", Tiles(cow.WalkSpeed), StatFormat.Speed));
            e.stats.Add(new StatLine("Compost per crop eaten", cow.BaseCompostPerEat, StatFormat.Number)
                .Multiply(CropStats.FromResearch, 1f + res(Research.StatKey.CowRunYield), roundToInt: true)
                .Multiply(CropStats.FromUpgrades, FarmUpgrades.CompostMultiplier, roundToInt: true));
            e.notes.Add($"During a run she eats a ripe crop every {cow.MinEatInterval:0}-{cow.MaxEatInterval:0}s and turns it into compost.");
        }
        else
        {
            e.stats.Add(new StatLine("Walk speed", Tiles(a.roamSpeed), StatFormat.Speed));
        }
    }

    /// <summary>World units per second -> tiles per second (one tile = a tile plus its gap).</summary>
    private static float Tiles(float worldUnits)
    {
        float pitch = FarmGrid.Instance != null && FarmGrid.Instance.TilePitch > 0f ? FarmGrid.Instance.TilePitch : 2f;
        return worldUnits / pitch;
    }

    // ── Pests ────────────────────────────────────────────────────

    private static AlmanacEntry PestEntry(AnimalThreatData p)
    {
        bool deer = p.threatType == AnimalThreatType.Deer;
        AlmanacArt art = AlmanacArt.Instance;
        Sprite icon = art != null ? (deer ? art.deerIcon : art.crowIcon) : null;
        if (icon == null && p.prefabs != null)
            foreach (var go in p.prefabs)
            {
                var sr = go != null ? go.GetComponentInChildren<SpriteRenderer>() : null;
                if (sr != null && sr.sprite != null) { icon = sr.sprite; break; }
            }

        var e = new AlmanacEntry
        {
            kind = AlmanacKind.Pest,
            id = deer ? "deer" : "crow",
            name = deer ? "Deer" : "Crows",
            icon = icon,
            blurb = Copy(deer ? "almanac_pest_deer" : "almanac_pest_crow", ""),
        };
        if (deer) { e.facts.Add("Fences keep them out"); e.facts.Add("Dogs and geese chase them off"); }
        else { e.facts.Add("Scarecrows scare them off"); e.facts.Add("Geese chase them off"); }

        e.stats.Add(new StatLine("Damage per bite (up to)", p.maxDamagePerBite, StatFormat.Number));
        e.stats.Add(new StatLine("Bites before moving on (up to)", p.maxBitesPerPlant, StatFormat.Number));
        e.stats.Add(new StatLine("Time between bites", p.biteInterval, StatFormat.Seconds));
        e.stats.Add(new StatLine("Move speed", Tiles(p.moveSpeed), StatFormat.Speed));

        // Damage by stage, drawn on an example plant (the starter crop, which every player has).
        CropData example = Crops().FirstOrDefault(c => c.seedSprite != null && c.harvestableSprite != null);
        Sprite[] sprites = example != null ? StageSprites(example) : new Sprite[4];
        float[] mults = { p.seedMultiplier, p.sproutMultiplier, p.saplingMultiplier, p.harvestableMultiplier };
        e.timelineTitle = "Damage by growth stage";
        for (int i = 0; i < 4; i++)
            e.stages.Add(new TimelineStage
            {
                name = StageNames[i],
                sprite = sprites[i],
                note = StageNote(mults[i]),
                heat = Mathf.Clamp01(mults[i] / 2f),
            });
        if (example != null) e.timelineFooter = $"(Shown on a {example.cropName}; every crop grows through the same four stages.)";

        // Never name a crop the player hasn't discovered. (Ranked by appetite: the same for every player.)
        foreach (CropData c in Crops().Where(c => CropOwnership.StateOf(c) != SeedState.Masked)
                                      .OrderByDescending(c => c.PestAppetite(p.threatType)))
            e.appetites.Add((c.cropName, c.PestAppetite(p.threatType)));
        return e;
    }

    private static string StageNote(float mult) =>
        mult <= 0f ? "ignored" : $"x{mult.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"; // "damage" is in the section title
}
