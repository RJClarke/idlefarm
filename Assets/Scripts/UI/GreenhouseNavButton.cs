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
        panController.ToggleFarmGreenhouse();
    }

    private void OnPanCompleted(CameraPanController.Location loc) => UpdateLabel(loc);

    private void UpdateLabel(CameraPanController.Location loc)
    {
        if (label == null) return;
        label.text = loc == CameraPanController.Location.Greenhouse ? greenhouseLabel : farmLabel;
    }
}
