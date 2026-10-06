using System;
using UnityEngine;

public enum ProductKind { Consumable, NonConsumable }
public enum StoreSection { Pass, Gems, ComingSoon, Sets, Boosts } // append-only: serialized by index

/// <summary>One Store product. The catalog asset (Resources/StoreCatalog) holds these; the starting
/// values come from StoreDefaults, seeded by Farm Game > Monetization > Build Assets.</summary>
[Serializable]
public class StoreProductDef
{
    public string id;
    public string displayName;
    public ProductKind kind;
    public StoreSection section;
    public int gems;
    [Tooltip("Shown until the real store reports a localized price.")]
    public string fallbackPrice;
    [TextArea(2, 4)] public string description;
    [Tooltip("Small corner ribbon, e.g. \"+20%\". Empty = none.")]
    public string ribbon;
    public bool comingSoon;
    public Sprite icon;
    [Tooltip("Skins this purchase unlocks (SkinDefaults ids).")]
    public string[] grantsSkinIds = new string[0];
}

/// <summary>Starting Store catalog (spec §6). Product ids must match the store consoles in phase 2.</summary>
public static class StoreDefaults
{
    public const string PassId = "farmers_pass";
    public const string PassDescription = "No ads. Ever.";
    public const string StarterId = "bundle_starter";
    public const string HarvestBlessingId = "boost_harvest_blessing";
    public const float HarvestBlessingMultiplier = 1.25f;
    /// <summary>Ids removed from the catalog (Build Assets deletes them from StoreCatalog.asset).</summary>
    public static readonly string[] RetiredIds = { "soon_harvest_blessing", "soon_animal_skins", "soon_building_skins" };

    public static StoreProductDef[] Products => new[]
    {
        new StoreProductDef { id = PassId, displayName = "Farmer's Pass", kind = ProductKind.NonConsumable,
            section = StoreSection.Pass, gems = 500, fallbackPrice = "$9.99", description = PassDescription, ribbon = "" },

        Bundle("gems_handful", "Handful", 100, "$0.99", ""),
        Bundle("gems_pouch", "Pouch", 550, "$4.99", "+10%"),
        Bundle("gems_sack", "Sack", 1200, "$9.99", "+20%"),
        Bundle("gems_chest", "Chest", 2600, "$19.99", "+30%"),
        Bundle("gems_vault", "Vault", 7000, "$49.99", "+40%"),

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
    };

    private static StoreProductDef Bundle(string id, string name, int gems, string price, string ribbon) =>
        new StoreProductDef { id = id, displayName = name, kind = ProductKind.Consumable, section = StoreSection.Gems,
            gems = gems, fallbackPrice = price, ribbon = ribbon, description = "" };

    private static StoreProductDef Soon(string id, string name, string description) =>
        new StoreProductDef { id = id, displayName = name, kind = ProductKind.NonConsumable, section = StoreSection.ComingSoon,
            comingSoon = true, description = description, fallbackPrice = "", ribbon = "" };
}
