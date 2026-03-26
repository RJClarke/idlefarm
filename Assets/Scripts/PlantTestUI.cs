using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dev Tools drawer — collapsible panel with debug buttons and info displays.
/// Toggle button stays visible; panel slides open/closed on click.
/// </summary>
public class PlantTestUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlantTestManager testManager;

    [Header("Toggle Button")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TextMeshProUGUI toggleButtonText;

    [Header("Drawer Panel")]
    [SerializeField] private GameObject drawerPanel;

    [Header("Dev Buttons")]
    [SerializeField] private Button speedUpButton;
    [SerializeField] private Button spawnCrowButton;
    [SerializeField] private Button spawnDeerButton;

    [Header("Info Display")]
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Helper Info")]
    [SerializeField] private TextMeshProUGUI helperInfoText;

    [Header("Time Control")]
    [SerializeField] private float normalTimeScale = 1f;
    [SerializeField] private float fastTimeScale = 20f;
    private bool isSpeedUp = false;

    private bool isOpen = false;

    private void Start()
    {
        if (testManager == null)
            testManager = FindFirstObjectByType<PlantTestManager>();

        // Toggle button
        if (toggleButton != null)
            toggleButton.onClick.AddListener(OnToggleClicked);

        // Dev buttons
        if (speedUpButton != null)
            speedUpButton.onClick.AddListener(OnSpeedUpClicked);
        if (spawnCrowButton != null)
            spawnCrowButton.onClick.AddListener(OnSpawnCrowClicked);
        if (spawnDeerButton != null)
            spawnDeerButton.onClick.AddListener(OnSpawnDeerClicked);

        // Start closed
        isOpen = false;
        if (drawerPanel != null)
            drawerPanel.SetActive(false);
        UpdateToggleText();
        UpdateSpeedButtonText();
    }

    private void Update()
    {
        if (!isOpen) return;

        if (infoText != null)
            UpdateInfoDisplay();
        if (helperInfoText != null)
            UpdateHelperDisplay();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Toggle
    // ─────────────────────────────────────────────────────────────────────

    private void OnToggleClicked()
    {
        isOpen = !isOpen;
        if (drawerPanel != null)
            drawerPanel.SetActive(isOpen);
        UpdateToggleText();
    }

    private void UpdateToggleText()
    {
        if (toggleButtonText != null)
            toggleButtonText.text = isOpen ? "Close Dev Tools" : "Dev Tools";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Button Handlers
    // ─────────────────────────────────────────────────────────────────────

    private void OnSpeedUpClicked()
    {
        isSpeedUp = !isSpeedUp;
        Time.timeScale = isSpeedUp ? fastTimeScale : normalTimeScale;
        UpdateSpeedButtonText();
    }

    private void OnSpawnCrowClicked()
    {
        if (ThreatWaveManager.Instance != null)
            ThreatWaveManager.Instance.ForceSpawnCrow();
    }

    private void OnSpawnDeerClicked()
    {
        if (ThreatWaveManager.Instance != null)
            ThreatWaveManager.Instance.ForceSpawnDeer();
    }

    private void UpdateSpeedButtonText()
    {
        if (speedUpButton != null)
        {
            var txt = speedUpButton.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = isSpeedUp ? "Normal Speed" : $"Speed {fastTimeScale}x";
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Info Displays
    // ─────────────────────────────────────────────────────────────────────

    private void UpdateInfoDisplay()
    {
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);

        int seeds = 0, sprouts = 0, saplings = 0, harvestable = 0;
        float avgMoisture = 0f;
        int lowMoisture = 0;
        int driedOut = 0;
        int rotting = 0;

        foreach (Plant plant in allPlants)
        {
            switch (plant.CurrentStage)
            {
                case GrowthStage.Seed: seeds++; break;
                case GrowthStage.Sprout: sprouts++; break;
                case GrowthStage.Sapling: saplings++; break;
                case GrowthStage.Harvestable: harvestable++; break;
            }
            avgMoisture += plant.CurrentMoisture;
            if (plant.CurrentMoisture < 30f) lowMoisture++;
            if (plant.IsDriedOut) driedOut++;
            if (plant.IsRotting) rotting++;
        }

        if (allPlants.Length > 0) avgMoisture /= allPlants.Length;

        string info = $"<b>FARM</b>  Plants: {allPlants.Length}";
        info += $"  S:{seeds} Sp:{sprouts} Sa:{saplings} H:{harvestable}";
        if (rotting > 0) info += $"  <color=orange>Rot:{rotting}</color>";
        info += $"\nMoisture: {avgMoisture:F0}%";
        if (driedOut > 0) info += $"  <color=red>Dried:{driedOut}</color>";
        else if (lowMoisture > 0) info += $"  <color=orange>Low:{lowMoisture}</color>";

        if (ThreatWaveManager.Instance != null)
        {
            info += $"\n<b>THREATS</b>  Wave: {ThreatWaveManager.Instance.CurrentWave}";
            info += $"  Hunger: {ThreatWaveManager.Instance.CurrentHunger:F0}";
        }

        if (isSpeedUp)
            info += $"\n<color=yellow>TIME: {fastTimeScale}x</color>";

        infoText.text = info;
    }

    private void UpdateHelperDisplay()
    {
        if (HelperManager.Instance == null) return;

        int total = HelperManager.Instance.GetHelperCount();
        int idle = HelperManager.Instance.GetIdleHelperCount();
        int working = total - idle;
        int tasks = HelperManager.Instance.GetPendingTaskCount();

        string info = $"<b>HELPERS</b>  Total:{total}  Idle:{idle}  Working:{working}";
        if (tasks > 0) info += $"  Tasks:{tasks}";

        helperInfoText.text = info;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        Time.timeScale = GameConstants.Instance != null ? GameConstants.Instance.baseGameSpeed : 2f;

        if (toggleButton != null) toggleButton.onClick.RemoveListener(OnToggleClicked);
        if (speedUpButton != null) speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
        if (spawnCrowButton != null) spawnCrowButton.onClick.RemoveListener(OnSpawnCrowClicked);
        if (spawnDeerButton != null) spawnDeerButton.onClick.RemoveListener(OnSpawnDeerClicked);
    }
}
