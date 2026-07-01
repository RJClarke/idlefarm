using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A choppable tree in the Woods. Tap to chop; after the effective hit count it falls,
/// awards Wood, and shows a stump that regrows after WoodTreeData.regrowSeconds
/// (UtcNow-based so offline time counts). Only interactable when the camera is at Woods.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class TreeNode : MonoBehaviour
{
    [SerializeField] private WoodTreeData data;
    [SerializeField] private float shakePixels = 3f;

    private SpriteRenderer sr;
    private Collider2D ownCollider;
    private int hitsSoFar;
    private bool isStump;
    private long stumpSinceUtcTicks;
    private Vector3 basePos;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        basePos = transform.localPosition;
        ShowStanding();
    }

    private void Update()
    {
        if (isStump) { TryRegrow(); return; }
        if (data == null || !AtWoods()) return;
        if (!TryReadTap(out Vector2 screenPos)) return;
        if (!PointerHitsSelf(screenPos)) return;
        Chop();
    }

    private void Chop()
    {
        int axe = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.AxeLevel : 0;
        int reduction = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.HitsReductionPerLevel : 0;

        if (!WoodcuttingMath.CanFell(data.requiredAxeLevel, axe))
        {
            // Locked tree: small feedback, no progress. (Toast/hint optional.)
            return;
        }

        hitsSoFar++;
        LeanTween.moveLocalX(gameObject, basePos.x + shakePixels / 32f, 0.04f).setLoopPingPong(1);

        int needed = WoodcuttingMath.EffectiveHitsToFell(data.baseHitsToFell, axe, reduction);
        if (hitsSoFar >= needed) Fell();
    }

    private void Fell()
    {
        if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddWood(data.woodYield);
        // TODO(art): floating +N text via existing floating-number system.
        hitsSoFar = 0;
        isStump = true;
        stumpSinceUtcTicks = System.DateTime.UtcNow.Ticks;
        ShowStump();
    }

    private void TryRegrow()
    {
        double elapsed = (System.DateTime.UtcNow.Ticks - stumpSinceUtcTicks) / (double)System.TimeSpan.TicksPerSecond;
        if (WoodcuttingMath.IsRegrown(elapsed, data.regrowSeconds)) { isStump = false; ShowStanding(); }
    }

    private void ShowStanding()
    {
        if (data != null && data.standingSprite != null) sr.sprite = data.standingSprite;
    }

    private void ShowStump()
    {
        if (data != null && data.stumpSprite != null) sr.sprite = data.stumpSprite;
    }

    private static bool AtWoods()
    {
        var pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        return pan != null && !pan.IsPanning && pan.CurrentLocation == CameraPanController.Location.Woods;
    }

    // --- pointer helpers (mirrors ShopBuilding) ---
    private static bool TryReadTap(out Vector2 screenPos)
    {
        screenPos = default;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
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
}
