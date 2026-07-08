using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Lake's clickable water (spec §3a). Tapping casts the line, or collects a fish once one is
/// biting, or shows the "buy a pole first" hint. A bite indicator (a small fish icon speech bubble)
/// hovers over the water while a fish is on the line. Interactable only when the camera is settled
/// at the Lake. Pointer handling mirrors WoodRack/CanneryBuilding.
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

    [Header("Bite Indicator")]
    [Tooltip("Local offset from the lake origin where the bite bubble + cast feedback appear.")]
    [SerializeField] private Vector3 bobberOffset = new Vector3(0f, 1.5f, 0f);

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;
    private WorldHintPopup biteIndicator;
    private bool biteShown;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        SyncBiteIndicator();

        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
            return;
        if (UITapBlocker.PointerOverUI(screenPos)) { CancelPress(); return; }

        if (justPressed && !isPressed && CanInteract() && PointerHitsSelf(screenPos))
        {
            isPressed = true;
            spriteRenderer.color = pressTint * baseColor;
            DoTween(baseScale * pressScale, pressDuration);
            return;
        }
        if (held && isPressed && !PointerHitsSelf(screenPos)) { CancelPress(); return; }
        if (justReleased && isPressed)
        {
            bool overSelf = PointerHitsSelf(screenPos);
            CancelPress();
            if (overSelf && CanInteract()) HandleClick();
        }
    }

    private void SyncBiteIndicator()
    {
        var fm = FishingManager.Instance;
        bool biting = fm != null && fm.HasBite;
        if (biting && !biteShown)
        {
            // A small fish icon, no words (spec §3a). WorldHintPopup renders TMP text; an emoji is
            // the placeholder icon until dedicated bubble art lands.
            if (biteIndicator != null) Destroy(biteIndicator.gameObject);
            biteIndicator = WorldHintPopup.Create(transform.position + bobberOffset, "🐟");
            biteShown = true;
        }
        else if (!biting && biteShown)
        {
            if (biteIndicator != null) Destroy(biteIndicator.gameObject);
            biteIndicator = null;
            biteShown = false;
        }
    }

    private void HandleClick()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;

        if (!fm.HasPole) { fm.ShowNoPoleHint(transform.position + bobberOffset); return; }

        if (fm.HasBite)
        {
            int tier = fm.Collect();
            if (tier > 0) WorldHintPopup.Create(transform.position + bobberOffset, $"🐟 {FishTiers.Name(tier)}!");
            return;
        }

        if (fm.State == FishingManager.CastState.Idle)
        {
            if (fm.Cast()) WorldHintPopup.Create(transform.position + bobberOffset, "Cast!");
            return;
        }

        // Waiting: gentle reminder, no state change.
        WorldHintPopup.Create(transform.position + bobberOffset, "Waiting for a bite…");
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

    private bool PointerHitsSelf(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null || ownCollider == null) return false;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return Physics2D.OverlapPoint(world) == ownCollider;
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
