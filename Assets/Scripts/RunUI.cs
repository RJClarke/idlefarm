using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays run information and handles end run button
/// </summary>
public class RunUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private Button startRunButton;
    [SerializeField] private Button endRunButton;
    [SerializeField] private Button equipFieldsButton;

    [Header("Display Settings")]
    [SerializeField] private bool showTimer = true;

    private bool hasShownInitialEquipment = false;

    private void Start()
    {
        if (startRunButton != null)
            startRunButton.onClick.AddListener(OnStartRunButtonClicked);

        if (endRunButton != null)
            endRunButton.onClick.AddListener(OnEndRunButtonClicked);

        if (equipFieldsButton != null)
            equipFieldsButton.onClick.AddListener(OnEquipFieldsClicked);

        UpdateButtonStates();
    }

    private void Update()
    {
        // Show saved equipment on first frame after all singletons are ready
        if (!hasShownInitialEquipment && SeedSelectionPopup.Instance != null
            && EquipmentManager.Instance != null && FarmGrid.Instance != null
            && HelperManager.Instance != null)
        {
            hasShownInitialEquipment = true;
            ShowSavedEquipment();
            HelperManager.Instance.ShowHomeScreenHelpers();
        }

        // Update timer display every frame
        if (showTimer && runTimerText != null && RunManager.Instance != null)
        {
            if (RunManager.Instance.IsRunActive)
            {
                runTimerText.text = "Run Time: " + RunManager.Instance.GetFormattedRunDuration();
            }
            else
            {
                runTimerText.text = "No Active Run";
            }
        }

        // Update button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (RunManager.Instance == null) return;

        bool inRun = RunManager.Instance.IsRunActive;

        if (startRunButton != null)
            startRunButton.gameObject.SetActive(!inRun);

        if (endRunButton != null)
            endRunButton.gameObject.SetActive(inRun);

        // Equip Fields only visible on home screen (pre-run)
        if (equipFieldsButton != null)
            equipFieldsButton.gameObject.SetActive(!inRun);
    }

    private void OnStartRunButtonClicked()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.StartNewRun();
    }

    private void OnEndRunButtonClicked()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.EndRun();
    }

    private void OnEquipFieldsClicked()
    {
        if (SeedSelectionPopup.Instance != null)
        {
            SeedSelectionPopup.Instance.OnSelectionSaved += OnFieldsSaved;
            SeedSelectionPopup.Instance.Show();
        }
    }

    private void OnFieldsSaved()
    {
        if (SeedSelectionPopup.Instance != null)
            SeedSelectionPopup.Instance.OnSelectionSaved -= OnFieldsSaved;

        // Refresh home screen equipment visuals
        ShowSavedEquipment();
    }

    /// <summary>
    /// Load saved equipment assignments and show visuals on home screen.
    /// </summary>
    private void ShowSavedEquipment()
    {
        if (SeedSelectionPopup.Instance != null)
            SeedSelectionPopup.Instance.LoadAndApplySavedSelections();

        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.ShowHomeScreenEquipment();
    }

    private void OnDestroy()
    {
        if (startRunButton != null)
            startRunButton.onClick.RemoveListener(OnStartRunButtonClicked);
        if (endRunButton != null)
            endRunButton.onClick.RemoveListener(OnEndRunButtonClicked);
        if (equipFieldsButton != null)
            equipFieldsButton.onClick.RemoveListener(OnEquipFieldsClicked);
    }
}