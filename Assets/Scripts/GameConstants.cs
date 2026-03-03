using UnityEngine;

/// <summary>
/// Global constants and settings that apply to all crops/systems
/// Singleton pattern - easy to access from anywhere
/// </summary>
public class GameConstants : MonoBehaviour
{
    public static GameConstants Instance { get; private set; }

    [Header("Rot & Decay Settings")]
    [Tooltip("HP lost per second when a plant is in Rot state (applies to all crops)")]
    [Range(1f, 10f)]
    public float rotDecayRate = 4f; // HP per second

    [Tooltip("Harvest value multiplier during Rot state (0.25 = 25% of full value)")]
    [Range(0f, 1f)]
    public float rotHarvestValueMultiplier = 0.25f;

    [Header("Dry-Out Settings")]
    [Tooltip("Time (seconds) at 0% moisture before plant enters Dried Out state")]
    [Range(10f, 60f)]
    public float dryOutThreshold = 30f;

    [Tooltip("HP lost per second when plant is Dried Out")]
    [Range(1f, 5f)]
    public float dryOutDecayRate = 2f;

    [Header("Moisture Settings")]
    [Tooltip("Base moisture depletion rate (% per second) - can be modified per crop")]
    [Range(0.1f, 2f)]
    public float baseMoistureDepletionRate = 0.5f; // % per second

    [Tooltip("Moisture added when manually watering a plant")]
    [Range(10f, 50f)]
    public float manualWaterAmount = 30f; // %

    [Header("Growth Speed Bonuses")]
    [Tooltip("Moisture threshold where growth speed bonus starts (above this = faster growth)")]
    [Range(0f, 100f)]
    public float moistureBonusThreshold = 50f; // %

    [Tooltip("Maximum growth speed multiplier at 100% moisture")]
    [Range(1f, 2f)]
    public float maxGrowthSpeedMultiplier = 1.5f; // 1.5x speed at 100% moisture

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Calculate growth speed multiplier based on moisture level
    /// 0% = 0x (stopped)
    /// 1-50% = 1.0x (base speed)
    /// 51-100% = 1.0x to 1.5x (bonus)
    /// </summary>
    public float CalculateGrowthSpeedMultiplier(float moisturePercent)
    {
        if (moisturePercent <= 0f)
        {
            return 0f; // Growth stops completely
        }
        else if (moisturePercent <= moistureBonusThreshold)
        {
            return 1.0f; // Base growth speed
        }
        else
        {
            // Linear bonus from threshold to 100%
            // Formula: 1.0 + ((moisture - 50) / 100) * (maxMultiplier - 1.0)
            float bonusRange = 100f - moistureBonusThreshold;
            float bonusAmount = (moisturePercent - moistureBonusThreshold) / bonusRange;
            return 1.0f + (bonusAmount * (maxGrowthSpeedMultiplier - 1.0f));
        }
    }

    /// <summary>
    /// Calculate actual harvest value based on plant state
    /// </summary>
    public int CalculateHarvestValue(int baseValue, bool isRotting)
    {
        if (isRotting)
        {
            return Mathf.RoundToInt(baseValue * rotHarvestValueMultiplier);
        }
        return baseValue;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Growth Speed Calculations")]
    private void TestGrowthSpeed()
    {
        Debug.Log("=== GROWTH SPEED TESTS ===");
        Debug.Log($"0% moisture: {CalculateGrowthSpeedMultiplier(0f):F2}x speed");
        Debug.Log($"25% moisture: {CalculateGrowthSpeedMultiplier(25f):F2}x speed");
        Debug.Log($"50% moisture: {CalculateGrowthSpeedMultiplier(50f):F2}x speed");
        Debug.Log($"68% moisture: {CalculateGrowthSpeedMultiplier(68f):F2}x speed");
        Debug.Log($"100% moisture: {CalculateGrowthSpeedMultiplier(100f):F2}x speed");
    }

    [ContextMenu("Test Harvest Value Calculations")]
    private void TestHarvestValue()
    {
        int baseValue = 100;
        Debug.Log("=== HARVEST VALUE TESTS ===");
        Debug.Log($"Normal harvest: ${CalculateHarvestValue(baseValue, false)}");
        Debug.Log($"Rot harvest: ${CalculateHarvestValue(baseValue, true)} ({rotHarvestValueMultiplier * 100}% of base)");
    }
#endif
}