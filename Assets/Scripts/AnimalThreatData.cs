using UnityEngine;

/// <summary>
/// ScriptableObject that configures all stats for an animal threat (Deer, Crow, etc.)
/// Create via: Right-click in Project → Create → Farm Game → Animal Threat Data
///
/// Weather threats are a separate system — this only covers animal/pest behavior.
/// </summary>
[CreateAssetMenu(fileName = "New Animal Threat", menuName = "Farm Game/Animal Threat Data", order = 5)]
public class AnimalThreatData : ScriptableObject
{
    [Header("Identity")]
    public string threatName = "Animal";
    public AnimalThreatType threatType = AnimalThreatType.Deer;

    [Header("Prefabs")]
    [Tooltip("Drag DeerF and DeerM prefabs here — one is picked randomly each spawn")]
    public GameObject[] prefabs;

    [Header("Movement")]
    [Tooltip("World units per second when moving between plants")]
    [Range(0.5f, 10f)]
    public float moveSpeed = 3f;

    [Tooltip("World radius the animal searches for its next plant target")]
    [Range(0.5f, 8f)]
    public float grazingRadius = 3f;

    [Header("Bite / Peck Settings")]
    [Tooltip("Minimum number of bites on a single plant before moving on")]
    [Range(1, 8)]
    public int minBitesPerPlant = 2;

    [Tooltip("Maximum number of bites on a single plant before moving on")]
    [Range(1, 8)]
    public int maxBitesPerPlant = 4;

    [Tooltip("Base minimum damage per single bite (before stage multiplier)")]
    [Range(1f, 80f)]
    public float minDamagePerBite = 20f;

    [Tooltip("Base maximum damage per single bite (before stage multiplier)")]
    [Range(1f, 80f)]
    public float maxDamagePerBite = 40f;

    [Tooltip("Seconds between each bite while standing on a plant")]
    [Range(0.5f, 5f)]
    public float biteInterval = 3f;

    [Header("Stage Damage Multipliers")]
    [Tooltip("Multiplier when eating a Seed. Set to 0 to make this animal ignore seeds.")]
    [Range(0f, 3f)]
    public float seedMultiplier = 0f;       // Deer ignores seeds

    [Tooltip("Multiplier when eating a Sprout.")]
    [Range(0f, 3f)]
    public float sproutMultiplier = 1f;

    [Tooltip("Multiplier when eating a Sapling.")]
    [Range(0f, 3f)]
    public float saplingMultiplier = 2f;    // Deer loves saplings

    [Tooltip("Multiplier when eating a Harvestable plant.")]
    [Range(0f, 3f)]
    public float harvestableMultiplier = 1.5f;

    [Header("Placeholder Visual (MVP)")]
    [Tooltip("Color of the solid-color placeholder sprite used in-editor")]
    public Color placeholderColor = Color.white;

    [Tooltip("World-space size of the placeholder square sprite")]
    [Range(0.1f, 1.5f)]
    public float visualSize = 0.4f;

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the stage multiplier. A value of 0 means this animal ignores that stage.
    /// </summary>
    public float GetStageMultiplier(GrowthStage stage)
    {
        switch (stage)
        {
            case GrowthStage.Seed:        return seedMultiplier;
            case GrowthStage.Sprout:      return sproutMultiplier;
            case GrowthStage.Sapling:     return saplingMultiplier;
            case GrowthStage.Harvestable: return harvestableMultiplier;
            default: return 0f;
        }
    }

    /// <summary>
    /// Returns true if this animal will target plants at this stage.
    /// (Multiplier of 0 = ignored.)
    /// </summary>
    public bool CanTargetStage(GrowthStage stage) => GetStageMultiplier(stage) > 0f;

    /// <summary>
    /// Calculate a random damage value for a single bite, including stage multiplier.
    /// Also used to calculate how much hunger that bite satisfies.
    /// </summary>
    public float GetBiteDamage(GrowthStage stage)
    {
        float baseDamage = Random.Range(minDamagePerBite, maxDamagePerBite);
        return baseDamage * GetStageMultiplier(stage);
    }

    private void OnValidate()
    {
        minDamagePerBite = Mathf.Min(minDamagePerBite, maxDamagePerBite);
        minBitesPerPlant = Mathf.Min(minBitesPerPlant, maxBitesPerPlant);
    }
}

/// <summary>
/// Identifies which family of animal this is.
/// Used by ThreatWaveManager for cap counting.
/// </summary>
public enum AnimalThreatType
{
    Deer,
    Crow
}