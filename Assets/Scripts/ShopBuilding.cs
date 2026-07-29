using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class ShopBuilding : MonoBehaviour
{
    public enum ShopType { Plants, Equipment, Carpenter, Greenhouse, TownBoard }

    [Header("Identity")]
    [SerializeField] private ShopType shopType = ShopType.Plants;
    [SerializeField] private string displayName = "Shop";

    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.92f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    [Header("Behavior")]
    [Tooltip("Only allow clicks when camera is settled at this location. Set to Farm to allow anywhere.")]
    [SerializeField] private CameraPanController.Location requiredLocation = CameraPanController.Location.Market;

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;

    public ShopType Type => shopType;
    public string DisplayName => displayName;

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

        // A menu/overlay over the pointer swallows the tap so it can't fall through to the world.
        if (UITapBlocker.PointerOverUI(screenPos)) { CancelPress(); return; }

        // Press start: pointer just went down on this collider.
        if (justPressed && !isPressed && CanInteract() && PointerHitsSelf(screenPos))
        {
            isPressed = true;
            spriteRenderer.color = pressTint * baseColor;
            DoTween(baseScale * pressScale, pressDuration);
            return;
        }

        // While held: if pointer drifts off the collider, cancel the press visually.
        if (held && isPressed && !PointerHitsSelf(screenPos))
        {
            CancelPress();
            return;
        }

        // Release: if pointer came up and we were pressing this, fire click only if still over collider.
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

        // Prefer active touch on touchscreen; fall back to mouse.
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            justReleased = false;
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
        Collider2D hit = Physics2D.OverlapPoint(world);
        return hit == ownCollider;
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
        // Every shop stays open during a run — this used to carve out the Greenhouse (Research) as
        // the sole exception, but nothing sold here retroactively edits an active run: crop unlocks
        // only matter at seed selection, and EquipmentManager defers its zone rebuild while a run is
        // active. Location and pan state are the only gates.
        CameraPanController pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (pan == null) return true;
        return !pan.IsPanning && pan.CurrentLocation == requiredLocation;
    }

    private void HandleClick()
    {
        switch (shopType)
        {
            case ShopType.Plants:
                if (ShopPopupUITK.TryOpen(ShopPopupUITK.Section.Plants)) return;
                break;
            case ShopType.Equipment:
                if (ShopPopupUITK.TryOpen(ShopPopupUITK.Section.Equipment)) return;
                break;
            case ShopType.Greenhouse:
                if (ResearchPopupUITK.Instance != null) { ResearchPopupUITK.Instance.Open(); return; }
                break;
            case ShopType.Carpenter:
                if (CarpenterPopupUITK.Instance != null) { CarpenterPopupUITK.Instance.Open(); return; }
                break;
            case ShopType.TownBoard:
                // The Town Message Board opens the same Town Requests / Reputation popup that tapping
                // the always-on rep bar does.
                if (TownRequestsPopupUITK.Instance != null) { TownRequestsPopupUITK.Instance.Open(); return; }
                break;
        }
        if (MarketPopupUITK.Instance != null)
        {
            MarketPopupUITK.Instance.Open();
            return;
        }
        Debug.Log($"[ShopBuilding] Clicked {displayName} ({shopType}) — no popup wired.");
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
