using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GreenhouseNavButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string farmLabel = "🏡 Greenhouse";
    [SerializeField] private string greenhouseLabel = "🌾 Back to Farm";

    private CameraPanController panController;

    private void Reset()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>();
    }

    private void Start()
    {
        panController = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (panController == null)
        {
            Debug.LogWarning("[GreenhouseNavButton] No CameraPanController found on Main Camera.");
            return;
        }

        if (button != null) button.onClick.AddListener(OnClick);
        panController.OnPanStarted   += OnPanStarted;
        panController.OnPanCompleted += OnPanCompleted;
        BuildingState.OnBuildingBuilt += OnBuildingBuilt;

        UpdateLabel(panController.CurrentLocation);
        UpdateVisibility(panController.CurrentLocation);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
        if (panController != null)
        {
            panController.OnPanStarted   -= OnPanStarted;
            panController.OnPanCompleted -= OnPanCompleted;
        }
        BuildingState.OnBuildingBuilt -= OnBuildingBuilt;
    }

    private void OnClick()
    {
        if (panController == null) return;
        // Zip straight to the Greenhouse from wherever we are (or back to Farm if already there) —
        // no need to return to the Farm view first.
        panController.PanTo(panController.CurrentLocation == CameraPanController.Location.Greenhouse
            ? CameraPanController.Location.Farm
            : CameraPanController.Location.Greenhouse);
    }

    private void OnPanStarted(CameraPanController.Location target) => UpdateVisibility(target);
    private void OnPanCompleted(CameraPanController.Location loc) => UpdateLabel(loc);
    private void OnBuildingBuilt(string _) => UpdateVisibility(panController != null ? panController.CurrentLocation : CameraPanController.Location.Farm);

    private void UpdateLabel(CameraPanController.Location loc)
    {
        if (label == null) return;
        label.text = loc == CameraPanController.Location.Greenhouse ? greenhouseLabel : farmLabel;
    }

    private void UpdateVisibility(CameraPanController.Location loc)
    {
        bool built  = BuildingState.IsBuilt(BuildingState.GreenhouseKey);
        bool hidden = loc == CameraPanController.Location.Market;
        gameObject.SetActive(built && !hidden);
    }
}
