using UnityEngine;

/// <summary>
/// Equipment types that can be placed in zones.
/// </summary>
public enum EquipmentType
{
    Scarecrow,
    Fence,
    Sprinkler
}

/// <summary>
/// ScriptableObject defining an equipment item's base stats and upgrade references.
/// Create via: Right-click in Project > Create > Farm Game > Equipment Data
/// </summary>
[CreateAssetMenu(fileName = "New Equipment", menuName = "Farm Game/Equipment Data", order = 6)]
public class EquipmentData : ScriptableObject
{
    [Header("Identity")]
    public string equipmentID = "scarecrow";
    public string displayName = "Scarecrow";
    public EquipmentType equipmentType = EquipmentType.Scarecrow;
    public string icon = "";
    [Tooltip("Sprite used in UI tiles (equipment rail, slots, etc.)")]
    public Sprite iconSprite;
    [TextArea(2, 4)]
    public string description = "Repels crows within its area of effect.";

    [Header("Unlock")]
    [Tooltip("Must match the unlockID on the corresponding UnlockData asset (e.g. 'scarecrow_unlock')")]
    public string unlockID = "";
    [Tooltip("Optional. If set, this equipment is hidden entirely (not shown as locked) until ResearchManager.IsFeatureUnlocked(flag) is true.")]
    public string requiredFeatureFlag = "";

    /// <summary>
    /// Returns true if this equipment has been unlocked (purchased at the Market).
    /// If unlockID is empty, the equipment is always considered unlocked.
    /// </summary>
    public bool IsUnlocked()
    {
        if (string.IsNullOrEmpty(unlockID)) return true;
        if (UpgradeManager.Instance == null) return false;
        return UpgradeManager.Instance.GetPermanentLevel(unlockID) > 0;
    }

    [Header("Base Stats")]
    [Tooltip("Seconds before repel charges refill after depletion")]
    [Range(5f, 120f)]
    public float baseCooldownSeconds = 60f;

    [Tooltip("Number of threats repelled per cycle before cooldown starts")]
    [Range(1, 10)]
    public int baseRepelCapacity = 1;

    [Tooltip("World-unit radius of the interception circle")]
    [Range(0.5f, 10f)]
    public float baseAoERadius = 2.0f;

    [Header("Threat Targeting")]
    [Tooltip("Which animal threat type this equipment counters")]
    public AnimalThreatType countersTheatType = AnimalThreatType.Crow;

    [Header("Visual")]
    [Tooltip("Optional prefab to spawn in the zone (scarecrow sprite, etc.)")]
    public GameObject visualPrefab;

    [Header("UI Display")]
    [Tooltip("UpgradeData assets to display in the Equipment popup, in row order.")]
    public UpgradeData[] uiUpgradeRows;

    [Header("Upgrade IDs (must match UpgradeData assets)")]
    public string aoeUpgradeID = "scarecrow_aoe";
    public string cooldownUpgradeID = "scarecrow_cooldown";
    public string capacityUpgradeID = "scarecrow_capacity";

    [Header("Upgrade Scaling")]
    [Tooltip("AoE radius added per upgrade level")]
    [Range(0.1f, 2f)]
    public float aoeBonusPerLevel = 0.5f;

    [Tooltip("Cooldown seconds reduced per upgrade level")]
    [Range(1f, 15f)]
    public float cooldownReductionPerLevel = 5f;

    [Tooltip("Extra repel charges per upgrade level")]
    [Range(1, 5)]
    public int capacityBonusPerLevel = 1;

    [Tooltip("Minimum cooldown floor (seconds) — upgrades can't reduce below this")]
    [Range(1f, 20f)]
    public float minCooldownSeconds = 10f;

    // ─────────────────────────────────────────────────────────────────────
    // Fence-specific fields (only used when equipmentType == Fence)
    // ─────────────────────────────────────────────────────────────────────

    [Header("Fence Settings")]
    [Tooltip("Coverage fraction per fence level (1-based). Index 0 = level 1, etc.")]
    public float[] fenceCoveragePerLevel = new float[] { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };

    /// <summary>
    /// Returns the fence coverage fraction for the given level (1-based).
    /// </summary>
    public float GetFenceCoverage(int level)
    {
        if (fenceCoveragePerLevel == null || fenceCoveragePerLevel.Length == 0)
            return 1f;
        int index = Mathf.Clamp(level - 1, 0, fenceCoveragePerLevel.Length - 1);
        return fenceCoveragePerLevel[index];
    }

    // ─────────────────────────────────────────────────────────────────────
    // Sprinkler-specific fields (only used when equipmentType == Sprinkler)
    // ─────────────────────────────────────────────────────────────────────

    [Header("Sprinkler Settings")]
    [Tooltip("Seconds the sprinkler is active (watering) per cycle")]
    [Range(1f, 60f)]
    public float activeDurationSeconds = 10f;

    [Tooltip("Moisture % added per second to plants within AoE while active")]
    [Range(0.1f, 10f)]
    public float baseMoisturePowerPerSecond = 1.5f;

    [Tooltip("Upgrade ID for water power (3rd path — replaces capacity for Sprinkler)")]
    public string waterPowerUpgradeID = "sprinkler_power";

    [Tooltip("Extra moisture %/sec per upgrade level")]
    [Range(0.1f, 3f)]
    public float waterPowerBonusPerLevel = 0.5f;

    [Tooltip("Upgrade ID for sprinkler active-duration boost. Only used by Sprinkler.")]
    public string durationUpgradeID = "";
}
