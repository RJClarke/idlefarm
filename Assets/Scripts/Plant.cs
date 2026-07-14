using UnityEngine;

/// <summary>
/// Represents a single planted crop that grows through stages
/// Phase 3.1: Moisture tracked internally, displayed on soil tile
/// </summary>
[RequireComponent(typeof(PlantVisuals))]
public class Plant : MonoBehaviour
{
    [Header("Crop Configuration")]
    [SerializeField] private CropData cropData;

    [Header("Current State (Read-Only)")]
    [SerializeField] private GrowthStage currentStage = GrowthStage.Seed;
    [SerializeField] private float currentHP;
    [SerializeField] private float stageTimer;
    [SerializeField] private bool isGrowing = true;

    [Header("Moisture System (Phase 3.1)")]
    [SerializeField] private float currentMoisture = 100f; // 0-100%
    [SerializeField] private bool isDriedOut = false;
    [SerializeField] private float dryOutTimer = 0f;

    [Header("Harvest & Rot System (Phase 4)")]
    [SerializeField] private bool isInHarvestWindow = false;
    [SerializeField] private float harvestWindowTimer = 0f;
    [SerializeField] private bool isRotting = false;

    // Components
    private PlantVisuals visuals;
    private SoilTile parentTile;

    // Properties
    public GrowthStage CurrentStage => currentStage;
    public CropData CropData => cropData;
    public float CurrentHP => currentHP;
    public bool IsHarvestable => currentStage == GrowthStage.Harvestable;
    public SoilTile ParentTile => parentTile;
    public float CurrentMoisture => currentMoisture;
    public bool IsDriedOut => isDriedOut;
    public float CurrentGrowthSpeed { get; private set; } = 1.0f;
    public bool IsInHarvestWindow => isInHarvestWindow;
    public float HarvestWindowTimer => harvestWindowTimer;
    public bool IsRotting => isRotting;

    private void Awake()
    {
        visuals = GetComponent<PlantVisuals>();
    }

    /// <summary>
    /// Initialize the plant with crop data and parent tile
    /// </summary>
    public void Initialize(CropData crop, SoilTile tile)
    {
        cropData = crop;
        parentTile = tile;
        
        currentStage = GrowthStage.Seed;
        float hpBonus = ResearchManager.Instance != null ? ResearchManager.Instance.GetBonus(Research.StatKey.CropHp) : 0f;
        currentHP = crop.maxHP * (1f + hpBonus);
        stageTimer = crop.GetStageTime(currentStage);
        // Head Start (farm upgrade): newly planted crops begin partway into the first stage.
        stageTimer *= (1f - FarmUpgrades.HeadStartFraction);
        isGrowing = true;

        currentMoisture = 100f;
        isDriedOut = false;
        dryOutTimer = 0f;

        if (visuals != null)
            visuals.UpdateVisuals(currentStage, crop);

    }

    private void Update()
    {
        if (cropData == null) return;

        if (RunManager.Instance != null && !RunManager.Instance.IsRunActive)
            return;

        if (isGrowing)
            UpdateGrowth(Time.deltaTime);

        UpdateMoisture(Time.deltaTime);

        if (isInHarvestWindow)
            UpdateHarvestWindow(Time.deltaTime);

        if (isRotting)
            UpdateRot(Time.deltaTime);
    }

    private void UpdateGrowth(float deltaTime)
    {
        if (currentStage == GrowthStage.Harvestable)
        {
            isGrowing = false;
            CurrentGrowthSpeed = 0f;
            return;
        }

        float speedMultiplier = CalculateGrowthSpeed();
        CurrentGrowthSpeed = speedMultiplier;
        stageTimer -= deltaTime * speedMultiplier;

        if (stageTimer <= 0f)
            AdvanceToNextStage();
    }

