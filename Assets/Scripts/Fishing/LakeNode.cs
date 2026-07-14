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
    [Tooltip("Seconds of hold to reach a full-power cast. Gentle quadratic wind-up over the first " +
             "quarter of the ramp, then CONSTANT speed all the way to max — no end coast.")]
    [SerializeField] private float chargeRampSeconds = 4f;

    [Header("Reel")]
    [Tooltip("Hold this long (line out) to switch from tap-per-step to a continuous slow reel.")]
    [SerializeField] private float holdReelDelay = 0.35f;
    [Tooltip("Reel steps per second while holding — slow, but carries the bobber all the way to " +
             "shore in one smooth motion.")]
    [SerializeField] private float holdReelStepsPerSecond = 1.6f;

    [Header("Whirlpool (optional)")]
    [SerializeField] private WhirlpoolManager whirlpool;    // may be null until wired

    [Header("Fish Icons (index = tier-1: Perch, Bass, Pike)")]
    [Tooltip("Per-tier fish sprites shown in the bite bubble and the catch toast.")]
    [SerializeField] private Sprite[] fishIcons = new Sprite[3];

    // Per-tier fish-name tint for the catch toast (index = tier-1): lake blue, bass green,
    // rare-pike purple.
    private static readonly Color[] FishNameColors =
    {
        new Color(0.24f, 0.50f, 0.75f),
        new Color(0.25f, 0.56f, 0.23f),
        new Color(0.56f, 0.27f, 0.68f),
    };

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Collider2D hitCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;

    [Header("Feel")]
    [Tooltip("Cooldown after a line resolves before a new cast can start — stops mash-taps from " +
             "instantly recasting the moment you finish reeling in.")]
    [SerializeField] private float recastDelay = 0.5f;

    // charge gesture state
    private bool charging;
    private float chargeElapsed;        // seconds held so far
    private float chargeT;              // 0..1 eased fill
    private Vector2 aimDir = Vector2.up;
    private float aimTargetDist;        // world distance to the pressed spot (for the tick)
    private bool hotspotBiteConsumed;
    private float recastReadyTime;      // Time.time before which charging is blocked
    private FishingManager.CastState prevState;

    // reel gesture state
    private bool reelPressActive;       // a press began while the line was out
    private bool reelHolding;           // press has lasted past holdReelDelay → continuous reel
    private float reelPressTime;

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

        // The reticle only exists while aiming a cast — hide it regardless of its authored
        // scene state (left enabled, it sits visible in the middle of the map from boot).
        if (reticle != null) reticle.enabled = false;
    }

    private void Start()
    {
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnCatch += OnFishCaught;
            FishingManager.Instance.OnEmptyReel += OnEmptyReel;
            prevState = FishingManager.Instance.State;
        }
    }

    private void OnDestroy()
    {
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnCatch -= OnFishCaught;
            FishingManager.Instance.OnEmptyReel -= OnEmptyReel;
        }
    }

    /// <summary>Fish sprite for a 1-based tier (null if unwired / out of range).</summary>
    public Sprite FishIcon(int tier)
    {
        int i = tier - 1;
        return fishIcons != null && i >= 0 && i < fishIcons.Length ? fishIcons[i] : null;
    }

    // Celebrate a bank: float "+1 <fish>" over the pole and drop a bottom catch toast with the
    // caught fish's icon and its name tinted per tier. The float uses the TMP sprite icon, not
    // the 🐟 emoji — emoji glyphs go solid black under the floating text's outline.
    private void OnFishCaught(int tier)
    {
        Vector3 polePos = CastOrigin + Vector3.up * 0.6f;
        FloatingTextManager.ShowText("+1 " + CurrencyIcons.Fish, new Color(0.35f, 0.6f, 0.95f), polePos);
        Color nameColor = FishNameColors[Mathf.Clamp(tier - 1, 0, FishNameColors.Length - 1)];
        string hex = ColorUtility.ToHtmlStringRGB(nameColor);
        ToastManager.ShowCatch(FishIcon(tier), $"Caught a <color=#{hex}>{FishTiers.Name(tier)}</color>!");
    }

    // The consolation prize: a grey float over the pole + a bottom toast carrying the equipped
    // pole's icon, so an empty line reads clearly as "no fish" rather than a silent reset.
    private void OnEmptyReel()
    {
        Vector3 polePos = CastOrigin + Vector3.up * 0.6f;
        FloatingTextManager.ShowText("No bites...", new Color(0.78f, 0.80f, 0.84f), polePos);
        var fm = FishingManager.Instance;
        Sprite rod = fm != null ? fm.PoleIcon(fm.PoleLevel) : null;
        ToastManager.ShowCatch(rod, "Nothing was biting...");
    }

    private void Update()
    {
        TrackWhirlpool();

        // Start a short recast cooldown the moment a line resolves back to Idle (anti-mash-cast).
        var fmState = FishingManager.Instance != null ? FishingManager.Instance.State : FishingManager.CastState.Idle;
        if ((prevState == FishingManager.CastState.Waiting || prevState == FishingManager.CastState.Bite)
            && fmState == FishingManager.CastState.Idle)
            recastReadyTime = Time.time + recastDelay;
        prevState = fmState;

        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
        { if (charging && !held) CancelCharge(); return; }
        if (UITapBlocker.PointerOverUI(screenPos)) { CancelCharge(); CancelPress(); return; }

        var fm = FishingManager.Instance;
        bool interact = CanInteract() && fm != null;

        if (interact && fm.State == FishingManager.CastState.Idle)
            HandleChargeGesture(screenPos, justPressed, justReleased, held);
        else
            HandleReelGesture(screenPos, justPressed, justReleased, held);
    }

    // ── Idle: press-hold to charge + 2D-aim, release to cast ─────────────
    private void HandleChargeGesture(Vector2 screenPos, bool justPressed, bool justReleased, bool held)
    {
        var fm = FishingManager.Instance;
        if (justPressed && !charging && Time.time >= recastReadyTime && PointerHitsSelf(screenPos))
        {
            if (!fm.HasPole) { fm.ShowNoPoleHint(transform.position); return; }
            charging = true; chargeElapsed = 0f; chargeT = 0f;
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
            // Unscaled time: charging is a player gesture and must feel identical at any Game Speed.
            chargeElapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(chargeElapsed / Mathf.Max(0.01f, chargeRampSeconds));
            // Quadratic wind-up over the first quarter, then linear to max (slope-continuous at
            // k=c, f(1)=1): a readable slow start with NO slow-down again near the top.
            const float c = 0.25f;
            const float a = 1f / (2f * c - c * c);   // ≈2.29 → fill ≈14% at the c boundary
            chargeT = k <= c ? a * k * k : a * c * c + 2f * a * c * (k - c);
            if (chargeMeter != null) chargeMeter.SetFill(chargeT);
            return;
        }
        if (charging && justReleased)
        {
            float power = ClampPowerToWater(aimDir, chargeT);
            EndCharge();
            fm.Cast(power, aimDir);
        }
    }

    /// <summary>Clamp the charged power so the bobber lands ON water: scan the cast ray inward from the
    /// desired distance and return the farthest point that sits on the water collider. Keeps casts from
    /// overshooting onto land — the bobber just goes as far as the water allows.</summary>
    private float ClampPowerToWater(Vector2 dir, float desiredPower01)
    {
        if (hitCollider == null || maxCastRange <= 0f) return desiredPower01;
        Vector2 origin = (Vector2)CastOrigin;
        float desiredDist = Mathf.Clamp01(desiredPower01) * maxCastRange;
        const float step = 0.2f;
        for (float d = desiredDist; d >= 0f; d -= step)
        {
            if (hitCollider.OverlapPoint(origin + dir * d)) return d / maxCastRange;
        }
        return 0f; // no water along the ray (shouldn't happen — you aim by pressing on water)
    }

    // ── Waiting/Bite: tap ANYWHERE in the lake view to reel a step toward shore ──
    // You reel the line back toward the pole, so a tap on the grass (near shore) reads more
    // naturally than having to tap the water out past the bobber. A quick tap-release reels one
    // step; press-and-HOLD switches to a slow continuous reel that carries the bobber all the
    // way to shore in one smooth motion.
    private void HandleReelGesture(Vector2 screenPos, bool justPressed, bool justReleased, bool held)
    {
        var fm = FishingManager.Instance;
        if (fm == null || !CanInteract()) return;

        if (justPressed)
        {
            reelPressActive = true;
            reelHolding = false;
            reelPressTime = Time.unscaledTime;
        }

        if (reelPressActive && held && !reelHolding
            && Time.unscaledTime - reelPressTime >= holdReelDelay)
            reelHolding = true;

        if (reelHolding && held)
            fm.ReelHold(Time.unscaledDeltaTime * holdReelStepsPerSecond);

        if (justReleased)
        {
            // A quick tap (never crossed into hold territory) reels one discrete step.
            if (!reelHolding) fm.Reel();
            reelPressActive = false;
            reelHolding = false;
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
        charging = false; chargeElapsed = 0f; chargeT = 0f;
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
