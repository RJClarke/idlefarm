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
        return baseSpeed * (1f + researchBonus);
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

        currentMoisture -= depletionRate * deltaTime;
        currentMoisture = Mathf.Clamp(currentMoisture, 0f, 100f);

        if (currentMoisture <= 0f)
        {
            dryOutTimer += deltaTime;
            
            if (dryOutTimer >= GameConstants.Instance.dryOutThreshold)
            {
                if (!isDriedOut)
                {
                    isDriedOut = true;
                    if (visuals != null)
                        visuals.UpdateVisuals(currentStage, cropData, isDriedOut);
                }
                
                currentHP -= GameConstants.Instance.dryOutDecayRate * deltaTime;
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

        currentHP -= GameConstants.Instance.rotDecayRate * deltaTime;
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

        bool wasAtMax = currentMoisture >= 100f;
        currentMoisture += waterAmount;
        currentMoisture = Mathf.Clamp(currentMoisture, 0f, 100f);

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

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMoney(harvestValue);
            FloatingTextManager.ShowMoney(harvestValue, transform.position);
        }

        if (RunStats.Instance != null) RunStats.Instance.AddCropHarvested();

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
    public void TakeDamage(float damage)
    {
        if (damage <= 0f) return;

        currentHP -= damage;
        currentHP  = Mathf.Max(currentHP, 0f);

        if (currentHP <= 0f)
            Die("threat/weather damage");
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
        currentMoisture  = Mathf.Clamp(currentMoisture, 0f, 100f);

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
    /// Plant dies. Cause string is used for debug logging only.
    /// Default "unknown" keeps all internal call sites valid without changes.
    /// </summary>
    private void Die(string cause = "unknown")
    {
        Debug.Log($"💀 {cropData.cropName} DIED from {cause}!");

        if (RunStats.Instance != null)
        {
            if (cause == "dry-out") RunStats.Instance.AddPlantDehydrated();
            else if (cause == "rot") RunStats.Instance.AddCropDecayed();
        }

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