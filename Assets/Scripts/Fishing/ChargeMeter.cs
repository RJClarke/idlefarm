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

    [Tooltip("Local height of the fillable interior at fill=1 (tune so full fill fills the frame).")]
    [SerializeField] private float interiorHeight = 1.2f;
    [Tooltip("Local Y of the interior bottom (where fill starts and tick=0 sits).")]
    [SerializeField] private float interiorBottomY = -0.6f;
    [Tooltip("Local width the fill should render at inside the frame.")]
    [SerializeField] private float interiorWidth = 0.25f;

    // Cached native sprite size (units at scale 1) so fill scaling is independent of the sprite's
    // pixel dimensions — localScale = desired / native.
    private float fillNativeH = 1f;
    private float fillNativeW = 1f;

    private void Awake()
    {
        CacheFillNative();
        Hide();
    }

    private void CacheFillNative()
    {
        if (fill != null && fill.sprite != null)
        {
            Vector2 s = fill.sprite.bounds.size;
            if (s.y > 1e-4f) fillNativeH = s.y;
            if (s.x > 1e-4f) fillNativeW = s.x;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        SetFill(0f);
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Fill 0..1 grows the green bar upward, bottom-anchored at interiorBottomY. Works with a
    /// center-pivot sprite: the sprite's center is placed half its rendered height above the bottom.</summary>
    public void SetFill(float fill01)
    {
        if (fill == null) return;
        float f = Mathf.Clamp01(fill01);
        float renderedH = f * interiorHeight;
        fill.transform.localScale = new Vector3(interiorWidth / fillNativeW, renderedH / fillNativeH, 1f);
        var p = fill.transform.localPosition;
        p.y = interiorBottomY + renderedH * 0.5f; // bottom edge sits at interiorBottomY
        fill.transform.localPosition = p;
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
