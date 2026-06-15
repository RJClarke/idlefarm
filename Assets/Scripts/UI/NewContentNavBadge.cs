using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a small red "NEW" dot in the top-right corner of a uGUI element when there is
/// unseen content of the configured kind. Attach to the Equipment bottom-nav button.
/// The dot is created at runtime; no scene wiring beyond adding this component.
/// </summary>
public class NewContentNavBadge : MonoBehaviour
{
    public enum Source { Equipment, Research }

    [SerializeField] private Source source = Source.Equipment;
    [SerializeField] private float size = 18f;
    [SerializeField] private Vector2 inset = new Vector2(6f, 6f);

    private GameObject dot;

    private void Start()
    {
        CreateDot();
        if (NewContentTracker.Instance != null) NewContentTracker.Instance.OnChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (NewContentTracker.Instance != null) NewContentTracker.Instance.OnChanged -= Refresh;
    }

    private void CreateDot()
    {
        dot = new GameObject("NewBadgeDot", typeof(RectTransform), typeof(Image));
        var rt = dot.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); // top-right of the button
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(-inset.x, -inset.y);

        var img = dot.GetComponent<Image>();
        img.sprite = NewContentTracker.CreateCircleSprite(48);
        img.color = new Color(0.92f, 0.22f, 0.22f);
        img.raycastTarget = false;
    }

    private void Refresh()
    {
        if (dot == null) return;
        bool show = NewContentTracker.Instance != null && (source == Source.Equipment
            ? NewContentTracker.Instance.HasUnseenEquipment()
            : NewContentTracker.Instance.HasUnseenResearch());
        dot.SetActive(show);
    }
}
