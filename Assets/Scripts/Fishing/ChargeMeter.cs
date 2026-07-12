using UnityEngine;

/// <summary>
/// A vertical charge meter for the fishing cast: a gold Craftpix frame (track) with a green fill
/// that grows bottom→top and a red target tick. Pure view — LakeNode drives it via SetFill/SetTick
/// while the player holds to charge. Built from three SpriteRenderers under this object; the fill
/// renderer must use a bottom-pivot sprite so scaling its localScale.y fills upward.
/// </summary>
public class ChargeMeter : MonoBehaviour
{
    [SerializeField] private SpriteRenderer track;  // meter_track (gold frame)
    [SerializeField] private SpriteRenderer fill;   // meter_fill (green, bottom pivot)
    [SerializeField] private SpriteRenderer tick;   // meter_tick (tinted red)

    [Tooltip("World height of the fillable interior at fill=1 (tune to sit inside the frame).")]
    [SerializeField] private float interiorHeight = 1.2f;
    [Tooltip("Local Y of the interior bottom (where fill starts and tick=0 sits).")]
    [SerializeField] private float interiorBottomY = -0.6f;
    [Tooltip("Local X scale applied to the fill (width inside the frame). Preserved when scaling height.")]
    [SerializeField] private float fillWidthScale = 1f;

    private void Awake() => Hide();

    public void Show()
    {
        gameObject.SetActive(true);
        SetFill(0f);
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Fill 0..1 grows the green bar upward from the interior bottom.</summary>
    public void SetFill(float fill01)
    {
        if (fill == null) return;
        float f = Mathf.Clamp01(fill01);
        fill.transform.localScale = new Vector3(fillWidthScale, f * interiorHeight, 1f);
        var p = fill.transform.localPosition; p.y = interiorBottomY; fill.transform.localPosition = p;
    }

    /// <summary>Position the target tick at tick01 up the interior (0 = bottom, 1 = top).</summary>
    public void SetTick(float tick01)
    {
        if (tick == null) return;
        bool show = tick01 > 0.0001f && tick01 < 0.9999f;
        tick.enabled = show;
        var p = tick.transform.localPosition;
        p.y = interiorBottomY + Mathf.Clamp01(tick01) * interiorHeight;
        tick.transform.localPosition = p;
    }
}
