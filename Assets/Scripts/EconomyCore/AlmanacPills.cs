using System.Collections.Generic;
using UnityEngine;

/// <summary>What kind of thing an Almanac pill says. Pills work like types: each one is a class the
/// entry belongs to, with its own colour (set by the popup) and a help line shown when tapped.</summary>
public enum PillKind
{
    Regrows, QuickGrower, SlowGrower, Cannable, GreatCompost, Sturdy, Fragile, Thirsty, DroughtHardy,
    DeerDamage, CrowDamage,   // crop pages: how hard each pest hits this crop ("Deer | 175%")
    Deer, Crow,               // equipment/animal pages: which pest it deals with
    Guard, LaysEggs, GivesGems, WatersCrops, MakesCompost,
}

/// <summary>One pill. <see cref="value"/> is only set on the two-part pest-damage pills.</summary>
public struct Pill
{
    public PillKind kind;
    public string label;
    public string value;
    public float appetite;
}

/// <summary>
/// Builds the Almanac's pills and their tap-to-learn help text. Pure, so the thresholds and wording
/// are tested; trait thresholds come from <see cref="CropTraits"/> so a pill can never disagree
/// with the numbers on the page.
/// </summary>
public static class AlmanacPills
{
    public static Pill Simple(PillKind kind) => new Pill { kind = kind, label = Label(kind) };

    /// <summary>"Deer | 175%": how much of a pest's normal damage lands on this crop.</summary>
    public static Pill PestDamage(bool deer, float appetite) => new Pill
    {
        kind = deer ? PillKind.DeerDamage : PillKind.CrowDamage,
        label = deer ? "Deer" : "Crow",
        value = Mathf.RoundToInt(appetite * 100f) + "%",
        appetite = appetite,
    };

    /// <summary>1 = pest hits harder than normal (red), -1 = softer (green), 0 = normal.</summary>
    public static int Heat(Pill p) => p.appetite > 1.01f ? 1 : p.appetite < 0.99f ? -1 : 0;

    /// <summary>A crop's pills: its traits, then a pest pill for each pest that doesn't do exactly
    /// normal damage (100% says nothing worth a pill).</summary>
    public static List<Pill> CropPills(float growSeconds, int maxHp, bool regrows, bool cannable,
                                       float thirst, float deerAppetite, float crowAppetite,
                                       float compostMultiplier = 1f)
    {
        var pills = new List<Pill>();
        if (regrows) pills.Add(Simple(PillKind.Regrows));
        if (growSeconds <= CropTraits.QuickGrowSeconds) pills.Add(Simple(PillKind.QuickGrower));
        else if (growSeconds >= CropTraits.SlowGrowSeconds) pills.Add(Simple(PillKind.SlowGrower));
        if (cannable) pills.Add(Simple(PillKind.Cannable));
        if (compostMultiplier > 1f) pills.Add(Simple(PillKind.GreatCompost));
        if (maxHp >= CropTraits.SturdyHp) pills.Add(Simple(PillKind.Sturdy));
        else if (maxHp <= CropTraits.FragileHp) pills.Add(Simple(PillKind.Fragile));
        if (thirst >= CropTraits.ThirstyAt) pills.Add(Simple(PillKind.Thirsty));
        else if (thirst <= CropTraits.DroughtHardyAt) pills.Add(Simple(PillKind.DroughtHardy));
        if (!Mathf.Approximately(deerAppetite, 1f)) pills.Add(PestDamage(true, deerAppetite));
        if (!Mathf.Approximately(crowAppetite, 1f)) pills.Add(PestDamage(false, crowAppetite));
        return pills;
    }

    public static string Label(PillKind kind) => kind switch
    {
        PillKind.Regrows => "Regrows",
        PillKind.QuickGrower => "Quick grower",
        PillKind.SlowGrower => "Slow grower",
        PillKind.Cannable => "Cannable",
        PillKind.GreatCompost => "Great compost",
        PillKind.Sturdy => "Sturdy",
        PillKind.Fragile => "Fragile",
        PillKind.Thirsty => "Thirsty",
        PillKind.DroughtHardy => "Drought-hardy",
        PillKind.DeerDamage or PillKind.Deer => "Deer",
        PillKind.CrowDamage or PillKind.Crow => "Crow",
        PillKind.Guard => "Guard",
        PillKind.LaysEggs => "Lays eggs",
        PillKind.GivesGems => "Gives Gems",
        PillKind.WatersCrops => "Waters crops",
        PillKind.MakesCompost => "Compost",
        _ => kind.ToString(),
    };

    /// <summary>The tap-to-learn line: what the pill means and why it matters.</summary>
    public static string Help(Pill p)
    {
        string S(float s) => StatText.Value(s, StatFormat.Seconds);
        string X(float m) => m.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
        switch (p.kind)
        {
            case PillKind.Regrows:
                return "Regrows: after you pick it, the plant stays in the ground and grows another crop. No new seed needed.";
            case PillKind.QuickGrower:
                return $"Quick grower: goes from seed to ripe in {S(CropTraits.QuickGrowSeconds)} or less.";
            case PillKind.SlowGrower:
                return $"Slow grower: takes {S(CropTraits.SlowGrowSeconds)} or more to go from seed to ripe.";
            case PillKind.Cannable:
                return "Cannable: some of this crop can go to the Cannery and become preserves worth much more.";
            case PillKind.GreatCompost:
                return "Great compost: makes extra compost at the Compost Bay whenever a plant is lost.";
            case PillKind.Sturdy:
                return $"Sturdy: {CropTraits.SturdyHp} health or more, so it holds up longer against pests and dry soil.";
            case PillKind.Fragile:
                return $"Fragile: {CropTraits.FragileHp} health or less. Pests and dry soil finish it off quickly.";
            case PillKind.Thirsty:
                return $"Thirsty: dries out its soil {X(CropTraits.ThirstyAt)} as fast as normal or faster. Sprinklers help a lot.";
            case PillKind.DroughtHardy:
                return $"Drought-hardy: its soil stays wet longer ({X(CropTraits.DroughtHardyAt)} as fast as normal or slower), so it needs less watering.";
            case PillKind.DeerDamage:
            case PillKind.CrowDamage:
            {
                string pests = p.kind == PillKind.DeerDamage ? "Deer" : "Crows";
                string how = Heat(p) > 0 ? "they go after it - red means more damage than normal."
                                         : "they mostly leave it alone - green means less damage than normal.";
                return $"{pests} do {p.value} of their normal damage to this crop: {how}";
            }
            case PillKind.Deer:
                return "Deer: this keeps deer off your crops.";
            case PillKind.Crow:
                return "Crow: this keeps crows off your crops.";
            case PillKind.Guard:
                return "Guard: protects your crops from the pests shown next to it.";
            case PillKind.LaysEggs:
                return "Lays eggs: drops an egg on a timer while it's your equipped animal. On Sell it pays out Coins; on Collect it's kept for town requests.";
            case PillKind.GivesGems:
                return "Gives Gems: leaves you a gift of Gems on a timer while it's your equipped animal.";
            case PillKind.WatersCrops:
                return "Waters crops: keeps the soil around it wet, so your helpers spend less time hauling water.";
            case PillKind.MakesCompost:
                return "Compost: makes compost, which you can spend to speed up Research.";
            default:
                return p.label;
        }
    }
}
