/// <summary>
/// The hand-authored Town Requests pool: who asks, why, and exactly what they want.
///
/// Lives in code (not the RequestCatalog asset) so the content always ships without any asset
/// wiring, and so edits are reviewable in a diff. RequestCatalog still owns the tuning knobs
/// (reward bases + variance); this owns the content.
///
/// BALANCE: counts are tuned so every request in a band costs roughly the same, using a
/// gold-equivalent value scale V (crop harvestValue*1.5 money, converted at 4 money = 1 gold;
/// wood 1, compost 1, egg 25, Perch 100, Bass 400, Pike 2000, Smoked Perch 300, Smoked Bass 1400).
/// Targets: Easy ~100V, Medium ~300V, Hard ~750V. Scarce items only ever appear alone, beside a
/// common filler, or with their own kind. The two premium outliers (Pike, Smoked Bass) cannot be
/// scaled below qty 1, so they carry a rewardMultiplier and a low weight instead.
/// </summary>
public static class TownRequestContent
{
    private const int Common = 10;   // normal pick weight
    private const int Rare = 1;      // premium outliers

    private static AuthoredRequest R(string who, string blurb, params AuthoredLine[] lines)
        => new AuthoredRequest { requester = who, blurb = blurb, lines = lines, weight = Common, rewardMultiplier = 1f };

    private static AuthoredRequest Premium(string who, string blurb, float mult, params AuthoredLine[] lines)
        => new AuthoredRequest { requester = who, blurb = blurb, lines = lines, weight = Rare, rewardMultiplier = mult };

    private static AuthoredLine L(string itemId, int count) => new AuthoredLine { itemId = itemId, count = count };

    /// <summary>Easy: one item, ~100V.</summary>
    public static readonly AuthoredRequest[] Easy =
    {
        R("Marta",      "Eggs for tomorrow's morning buns.",                 L("egg", 4)),
        R("Cormac",     "Patching the schoolhouse roof before the rains.",   L("wood", 90)),
        R("Widow Bree", "A small batch of strawberry preserves.",            L("Strawberry", 16)),
        R("Pip",        "Feeding my rabbit - he only likes the orange ones!", L("Carrot", 16)),
        R("Sal",        "Tonight's stew wants a heap of radishes.",          L("Radish", 32)),
        R("Gus",        "Fresh kindling for the stable stoves.",             L("wood", 100)),
        R("Rosie",      "Snack time for the little ones.",                   L("Tomato", 10)),
        R("Hettie",     "My famous cornbread won't make itself.",            L("Corn", 13)),
        R("Fern",       "Steeping a tonic - peppers with a bite.",           L("Green Pepper", 10)),
        R("Junie",      "Restocking my produce stall by dawn.",              L("Green Beans", 11)),
        R("Clara",      "Blueberry custard tarts for the creamery.",         L("Blueberry", 10)),
        R("Otis",       "A good heap of compost to enrich the town beds.",   L("compost", 90)),
    };

    /// <summary>Medium: two items, ~300V.</summary>
    public static readonly AuthoredRequest[] Medium =
    {
        R("Sal",        "Harvest supper - beans in the pot, radish on the side.", L("Green Beans", 16), L("Radish", 40)),
        R("Marta",      "A double batch of tomato focaccia for market day.",      L("Tomato", 20), L("egg", 4)),
        R("Bram",       "Charcoal burn needs a woodpile - and greens for the crew.", L("wood", 150), L("Carrot", 27)),
        R("Widow Bree", "Mixed-berry jam - the good stuff sells fast.",           L("Strawberry", 25), L("Blueberry", 15)),
        R("Tam",        "Pepper-pie special - one sweet, one hot.",               L("Green Pepper", 15), L("Red Pepper", 15)),
        R("Old Finch",  "Trading day - perch and wood to smoke 'em.",             L("fish_raw_1", 2), L("wood", 100)),
        R("Doc Hollis", "Broth and mash for the sick ward.",                      L("Carrot", 27), L("Corn", 19)),
        R("Nella",      "Full house - breakfast and lunch covered.",              L("egg", 6), L("Tomato", 15)),
        R("Gus",        "Winter feed and fresh bedding for the barn.",            L("Corn", 20), L("wood", 140)),
        R("Meg",        "Roots and a perch for the big soup pot.",                L("Radish", 34), L("fish_raw_1", 2)),
        R("Clara",      "Eggs and berries for the creamery.",                     L("egg", 4), L("Strawberry", 30)),
        R("Otis",       "Rebuilding the orchard beds before planting.",           L("compost", 150), L("wood", 150)),
    };

    /// <summary>Hard: two or three items, ~750V. Last two are premium outliers.</summary>
    public static readonly AuthoredRequest[] Hard =
    {
        R("Sal",        "The Founders' Feast - I'm cooking for the whole town.",  L("Tomato", 30), L("Corn", 30), L("wood", 210)),
        R("Widow Bree", "Winter-market preserves - every berry I can get.",       L("Strawberry", 40), L("Blueberry", 50)),
        R("Bram",       "Forge overhaul. Weeks of charcoal ahead.",               L("wood", 600), L("compost", 150)),
        R("Tam",        "Grand bake-off entry: the triple-pepper tart.",          L("Green Pepper", 30), L("Red Pepper", 30), L("wood", 150)),
        R("Doc Hollis", "Stocking the infirmary before flu season.",              L("Carrot", 60), L("Corn", 48)),
        R("Cormac",     "Raising the new granary.",                               L("wood", 550), L("compost", 200)),
        R("Meg",        "Feeding the road crew - and one good bass for the pot.", L("fish_raw_2", 1), L("Green Beans", 35)),
        R("Otis",       "The autumn fair - decorations, feast, the works.",       L("Corn", 30), L("Tomato", 25), L("Strawberry", 42)),
        R("Nella",      "Harvest-festival banquet - three days of guests.",       L("Carrot", 40), L("egg", 8), L("wood", 300)),
        R("Old Finch",  "A crate of smoked perch for the trade caravan.",         L("fish_smoked_1", 2), L("wood", 150)),

        // Premium outliers: a single unit already exceeds the Hard band (Pike 2000V, Smoked Bass
        // 1400V) and qty cannot go below 1, so they pay out proportionally and roll rarely.
        Premium("Old Finch", "The Anglers' Guild wants the prize catch.",         2.7f, L("fish_raw_3", 1)),
        Premium("Sal",       "One perfect smoked bass for the mayor's table.",    1.9f, L("fish_smoked_2", 1)),
    };

    /// <summary>Authored pool for a slot: 0=Easy, 1=Medium, 2=Hard.</summary>
    public static AuthoredRequest[] ForSlot(int slot) => slot == 0 ? Easy : slot == 1 ? Medium : Hard;
}
