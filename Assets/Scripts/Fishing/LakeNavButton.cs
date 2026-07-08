using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD button that toggles the camera between Farm and the Lake (fishing area, right of the farm).
/// Stays available during a run (like the Woods) — the Lake is meant to be visited mid-run.
/// Hidden only while at the Market. Mirrors WoodsNavButton.
/// </summary>
public class LakeNavButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string farmLabel = "🎣 Lake";
    [SerializeField] private string lakeLabel = "🌾 Back to Farm";

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
            Debug.LogWarning("[LakeNavButton] No CameraPanController found on Main Camera.");
            return;
        }

        if (button != null) button.onClick.AddListener(OnClick);
        panController.OnPanStarted   += OnPanStarted;
        panController.OnPanCompleted += OnPanCompleted;

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
    }

    private void OnClick()
    {
        if (panController == null) return;
        panController.PanTo(panController.CurrentLocation == CameraPanController.Location.Lake
            ? CameraPanController.Location.Farm
            : CameraPanController.Location.Lake);
    }

    private void OnPanStarted(CameraPanController.Location target) => UpdateVisibility(target);
    private void OnPanCompleted(CameraPanController.Location loc) => UpdateLabel(loc);

    private void UpdateLabel(CameraPanController.Location loc)
    {
        if (label == null) return;
        label.text = loc == CameraPanController.Location.Lake ? lakeLabel : farmLabel;
    }

    private void UpdateVisibility(CameraPanController.Location loc)
    {
        bool hidden = loc == CameraPanController.Location.Market;
        gameObject.SetActive(!hidden);
    }
}
