using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketNavButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string farmLabel = "🏪 Market";
    [SerializeField] private string marketLabel = "🌾 Back to Farm";

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
            Debug.LogWarning("[MarketNavButton] No CameraPanController found on Main Camera.");
            return;
        }

        if (button != null) button.onClick.AddListener(OnClick);
        panController.OnPanCompleted += OnPanCompleted;
        UpdateLabel(panController.CurrentLocation);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
        if (panController != null) panController.OnPanCompleted -= OnPanCompleted;
    }

    private void OnClick()
    {
        if (panController == null) return;
        panController.PanTo(panController.CurrentLocation == CameraPanController.Location.Market
            ? CameraPanController.Location.Farm
            : CameraPanController.Location.Market);
    }

    private void OnPanCompleted(CameraPanController.Location loc) => UpdateLabel(loc);

    private void UpdateLabel(CameraPanController.Location loc)
    {
        if (label == null) return;
        label.text = loc == CameraPanController.Location.Market ? marketLabel : farmLabel;
    }
}
