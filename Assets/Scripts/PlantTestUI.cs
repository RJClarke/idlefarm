using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Visual UI for testing plant system with buttons
/// Phase 3.1: Now includes watering controls
/// </summary>
public class PlantTestUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlantTestManager testManager;

    [Header("UI Buttons")]
    [SerializeField] private Button tillAllButton;
    [SerializeField] private Button plantOneButton;
    [SerializeField] private Button fillGridButton;
    [SerializeField] private Button clearAllButton;
    [SerializeField] private Button speedUpButton;
    
    [Header("Phase 3.1: Water Buttons")]
    [SerializeField] private Button waterAllButton;
    [SerializeField] private Button waterLowButton;

    [Header("Info Display")]
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Time Control")]
    [SerializeField] private float normalTimeScale = 1f;
    [SerializeField] private float fastTimeScale = 10f;
    private bool isSpeedUp = false;

    private void Start()
    {
        // Auto-find test manager if not assigned
        if (testManager == null)
        {
            testManager = FindFirstObjectByType<PlantTestManager>();
        }

        // Hook up button events
        if (tillAllButton != null)
            tillAllButton.onClick.AddListener(OnTillAllClicked);
        
        if (plantOneButton != null)
            plantOneButton.onClick.AddListener(OnPlantOneClicked);
        
        if (fillGridButton != null)
            fillGridButton.onClick.AddListener(OnFillGridClicked);
        
        if (clearAllButton != null)
            clearAllButton.onClick.AddListener(OnClearAllClicked);
        
        if (speedUpButton != null)
            speedUpButton.onClick.AddListener(OnSpeedUpClicked);

        // Phase 3.1: Water buttons
        if (waterAllButton != null)
            waterAllButton.onClick.AddListener(OnWaterAllClicked);
        
        if (waterLowButton != null)
            waterLowButton.onClick.AddListener(OnWaterLowClicked);

        // Update button text
        UpdateSpeedButtonText();
    }

    private void Update()
    {
        // Update info display
        if (infoText != null)
        {
            UpdateInfoDisplay();
        }
    }

    private void OnTillAllClicked()
    {
        if (testManager != null)
        {
            testManager.TillAllTiles();
        }
    }

    private void OnPlantOneClicked()
    {
        if (testManager != null)
        {
            testManager.PlantRandomTestCrop();
        }
    }

    private void OnFillGridClicked()
    {
        if (testManager != null)
        {
            testManager.FillGridWithTestCrops();
        }
    }

    private void OnClearAllClicked()
    {
        if (testManager != null)
        {
            testManager.ClearAllPlants();
        }
    }

    private void OnSpeedUpClicked()
    {
        isSpeedUp = !isSpeedUp;
        Time.timeScale = isSpeedUp ? fastTimeScale : normalTimeScale;
        UpdateSpeedButtonText();
        
    }

    // Phase 3.1: Water button handlers
    private void OnWaterAllClicked()
    {
        if (testManager != null)
        {
            testManager.WaterAllPlants();
        }
    }

    private void OnWaterLowClicked()
    {
        if (testManager != null)
        {
            testManager.WaterLowMoisturePlants();
        }
    }

    private void UpdateSpeedButtonText()
    {
        if (speedUpButton != null)
        {
            TextMeshProUGUI buttonText = speedUpButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isSpeedUp ? $"Normal Speed" : $"Speed Up ({fastTimeScale}x)";
            }
        }
    }

    private void UpdateInfoDisplay()
    {
        // Count plants by stage
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        
        int seeds = 0, sprouts = 0, saplings = 0, harvestable = 0;
        float avgMoisture = 0f;
        float avgSpeed = 0f; // Phase 3.2
        int lowMoisture = 0; // Below 30%
        int stoppedGrowth = 0; // Phase 3.2: At 0% moisture
        int driedOut = 0; // Phase 3.3: Taking damage
        int inHarvestWindow = 0; // Phase 4.1: Ready to harvest
        int rotting = 0; // Phase 4.2: Losing HP from rot
        
        foreach (Plant plant in allPlants)
        {
            switch (plant.CurrentStage)
            {
                case GrowthStage.Seed: seeds++; break;
                case GrowthStage.Sprout: sprouts++; break;
                case GrowthStage.Sapling: saplings++; break;
                case GrowthStage.Harvestable: harvestable++; break;
            }

            // Phase 3.1: Track moisture
            avgMoisture += plant.CurrentMoisture;
            if (plant.CurrentMoisture < 30f)
            {
                lowMoisture++;
            }
            
            // Phase 3.2: Track growth speed
            if (plant.CurrentStage != GrowthStage.Harvestable)
            {
                avgSpeed += plant.CurrentGrowthSpeed;
                if (plant.CurrentGrowthSpeed == 0f)
                {
                    stoppedGrowth++;
                }
            }
            
            // Phase 3.3: Track dried out plants
            if (plant.IsDriedOut)
            {
                driedOut++;
            }

            // Phase 4: Track harvest window and rot
            if (plant.IsInHarvestWindow)
            {
                inHarvestWindow++;
            }
            if (plant.IsRotting)
            {
                rotting++;
            }
        }

        int growingPlants = allPlants.Length - harvestable;
        if (allPlants.Length > 0)
        {
            avgMoisture /= allPlants.Length;
        }
        if (growingPlants > 0)
        {
            avgSpeed /= growingPlants;
        }

        // Count tiles
        int tilledTiles = 0;
        if (FarmGrid.Instance != null)
        {
            tilledTiles = FarmGrid.Instance.GetPlantableTiles().Count;
        }

        string info = $"<b>FARM STATUS</b>\n";
        info += $"Tilled Tiles: {tilledTiles}\n";
        info += $"Total Plants: {allPlants.Length}\n";
        info += $"\n<b>GROWTH STAGES</b>\n";
        info += $"🟤 Seeds: {seeds}\n";
        info += $"🟢 Sprouts: {sprouts}\n";
        info += $"🌿 Saplings: {saplings}\n";
        info += $"⭐ Harvestable: {harvestable}\n";
        
        // Phase 4.1: Harvest window info
        if (inHarvestWindow > 0)
        {
            info += $"⏰ <color=yellow>In Window: {inHarvestWindow}</color>\n";
        }
        
        // Phase 4.2: Rot warning
        if (rotting > 0)
        {
            info += $"🍂 <color=orange>ROTTING: {rotting}</color>\n";
        }
        
        // Phase 3.1: Moisture stats
        info += $"\n<b>MOISTURE</b>\n";
        info += $"💧 Average: {avgMoisture:F0}%\n";
        
        if (driedOut > 0)
        {
            info += $"💀 <color=red>DRIED OUT: {driedOut}</color>\n";
        }
        else if (lowMoisture > 0)
        {
            info += $"⚠️ <color=orange>Low: {lowMoisture} plants</color>\n";
        }
        else
        {
            info += $"[OK] All plants hydrated\n";
        }
        
        // Phase 3.2: Growth speed stats
        info += $"\n<b>GROWTH SPEED</b>\n";
        info += $"⚡ Average: {avgSpeed:F2}x\n";
        
        if (stoppedGrowth > 0)
        {
            info += $"🛑 <color=red>Stopped: {stoppedGrowth} plants</color>\n";
        }
        else if (avgSpeed > 1.2f)
        {
            info += $"[OK] <color=green>Fast growth!</color>\n";
        }
        else if (avgSpeed < 0.5f && growingPlants > 0)
        {
            info += $"⚠️ <color=yellow>Slow growth</color>\n";
        }
        else
        {
            info += $"[OK] Normal growth\n";
        }
        
        if (isSpeedUp)
        {
            info += $"\n⚡ <color=yellow>TIME: {fastTimeScale}x SPEED</color>";
        }

        infoText.text = info;
    }

    private void OnDestroy()
    {
        // Reset time scale when destroyed
        Time.timeScale = normalTimeScale;

        // Unhook button events
        if (tillAllButton != null)
            tillAllButton.onClick.RemoveListener(OnTillAllClicked);
        
        if (plantOneButton != null)
            plantOneButton.onClick.RemoveListener(OnPlantOneClicked);
        
        if (fillGridButton != null)
            fillGridButton.onClick.RemoveListener(OnFillGridClicked);
        
        if (clearAllButton != null)
            clearAllButton.onClick.RemoveListener(OnClearAllClicked);
        
        if (speedUpButton != null)
            speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);

        if (waterAllButton != null)
            waterAllButton.onClick.RemoveListener(OnWaterAllClicked);
        
        if (waterLowButton != null)
            waterLowButton.onClick.RemoveListener(OnWaterLowClicked);
    }
}