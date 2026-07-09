using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared base for the map navigation buttons (Greenhouse / Lake / Woods / Market). Holds the
/// button + label references, the editable pan target, and the toggle-to-Farm click behaviour.
/// Each subclass names its <see cref="Target"/> location and any visibility gates.
///
/// The button is the source of truth for WHERE it pans: <see cref="panOffset"/> is pushed into the
/// CameraPanController on Start and again on every click, so you can retune the X/Y in the inspector
/// (even while playing) and the next tap frames the new spot — no need to dig into CameraPanController.
/// </summary>
public abstract class MapNavButton : MonoBehaviour
{
    [SerializeField] protected Button button;
    [SerializeField] protected TMP_Text label;

    [Header("Labels")]
    [SerializeField] protected string farmLabel = "Go";
    [SerializeField] protected string atLabel = "🌾 Back to Farm";

    [Header("Pan Target")]
    [Tooltip("World offset (from the Farm view) the camera moves to when this button pans to its " +
             "location. Edit X/Y to reframe. Applied on Start and on each click, so it can be tuned live.")]
    [SerializeField] protected Vector2 panOffset;

    protected CameraPanController panController;

    /// <summary>The camera location this button toggles to (and back from).</summary>
    protected abstract CameraPanController.Location Target { get; }

    protected virtual void Reset()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>();
    }

    protected virtual void Start()
    {
        panController = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (panController == null)
        {
            Debug.LogWarning($"[{GetType().Name}] No CameraPanController found on Main Camera.");
            return;
        }

        panController.SetLocationOffset(Target, panOffset);
        if (button != null) button.onClick.AddListener(OnClick);
        panController.OnPanStarted   += OnPanStarted;
        panController.OnPanCompleted += OnPanCompleted;
        SubscribeExtra();

        UpdateLabel(panController.CurrentLocation);
        RefreshVisibility();
    }

    protected virtual void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
        if (panController != null)
        {
            panController.OnPanStarted   -= OnPanStarted;
            panController.OnPanCompleted -= OnPanCompleted;
        }
        UnsubscribeExtra();
    }

    private void OnClick()
    {
        if (panController == null) return;
        // Re-push the (possibly retuned) target, then zip straight there — or back to Farm if we're
        // already at this location — without routing through the Farm view first.
        panController.SetLocationOffset(Target, panOffset);
        panController.PanTo(panController.CurrentLocation == Target
            ? CameraPanController.Location.Farm
            : Target);
    }

    private void OnPanStarted(CameraPanController.Location target) => RefreshVisibility(target);
    private void OnPanCompleted(CameraPanController.Location loc) => UpdateLabel(loc);

    protected void UpdateLabel(CameraPanController.Location loc)
    {
        if (label == null) return;
        label.text = loc == Target ? atLabel : farmLabel;
    }

    protected void RefreshVisibility() =>
        RefreshVisibility(panController != null ? panController.CurrentLocation : CameraPanController.Location.Farm);

    protected virtual void RefreshVisibility(CameraPanController.Location current)
        => gameObject.SetActive(!ShouldHide(current));

    /// <summary>Whether the button should be hidden given the current/target location. Base: never.</summary>
    protected virtual bool ShouldHide(CameraPanController.Location current) => false;

    /// <summary>Hook for subclass-specific event subscriptions (building/run state).</summary>
    protected virtual void SubscribeExtra() { }
    protected virtual void UnsubscribeExtra() { }
}