    private float CalculateGrowthSpeed()
    {
        if (currentMoisture <= 0f)   return 0f;

        float baseSpeed;
        if (currentMoisture <= 50f) baseSpeed = 1.0f;
        else
        {
            float bonus = (currentMoisture - 50f) / 100f;
            baseSpeed = 1.0f + bonus;
        }

        float researchBonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.CropGrowthSpeed)
            : 0f;
        // Growth Rate (farm upgrade) stacks multiplicatively on top of research/moisture.
        return baseSpeed * (1f + researchBonus) * FarmUpgrades.GrowthMultiplier;
    }

    private void UpdateMoisture(float deltaTime)
    {
        if (GameConstants.Instance == null) return;

        float depletionRate = GameConstants.Instance.baseMoistureDepletionRate;
        depletionRate *= cropData.moistureDepletionRate;

        // SoilWaterEfficiency: each level reduces depletion (water lasts longer).
        if (ResearchManager.Instance != null)
        {
            float soilBonus = ResearchManager.Instance.GetBonus(Research.StatKey.SoilWaterEfficiency);
            depletionRate /= Mathf.Max(0.01f, 1f + soilBonus);
        }

        // Water Retention (farm upgrade): water lasts longer still.
        depletionRate /= Mathf.Max(0.01f, FarmUpgrades.MoistureRetentionDivisor);

        currentMoisture -= depletionRate * deltaTime;
        // Water Capacity (farm upgrade) raises the ceiling above 100; depletion never lifts moisture,
        // so the upper clamp only matters alongside watering/rain, but we use it consistently.
        currentMoisture = Mathf.Clamp(currentMoisture, 0f, FarmUpgrades.MaxMoisture);

        if (currentMoisture <= 0f)
        {
            dryOutTimer += deltaTime;

            // Drying Grace (farm upgrade): longer grace before dry-out damage begins.
            float dryThreshold = GameConstants.Instance.dryOutThreshold * FarmUpgrades.DryingGraceMultiplier;
            if (dryOutTimer >= dryThreshold)
            {
                if (!isDriedOut)
                {
                    isDriedOut = true;
                    if (visuals != null)
                        visuals.UpdateVisuals(currentStage, cropData, isDriedOut);
                }

                // Slow Decay (farm upgrade): slower HP loss while parched.
                currentHP -= GameConstants.Instance.dryOutDecayRate * deltaTime / Mathf.Max(0.01f, FarmUpgrades.SlowDecayDivisor);
                currentHP = Mathf.Max(currentHP, 0f);
                
                if (currentHP <= 0f)
                    Die("dry-out");
            }
        }
        else
        {
            dryOutTimer = 0f;
            
            if (isDriedOut)
            {
                isDriedOut = false;
                if (visuals != null)
                    visuals.UpdateVisuals(currentStage, cropData, isDriedOut);
            }
        }
    }

    private void UpdateHarvestWindow(float deltaTime)
    {
        harvestWindowTimer -= deltaTime;

        if (harvestWindowTimer <= 0f)
        {
            isInHarvestWindow = false;
            isRotting = true;

            if (visuals != null)
                visuals.UpdateVisuals(currentStage, cropData, isDriedOut, isRotting);
        }
    }

    private void UpdateRot(float deltaTime)
    {
        if (GameConstants.Instance == null) return;

        // Rot Resistance (farm upgrade): slower rot HP loss.
        currentHP -= GameConstants.Instance.rotDecayRate * deltaTime / Mathf.Max(0.01f, FarmUpgrades.RotResistanceDivisor);
        currentHP = Mathf.Max(currentHP, 0f);

        if (currentHP <= 0f)
            Die("rot");
    }

    private void AdvanceToNextStage()
    {
        switch (currentStage)
        {
            case GrowthStage.Seed:
                currentStage = GrowthStage.Sprout;
                stageTimer = cropData.GetStageTime(GrowthStage.Sprout);
                break;

            case GrowthStage.Sprout:
                currentStage = GrowthStage.Sapling;
                stageTimer = cropData.GetStageTime(GrowthStage.Sapling);
                break;

            case GrowthStage.Sapling:
                currentStage = GrowthStage.Harvestable;
                stageTimer = 0f;
                isGrowing = false;
                isInHarvestWindow = true;
                harvestWindowTimer = cropData.harvestWindowSeconds;
                break;
        }

        if (visuals != null)
            visuals.UpdateVisuals(currentStage, cropData, isDriedOut, isRotting);
    }

    public void Water()
    {
        if (GameConstants.Instance == null) return;

        float waterAmount = GameConstants.Instance.manualWaterAmount;
        if (ResearchManager.Instance != null)
            waterAmount *= 1f + ResearchManager.Instance.GetBonus(Research.StatKey.HelperWaterEfficiency);
        // Watering Power (farm upgrade): each watering adds more moisture.
        waterAmount *= FarmUpgrades.WateringPowerMultiplier;

        bool wasAtMax = currentMoisture >= 100f;
        currentMoisture += waterAmount;
        // Water Capacity (farm upgrade) lets moisture exceed 100 up to the buffer ceiling.
        currentMoisture = Mathf.Clamp(currentMoisture, 0f, FarmUpgrades.MaxMoisture);

        // Max Water Heals Plant HP (binary research)
        if (wasAtMax
            && ResearchManager.Instance != null
            && ResearchManager.Instance.IsFeatureUnlocked(Research.FeatureFlag.MaxWaterHealsPlant)
            && cropData != null)
        {
            float maxHpWithBonus = cropData.maxHP * (1f + ResearchManager.Instance.GetBonus(Research.StatKey.CropHp));
            currentHP = Mathf.Min(maxHpWithBonus, currentHP + maxHpWithBonus * 0.1f);
        }

        if (RunStats.Instance != null) RunStats.Instance.AddPlantWatered();

        bool wasDriedOut = isDriedOut;
        isDriedOut = false;
        dryOutTimer = 0f;

        if (wasDriedOut && visuals != null)
            visuals.UpdateVisuals(currentStage, cropData, isDriedOut);

    }

    public void Harvest()
    {
        if (!IsHarvestable)
        {
            Debug.LogWarning($"Cannot harvest {cropData.cropName} - not ready!");
            return;
        }

        int harvestValue = cropData.harvestValue;
        if (GameConstants.Instance != null)
            harvestValue = GameConstants.Instance.CalculateHarvestValue(cropData.harvestValue, isRotting);

        if (ResearchManager.Instance != null)
        {
            float sellBonus =
                ResearchManager.Instance.GetBonus(Research.StatKey.CropBonusSellAmount)
                + ResearchManager.Instance.GetBonus(Research.StatKey.SoilQuality)
                + ResearchManager.Instance.GetBonus(Research.StatKey.HelperHarvestEfficiency);
            harvestValue = Mathf.RoundToInt(harvestValue * (1f + sellBonus));
        }

        // Farm upgrades: Fertilizer A × Soil Quality × Zone Level (multiplicative), plus a
        // Bountiful Harvest crit roll that doubles the whole yield (cash AND coins) for this harvest.
        int zone = parentTile != null ? parentTile.ZoneID : 1;
        bool bountiful = Random.value < FarmUpgrades.BountifulChance;
        harvestValue = Mathf.RoundToInt(harvestValue * FarmUpgrades.CashYieldMultiplier(zone));
        if (bountiful) harvestValue *= 2;

        // Cannery intake (Pantry Economy §4a): a diverted harvest becomes jar progress
        // instead of cash + banked coins. Stats/refund/regrow below are unaffected.
        bool divertedToCannery = CanneryManager.Instance != null && CanneryManager.Instance.TryIntake(cropData);
        if (divertedToCannery)
            FloatingTextManager.ShowCanneryIntake(transform.position);

        if (!divertedToCannery && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMoney(harvestValue);
            FloatingTextManager.ShowMoney(harvestValue, transform.position);
        }

        // Bank permanent coins for this harvest (the "keep" currency). Scaled by coin research.
        int coinGain = 0;
        if (!divertedToCannery && CurrencyManager.Instance != null && cropData.coinValue > 0)
        {
            coinGain = cropData.coinValue;
            if (ResearchManager.Instance != null)
            {
                float coinBonus = ResearchManager.Instance.GetBonus(Research.StatKey.CropBonusCoinAmount);
                coinGain = Mathf.RoundToInt(coinGain * (1f + coinBonus));
            }
            // Farm upgrades: Fertilizer B × Soil Quality × Zone Level, doubled on a Bountiful crit.
            coinGain = Mathf.RoundToInt(coinGain * FarmUpgrades.CoinYieldMultiplier(zone));
            if (bountiful) coinGain *= 2;
            coinGain = Mathf.Max(1, coinGain);
            CurrencyManager.Instance.AddCoins(coinGain);
            // Stagger 0.35s after the cash pop and nudge up so both numbers stay readable.
            FloatingTextManager.ShowCoins(coinGain, transform.position + Vector3.up * 0.4f, 0.35f);
            if (RunStats.Instance != null) RunStats.Instance.AddCoinsBanked(coinGain);
        }

        if (RunStats.Instance != null) RunStats.Instance.AddCropHarvested(cropData);

        // Per-zone card: harvest count always; money/coins only when actually paid out
        // (a cannery-diverted harvest pays jar progress, not currency).
        if (RunStats.Instance != null)
            RunStats.Instance.AddZoneHarvest(zone, cropData,
                divertedToCannery ? 0 : harvestValue,
                divertedToCannery ? 0 : coinGain);

        // Seed Refund (farm upgrade): chance to hand back a seed, easing the fuel/bankruptcy pressure.
        if (SeedInventory.Instance != null && cropData != null
            && Random.value < FarmUpgrades.SeedRefundChance)
        {
            SeedInventory.Instance.RefundSeed(cropData, 1);
        }

        isInHarvestWindow = false;
        isRotting = false;
        harvestWindowTimer = 0f;

        if (cropData.canRegrow)
            StartRegrowth();
        else
            RemovePlant();
    }

    private void StartRegrowth()
    {
        currentStage = GrowthStage.Seed;
        stageTimer = cropData.regrowSeconds > 0 ? cropData.regrowSeconds : cropData.GetStageTime(GrowthStage.Seed);
        isGrowing = true;

        currentMoisture = 100f;
        isDriedOut = false;
        dryOutTimer = 0f;
        isInHarvestWindow = false;
        isRotting = false;
        harvestWindowTimer = 0f;

        if (visuals != null)
        {
            visuals.UpdateVisuals(currentStage, cropData, isDriedOut, isRotting);

            if (cropData.harvestedSprite != null)
            {
                visuals.GetComponent<SpriteRenderer>().sprite = cropData.harvestedSprite;
            }
        }

    }

    private void RemovePlant()
    {
        if (parentTile != null)
            parentTile.ClearPlant();

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Phase 6: Threat & Weather Damage
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply damage from an animal threat (crow, deer) or weather effect (lightning, wind).
    /// Stage multipliers are applied by the caller before this is invoked.
    /// </summary>
    public void TakeDamage(float damage, string cause = "threat/weather damage")
    {
        if (damage <= 0f) return;

        // Crop Hardiness (farm upgrade): reduce incoming threat/weather damage.
        damage /= Mathf.Max(0.01f, FarmUpgrades.HardinessDivisor);

        currentHP -= damage;
        currentHP  = Mathf.Max(currentHP, 0f);

        if (currentHP <= 0f)
            Die(cause);
    }

    /// <summary>
    /// Apply rain moisture directly to the moisture system.
    /// Feeds into existing currentMoisture — recovers dried-out state if moisture rises above 0.
    /// Called by ThunderstormManager on each rain tick.
    /// </summary>
    public void ApplyRain(float amount)
    {
        if (amount <= 0f) return;

        currentMoisture += amount;
        currentMoisture  = Mathf.Clamp(currentMoisture, 0f, FarmUpgrades.MaxMoisture);

        // If rain rescues a dried-out plant, recover it
        if (isDriedOut && currentMoisture > 0f)
        {
            isDriedOut  = false;
            dryOutTimer = 0f;
            if (visuals != null)
                visuals.UpdateVisuals(currentStage, cropData, isDriedOut);
        }
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired exactly when a plant's lifecycle ends WITHOUT being harvested
    /// (dry-out, rot, lightning/wind/threat damage). Compost Bay listens to this
    /// to credit compost for the dying crop. zoneID = plant's zone; cropTier = crop.tier;
    /// worldPos = position of the dying plant (for VFX).
    /// </summary>
    public static event System.Action<int, int, Vector3> OnPlantDied;

    /// <summary>
    /// Plant dies. Cause string is used for debug logging only.
    /// Default "unknown" keeps all internal call sites valid without changes.
    /// </summary>
    private void Die(string cause = "unknown")
    {
        Debug.Log($"💀 {cropData.cropName} DIED from {cause}!");

        if (RunStats.Instance != null)
            RunStats.Instance.AddPlantDeath(parentTile != null ? parentTile.ZoneID : -1, cropData, cause);

        if (parentTile != null && cropData != null)
            OnPlantDied?.Invoke(parentTile.ZoneID, cropData.tier, transform.position);

        RemovePlant();
    }

    private void OnMouseDown()
    {
        if (IsHarvestable) Harvest();
        else               Water();
    }

#if UNITY_EDITOR
    [ContextMenu("Show Plant Info")]
    private void ShowPlantInfo()
    {
        Debug.Log($"=== {cropData.cropName} ===");
        Debug.Log($"Stage: {currentStage}");
        Debug.Log($"Timer: {stageTimer:F1}s");
        Debug.Log($"HP: {currentHP}/{cropData.maxHP}");
        Debug.Log($"Moisture: {currentMoisture:F1}%");
        Debug.Log($"Growth Speed: {CurrentGrowthSpeed:F2}x");
        Debug.Log($"Dried Out: {isDriedOut}");
        Debug.Log($"In Harvest Window: {isInHarvestWindow}");
        if (isInHarvestWindow)
            Debug.Log($"  Window Time Left: {harvestWindowTimer:F1}s");
        Debug.Log($"Rotting: {isRotting}");
    }

    [ContextMenu("Force Harvestable")]
    private void ForceHarvestable()
    {
        currentStage = GrowthStage.Harvestable;
        stageTimer = 0f;
        isGrowing = false;
        if (visuals != null)
            visuals.UpdateVisuals(currentStage, cropData);
    }

    [ContextMenu("Force Dry (0% Moisture)")]
    private void ForceDry()
    {
        currentMoisture = 0f;
        Debug.Log($"Forced dry");
    }

    [ContextMenu("Force Dried Out State")]
    private void ForceDriedOut()
    {
        currentMoisture = 0f;
        dryOutTimer = 999f;
        isDriedOut = true;
        Debug.Log($"Forced dried-out state");
    }
#endif
}