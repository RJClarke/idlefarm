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

        // 2. EquipmentData
        const string eqPath = "Assets/Data/Equipment/CompostBay.asset";
        var eq = AssetDatabase.LoadAssetAtPath<EquipmentData>(eqPath);
        if (eq == null)
        {
            eq = ScriptableObject.CreateInstance<EquipmentData>();
            AssetDatabase.CreateAsset(eq, eqPath);
        }
        eq.unlockID = "compostbay_unlock";
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
        EditorUtility.SetDirty(eq);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CompostBaySetupGenerator] CompostBay_Unlock + CompostBay assets ready.");
    }
}
#endif
