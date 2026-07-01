using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clickable wood-rack prop in the Woods. Tapping it opens the sell panel
/// (WoodRackPopupUITK). Interactable only when the camera is settled at the Woods.
/// Press/click handling mirrors ShopBuilding.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class WoodRack : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.92f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
            return;

        if (justPressed && !isPressed && CanInteract() && PointerHitsSelf(screenPos))
        {
            isPressed = true;
            spriteRenderer.color = pressTint * baseColor;
            DoTween(baseScale * pressScale, pressDuration);
            return;
        }

        if (held && isPressed && !PointerHitsSelf(screenPos))
        {
            CancelPress();
            return;
        }

        if (justReleased && isPressed)
        {
            bool overSelf = PointerHitsSelf(screenPos);
            CancelPress();
            if (overSelf && CanInteract()) HandleClick();
        }
    }

    private static bool TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held)
    {
        screenPos = default;
        justPressed = false;
        justReleased = false;
        held = false;

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
        return !pan.IsPanning && pan.CurrentLocation == CameraPanController.Location.Woods;
    }

    private void HandleClick()
    {
        if (WoodRackPopupUITK.Instance != null) WoodRackPopupUITK.Instance.Open();
        else Debug.Log("[WoodRack] Clicked — no WoodRackPopupUITK in scene.");
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
