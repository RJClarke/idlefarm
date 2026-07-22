using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Makes a dropped egg/gem claimable by tapping it directly on the farm (not only via the HUD
/// collect button). Attached to the runtime EggDrop/GemDrop objects by <see cref="AnimalVisual"/>.
///
/// Mirrors the Woods tree-tap approach: read the pointer, hit-test against the sprite's (padded)
/// bounds, and route to <see cref="AnimalManager.ClaimPassiveReward"/> — which itself no-ops unless
/// a reward is actually ready, so a stray tap is harmless.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DropClickCollector : MonoBehaviour
{
    [Tooltip("World-space padding around the drop's sprite so the small target is forgiving to tap.")]
    [SerializeField] private float tapPadding = 0.3f;

    private SpriteRenderer sr;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (sr == null) return;
        if (!TryReadTap(out Vector2 screenPos)) return;
        // Don't claim through an open menu/overlay covering the tap point.
        if (UITapBlocker.PointerOverUI(screenPos)) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

        Bounds b = sr.bounds;
        b.Expand(new Vector3(tapPadding * 2f, tapPadding * 2f, 0f)); // Expand takes total size, not per-side
        if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) return;

        if (AnimalManager.Instance != null) AnimalManager.Instance.ClaimPassiveReward();
    }

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
}
