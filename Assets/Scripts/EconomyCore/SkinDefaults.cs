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

/// <summary>The starting skin catalogue (spec 2026-10-05 §4). Prices: common 1,000, uncommon 1,500, farmhouses
/// 2,000-3,500; set-exclusive skins cost 0 gems (real-money sets only).</summary>
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
        B("house_red_barn", "Red Barn", Bld + "Hayloft/Full/Hayloft_Red_Full_32x32.png", 2500, 3),
        B("house_green_barn", "Green Barn", Bld + "Hayloft/Full/Hayloft_Green_Full_32x32.png", 2500, 4),
        B("house_grey_barn", "Grey Barn", Bld + "Hayloft/Full/Hayloft_Grey_Full_32x32.png", 2500, 5),
        B("house_ranch", "Ranch House", Bld + "Additional Houses/24_Additional_Houses_One_Story_House_32x32.png", 3000, 6),
        B("house_manor", "Country Manor", Bld + "Additional Houses/24_Additional_Houses_Country_House_32x32.png", 3500, 7),
        B("house_lodge", "Mountain Lodge", Bld + "Additional Houses/24_Additional_Houses_Japanese_House_32x32.png", 3500, 8),
        B("house_yellow_barn", "Golden Barn", Bld + "Hayloft/Full/Hayloft_Yellow_Full_32x32.png", 0, 9, GoldenSet),
    };

    private static SkinDef A(string id, string name, string target, string path, int gems, int order, string set = "") =>
        new SkinDef { id = id, displayName = name, target = target, kind = SkinKind.AnimalSheet, assetPath = path, gemPrice = gems, sortOrder = order, setId = set };

    private static SkinDef B(string id, string name, string path, int gems, int order, string set = "") =>
        new SkinDef { id = id, displayName = name, target = "farmhouse", kind = SkinKind.BuildingSprite, assetPath = path, gemPrice = gems, sortOrder = order, setId = set };
}
