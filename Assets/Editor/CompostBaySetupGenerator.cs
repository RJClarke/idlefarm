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
        unlock.icon = "🌱";
        unlock.category = UnlockCategory.Equipment;
        unlock.coinCost = 800;
        unlock.lockedDescription = "Converts dead crops in its zone into Compost.";
        unlock.unlockedMessage = "Compost Bay Unlocked!";
        unlock.requiredFeatureFlag = Research.FeatureFlag.CompostingBasics;
        EditorUtility.SetDirty(unlock);

        // 2. UpgradeData for Conversion Efficiency
        const string convPath = "Assets/Data/Upgrades/CompostBay_Conversion.asset";
        var conv = AssetDatabase.LoadAssetAtPath<UpgradeData>(convPath);
        if (conv == null)
        {
            conv = ScriptableObject.CreateInstance<UpgradeData>();
            AssetDatabase.CreateAsset(conv, convPath);
        }
        conv.upgradeID = "compost_bay_conversion";
        conv.displayName = "Conversion Efficiency";
        conv.description = "Each level boosts compost yield per dead crop.";
        conv.maxLevel = 5;
        conv.baseCoinCost = 200;
        conv.coinCostMultiplier = 2.0f;
        conv.bonusPerLevel = 0.25f;
        conv.bonusUnit = "yield";
        conv.showPlusSign = true;
        conv.icon = "🌱";
        EditorUtility.SetDirty(conv);

        // 3. EquipmentData
        const string eqPath = "Assets/Data/Equipment/CompostBay.asset";
        var eq = AssetDatabase.LoadAssetAtPath<EquipmentData>(eqPath);
        if (eq == null)
        {
            eq = ScriptableObject.CreateInstance<EquipmentData>();
            AssetDatabase.CreateAsset(eq, eqPath);
        }
        // Identity (was silently inheriting Scarecrow defaults — fix)
        eq.equipmentID = "compostbay";
        eq.displayName = "Compost Bay";
        eq.description = "Converts dead crops in its zone into Compost.";
        eq.icon = "🌱";
        eq.unlockID = "compostbay_unlock";
        eq.requiredFeatureFlag = Research.FeatureFlag.CompostingBasics;
        // Hijack the "waterPower" channel for conversion %. AoE/cooldown/capacity unused.
        eq.aoeUpgradeID        = "";
        eq.cooldownUpgradeID   = "";
        eq.capacityUpgradeID   = "";
        eq.waterPowerUpgradeID = "compost_bay_conversion";
        eq.baseAoERadius              = 0f;
        eq.baseCooldownSeconds        = 0f;
        eq.baseRepelCapacity          = 0;
        eq.baseMoisturePowerPerSecond = 1.0f; // base conversion multiplier
        eq.waterPowerBonusPerLevel    = 0f;
        // Wire the upgrade row so the Equipment popup shows it
        eq.uiUpgradeRows = new[] { conv };
        EditorUtility.SetDirty(eq);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CompostBaySetupGenerator] CompostBay_Unlock + Conversion + CompostBay assets ready.");
    }
}
#endif
