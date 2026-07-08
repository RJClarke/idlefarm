using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clickable Cannery building in the world (between Farm and Woods, spec §8). Hidden until
/// built (BuildingState); shows the smoke child while the fire is lit; tap opens the popup.
/// Press/click handling mirrors WoodRack.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class CanneryBuilding : MonoBehaviour
{
    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    [Header("Smoke")]
    [Tooltip("Child object shown while the firebox is lit (chimney smoke, spec §8).")]
    [SerializeField] private GameObject smokeObject;

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

        // Inspector wiring is optional: default to a child named "Smoke".
        if (smokeObject == null)
        {
            Transform smoke = transform.Find("Smoke");
            if (smoke != null) smokeObject = smoke.gameObject;
        }
    }

    private void Start()
    {
        ApplyBuiltVisibility();
        BuildingState.OnBuildingBuilt += OnBuilt;
    }

    private void OnDestroy() => BuildingState.OnBuildingBuilt -= OnBuilt;

    private void OnBuilt(string key)
    {
        if (key == BuildingState.CanneryKey) ApplyBuiltVisibility();
    }

    private void ApplyBuiltVisibility()
    {
        bool built = BuildingState.IsBuilt(BuildingState.CanneryKey);
        spriteRenderer.enabled = built;
        ownCollider.enabled = built;
        if (!built && smokeObject != null) smokeObject.SetActive(false);
    }

    private void Update()
    {
        // Smoke tracks the fire cheaply (SetActive is a no-op when unchanged).
        if (smokeObject != null && CanneryManager.Instance != null)
            smokeObject.SetActive(CanneryManager.Instance.FireLit);

        if (!spriteRenderer.enabled) return;
        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
            return;

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
        if (pan == null) return true;
        return !pan.IsPanning; // reachable from Farm or Woods framing — it sits between them
    }

    private void HandleClick()
    {
        if (CanneryPopupUITK.Instance != null) CanneryPopupUITK.Instance.Open();
        else Debug.Log("[CanneryBuilding] Clicked — no CanneryPopupUITK in scene.");
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
