using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One of 4 world-placed log piles near the Wood Rack, each representing an equal share of the
/// wood cap (spec: wood-pile-visuals-design §3). Fills in index order — pile 1 shows nothing
/// until pile 0 is full — and swaps through 4 sprites by fill fraction. Clickable exactly like
/// the Wood Rack itself (opens the same sell popup); interaction/press-feedback pattern cloned
/// from WoodRack.cs. A pile with zero fill is hidden and not interactable.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class LogStackVisual : MonoBehaviour
{
    [Tooltip("0-3. Pile 0 fills first; capacity per pile = CurrencyManager.MaxWood / 4.")]
    [SerializeField] private int pileIndex;

    [Header("Sprites by fill fraction")]
    [SerializeField] private Sprite lowSprite;    // 0 < f < 0.30
    [SerializeField] private Sprite midSprite;    // 0.30 <= f < 0.60
    [SerializeField] private Sprite highSprite;   // 0.60 <= f < 1.0
    [SerializeField] private Sprite fullSprite;   // f >= 1.0

    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    private const int PileCount = 4;

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;
    private bool interactable;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Start()
    {
        Refresh();
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnWoodChanged += OnWoodChanged;
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnWoodChanged -= OnWoodChanged;
    }

    private void OnWoodChanged(int _) => Refresh();

    private void Refresh()
    {
        var cm = CurrencyManager.Instance;
        int capacityPerPile = cm != null ? cm.MaxWood / PileCount : 0;
        float fraction = cm != null ? WoodcuttingMath.PileFillFraction(cm.Wood, pileIndex, capacityPerPile) : 0f;

        interactable = fraction > 0f;
        spriteRenderer.enabled = interactable;
        ownCollider.enabled = interactable;
        if (!interactable) return;

        spriteRenderer.sprite = fraction >= 1f ? fullSprite
            : fraction >= 0.60f ? highSprite
            : fraction >= 0.30f ? midSprite
            : lowSprite;
    }

    private void Update()
    {
        if (!interactable) return;
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
        return pan == null || !pan.IsPanning;
    }

    private void HandleClick()
    {
        if (WoodRackPopupUITK.Instance != null) WoodRackPopupUITK.Instance.Open();
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
