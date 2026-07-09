namespace Research
{
    /// <summary>
    /// String constants for every research-bonus stat key. Referenced by ResearchData.targetStatKey
    /// and by consumers calling ResearchManager.GetBonus(StatKey.XYZ).
    /// </summary>
    public static class StatKey
    {
        // Soil
        public const string SoilWaterEfficiency = "soil_water_efficiency";
        public const string SoilQuality = "soil_quality";

        // Helper
        public const string HelperTillSpeed = "helper_till_speed";
        public const string HelperWaterSpeed = "helper_water_speed";
        public const string HelperWaterEfficiency = "helper_water_efficiency";
        public const string HelperPlantSpeed = "helper_plant_speed";
        public const string HelperHarvestSpeed = "helper_harvest_speed";
        public const string HelperHarvestEfficiency = "helper_harvest_efficiency";

        // Plant
        public const string CropHp = "crop_hp";
        public const string CropGrowthSpeed = "crop_growth_speed";
        public const string CropBonusSellAmount = "crop_bonus_sell_amount";
        public const string CropBonusCoinAmount = "crop_bonus_coin_amount"; // +% coins banked per harvest

        // Animals
        public const string ChickenCooldown = "chicken_cooldown";
        public const string ChickenEfficiency = "chicken_efficiency";
        public const string DogCooldown = "dog_cooldown";
        public const string DogEfficiency = "dog_efficiency";
        public const string RoosterCooldown = "rooster_cooldown";
        public const string RoosterEfficiency = "rooster_efficiency";
        public const string CowPassiveCompost = "cow_passive_compost";
        public const string CowRunYield = "cow_run_yield";

        // Equipment — values intentionally match EquipmentData.*UpgradeID so a single
        // GetBonus(upgradeID) call in EquipmentManager picks them up.
        // (Compost Bay deferred to Plan 2.)
        public const string ScarecrowAoe = "scarecrow_aoe";
        public const string ScarecrowEffectiveness = "scarecrow_capacity";
        public const string ScarecrowCooldown = "scarecrow_cooldown";
        public const string SprinklerAoe = "sprinkler_aoe";
        public const string SprinklerEffectiveness = "sprinkler_power";
        public const string SprinklerCooldown = "sprinkler_cooldown";
        public const string FenceAoe = "fence_aoe";
        public const string FenceEffectiveness = "fence_capacity";
        public const string FenceCooldown = "fence_cooldown";
        public const string CompostBayConversion = "compost_bay_conversion";

        // Weather
        public const string StormDamageReduction = "storm_damage_reduction";
        public const string RainWatering = "rain_watering";

        // Economy
        public const string SeedBagSize = "seed_bag_size";        // +% seeds per bag
        public const string SeedBagDiscount = "seed_bag_discount"; // +% reduction to bag cost

        // Processing (Pantry Economy Phase 3) — per-building fuel-efficiency (−% wood/slot-hour).
        public const string CanneryBurnEfficiency = "cannery_burn_efficiency";
        public const string SmokehouseBurnEfficiency = "smokehouse_burn_efficiency";

        // Meta
        public const string GameSpeed = "game_speed";
        public const string ResearchSpeed = "research_speed";
        public const string OfflineCap = "offline_cap";
        public const string OfflineEfficiency = "offline_efficiency";
    }

    /// <summary>String constants for binary feature unlocks granted by completing binary researches.</summary>
    public static class FeatureFlag
    {
        public const string OfflineProgress = "offline_progress";
        public const string MaxWaterHealsPlant = "max_water_heals_plant";
        public const string CompostingBasics = "composting_basics"; // for Market gating Compost Bay (Plan 2)

        // Processing (Pantry Economy Phase 3).
        public const string CanneryUnlocked = "cannery_unlocked";         // "Preserving" → Cannery in Carpenter stock
        public const string SmokehouseUnlocked = "smokehouse_unlocked";   // "Smoking"    → Smokehouse in Carpenter stock
        public const string CanneryExpansion1 = "cannery_expansion_1";    // +2 purchasable Cannery slots (→22)
        public const string CanneryExpansion2 = "cannery_expansion_2";    // +2 purchasable Cannery slots (→24)
        public const string SmokehouseExpansion1 = "smokehouse_expansion_1"; // +2 purchasable Smokehouse slots (→8)
    }
}
