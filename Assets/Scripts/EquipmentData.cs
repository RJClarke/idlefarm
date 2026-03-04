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
    [TextArea(2, 4)]
    public string description = "Repels crows within its area of effect.";

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
}
