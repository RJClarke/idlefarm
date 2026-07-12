using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Lake's clickable water and the fishing interaction hub (spec 2026-07-12). While Idle, a
/// press-and-hold on the water charges a vertical meter and 2D-aims a cast (touch = direction, meter
/// = distance); release casts. While a line is out (Waiting/Bite), tapping the water reels the
/// bobber a step toward shore — reaching shore banks a biting fish or retrieves an empty line.
/// LakeNode is also the spatial source of truth (cast origin + range + live bobber position) that
/// FishingLineVisual and WhirlpoolManager read, and it forwards whirlpool enter/exit to the manager.
/// Interactable only when the camera is settled at the Lake.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class LakeNode : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.97f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Header("Hit Target")]
    [Tooltip("Object whose collider is used for tap hit-testing. Set this to the painted water tilemap " +
             "so its CompositeCollider2D (the exact water outline) becomes the tappable area. Leave empty " +
             "to fall back to this object's own collider.")]
    [SerializeField] private GameObject waterHitSource;

    [Header("Cast Geometry")]
    [Tooltip("Shore point casts fly out from (the pole). Landing = origin + dir × power × maxCastRange.")]
    [SerializeField] private Transform castOrigin;
    [Tooltip("World distance a full-power cast reaches.")]
    [SerializeField] private float maxCastRange = 6f;

    [Header("Cast UI / Visual")]
    [SerializeField] private ChargeMeter chargeMeter;
    [SerializeField] private FishingLineVisual lineVisual;
    [SerializeField] private SpriteRenderer reticle;        // reticle.png, shown while charging
    [SerializeField] private float chargeFillSeconds = 1.2f;

    [Header("Whirlpool (optional)")]
    [SerializeField] private WhirlpoolManager whirlpool;    // may be null until wired

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Collider2D hitCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;

    // charge gesture state
    private bool charging;
    private float chargeT;              // 0..1 fill
    private Vector2 aimDir = Vector2.up;
    private float aimTargetDist;        // world distance to the pressed spot (for the tick)
    private bool hotspotBiteConsumed;

    public Vector3 CastOrigin => castOrigin != null ? castOrigin.position : transform.position;
    public float MaxCastRange => maxCastRange;

    /// <summary>Current bobber world position: along the cast ray, retreating toward the origin as it reels.</summary>
    public Vector3 CurrentBobberWorldPos()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return CastOrigin;
        float dist = fm.CastPower01 * maxCastRange * fm.ReelProgress01;
        Vector3 dir = new Vector3(fm.CastDir.x, fm.CastDir.y, 0f);
        return CastOrigin + dir * dist;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();

        // Tap hit-testing uses the painted water's collider when wired (its CompositeCollider2D traces
        // the exact tiles you drew); otherwise fall back to this object's own placeholder box.
        hitCollider = ownCollider;
        if (waterHitSource != null)
        {
            Collider2D water = waterHitSource.GetComponent<CompositeCollider2D>();
            if (water == null) water = waterHitSource.GetComponent<Collider2D>();
            if (water != null) hitCollider = water;
        }

        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        TrackWhirlpool();

        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
        { if (charging && !held) CancelCharge(); return; }
        if (UITapBlocker.PointerOverUI(screenPos)) { CancelCharge(); CancelPress(); return; }

        var fm = FishingManager.Instance;
        bool interact = CanInteract() && fm != null;

        if (interact && fm.State == FishingManager.CastState.Idle)
            HandleChargeGesture(screenPos, justPressed, justReleased, held);
        else
            HandleReelGesture(screenPos, justPressed, justReleased);
    }

    // ── Idle: press-hold to charge + 2D-aim, release to cast ─────────────
    private void HandleChargeGesture(Vector2 screenPos, bool justPressed, bool justReleased, bool held)
    {
        var fm = FishingManager.Instance;
        if (justPressed && !charging && PointerHitsSelf(screenPos))
        {
            if (!fm.HasPole) { fm.ShowNoPoleHint(transform.position); return; }
            charging = true; chargeT = 0f;
            Vector3 spot = PointerWorld(screenPos);
            Vector2 to = (Vector2)(spot - CastOrigin);
            aimDir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.up;
            aimTargetDist = Mathf.Min(to.magnitude, maxCastRange);
            if (reticle != null) { reticle.enabled = true; reticle.transform.position = CastOrigin + (Vector3)(aimDir * aimTargetDist); }
            if (chargeMeter != null) { chargeMeter.Show(); chargeMeter.SetTick(maxCastRange > 0f ? aimTargetDist / maxCastRange : 0f); }
            return;
        }
        if (charging && held)
        {
            chargeT = Mathf.Clamp01(chargeT + Time.deltaTime / Mathf.Max(0.01f, chargeFillSeconds));
            if (chargeMeter != null) chargeMeter.SetFill(chargeT);
            return;
        }
        if (charging && justReleased)
        {
            float power = chargeT;
            EndCharge();
            fm.Cast(power, aimDir);
        }
    }

    // ── Waiting/Bite: tap the water to reel a step toward shore ──────────
    private void HandleReelGesture(Vector2 screenPos, bool justPressed, bool justReleased)
    {
        var fm = FishingManager.Instance;
        if (fm == null || !CanInteract()) return;
        if (justPressed && !isPressed && PointerHitsSelf(screenPos))
        {
            isPressed = true;
            spriteRenderer.color = pressTint * baseColor;
            DoTween(baseScale * pressScale, pressDuration);
            return;
        }
        if (justReleased && isPressed)
        {
            bool overSelf = PointerHitsSelf(screenPos);
            CancelPress();
            if (overSelf) fm.Reel();
        }
    }

    // ── Whirlpool: drive dynamic hotspot state from the bobber position ──
    private void TrackWhirlpool()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;
        bool cast = fm.State == FishingManager.CastState.Waiting || fm.State == FishingManager.CastState.Bite;
        bool inside = cast && whirlpool != null && whirlpool.IsInside(CurrentBobberWorldPos());
        fm.SetInHotspot(inside);
        if (lineVisual != null) lineVisual.SetAgitated(inside && cast);

        // consume a whirlpool fish on the rising edge of a hotspot bite
        bool biting = cast && fm.HasBite;
        if (biting && !hotspotBiteConsumed && fm.CaughtFromHotspot)
        { if (whirlpool != null) whirlpool.ConsumeFish(); hotspotBiteConsumed = true; }
        if (!biting) hotspotBiteConsumed = false;
    }

    private void CancelCharge()
    {
        if (!charging) return;
        EndCharge();
    }

    private void EndCharge()
    {
        charging = false; chargeT = 0f;
        if (chargeMeter != null) chargeMeter.Hide();
        if (reticle != null) reticle.enabled = false;
    }

    private static bool TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held)
    {
        screenPos = default; justPressed = false; justReleased = false; held = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            held = true;
            return true;
        }
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justReleased = true;
            return true;
        }
        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            justPressed = Mouse.current.leftButton.wasPressedThisFrame;
            justReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            return justPressed || justReleased || held;
        }
        return false;
    }

    private Vector3 PointerWorld(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return CastOrigin;
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
    }

    private bool PointerHitsSelf(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null || hitCollider == null) return false;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return Physics2D.OverlapPoint(world) == hitCollider;
    }

    private void CancelPress()
    {
        if (!isPressed) return;
        isPressed = false;
        spriteRenderer.color = baseColor;
        DoTween(baseScale, releaseDuration);
    }

    private bool CanInteract()
    {
        CameraPanController pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (pan == null) return true;
        return !pan.IsPanning && pan.CurrentLocation == CameraPanController.Location.Lake;
    }

    private void DoTween(Vector3 target, float duration)
    {
        if (scaleTweenId != -1) LeanTween.cancel(scaleTweenId);
        scaleTweenId = LeanTween.scale(gameObject, target, duration)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => scaleTweenId = -1)
            .id;
    }
}
