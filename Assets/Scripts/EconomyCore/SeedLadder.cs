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

    // Cheapest to most expensive (user rule: one fixed order, by price). Coins per harvest are flat (1)
    // for now; crops differ by timing, risk and extras, and per-minute balance comes from the balance pass.
    public static readonly IReadOnlyList<Entry> Entries = new[]
    {
        E("Radish",       0,     1, starter: true),
        E("Carrot",       10,    1),
        E("Green Beans",  250,   1),
        E("Tomato",       500,   1),
        E("Corn",         1000,  1, flag: "composting_basics", compost: 2f),
        E("Green Pepper", 1500,  1),
        E("Red Pepper",   1500,  1),   // same as Green Pepper: similar crops, neither should look best-in-slot
        E("Strawberry",   2500,  1, flag: "cannery_unlocked"),
        E("Blueberry",    5000,  1),
    };

    public static readonly IReadOnlyList<string> Order = Entries.Select(e => e.cropName).ToArray();
}
