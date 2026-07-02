using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A choppable tree in the Woods. Grows continuously from a sapling to full grown over
/// WoodTreeData.growSeconds (UtcNow-based, so it keeps growing offline), stepping through
/// stages that scale it up. Tap to chop at any stage: cutting early yields only a portion of
/// the wood and restarts growth from a fresh sapling. Only interactable at the Woods.
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
    private long plantedUtcTicks;
    private int shownStage = -1;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        // Start at a random point in the growth cycle so the woods looks varied, not uniform.
        double offset = data != null ? Random.value * data.growSeconds : 0.0;
        plantedUtcTicks = System.DateTime.UtcNow.Ticks - (long)(offset * System.TimeSpan.TicksPerSecond);
    }

    private void Update()
    {
        if (data == null) return;
        ApplyGrowthVisual();

        if (!AtWoods()) return;
        if (!TryReadTap(out Vector2 screenPos)) return;
        if (!PointerHitsSelf(screenPos)) return;
        Chop();
    }

    private float GrowthFraction()
    {
        double elapsed = (System.DateTime.UtcNow.Ticks - plantedUtcTicks) / (double)System.TimeSpan.TicksPerSecond;
        return WoodcuttingMath.RegrowFraction(elapsed, data.growSeconds);
    }

    private void ApplyGrowthVisual()
    {
        float g = GrowthFraction();
        int stage = WoodcuttingMath.StageIndex(g, data.stageCount);

        // With per-stage art, the sprites themselves convey growth — hold a constant display
        // scale. Without art, fall back to scaling a single sprite from sapling to full.
        bool hasStageArt = data.stageSprites != null && data.stageSprites.Length > 0;
        float scale = hasStageArt
            ? data.fullScale
            : Mathf.Lerp(data.saplingScale, data.fullScale,
                         data.stageCount > 1 ? stage / (float)(data.stageCount - 1) : 1f);
        transform.localScale = new Vector3(scale, scale, 1f);

        if (stage != shownStage)
        {
            shownStage = stage;
            Sprite s = data.SpriteForStage(stage);
            if (s != null) sr.sprite = s;
        }
    }

    private void Chop()
    {
        int axe = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.AxeLevel : 0;
        int reduction = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.HitsReductionPerLevel : 0;

        if (!WoodcuttingMath.CanFell(data.requiredAxeLevel, axe))
        {
            // Locked tree: no progress. (Toast/hint optional.)
            return;
        }

        int stage = WoodcuttingMath.StageIndex(GrowthFraction(), data.stageCount);
        int fullHits = WoodcuttingMath.EffectiveHitsToFell(data.baseHitsToFell, axe, reduction);
        int needed = WoodcuttingMath.StageHits(fullHits, stage, data.stageCount);

        hitsSoFar++;
        LeanTween.moveLocalX(gameObject, transform.localPosition.x + shakePixels / 32f, 0.04f).setLoopPingPong(1);

        if (hitsSoFar >= needed) Fell(stage);
    }

    private void Fell(int stage)
    {
        int yield = WoodcuttingMath.StageYield(data.woodYield, stage, data.stageCount);
        if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddWood(yield);
        // TODO(art): floating +N text via existing floating-number system.

        // Cutting restarts growth from a fresh sapling, cooldown from now.
        hitsSoFar = 0;
        shownStage = -1;
        plantedUtcTicks = System.DateTime.UtcNow.Ticks;
        ApplyGrowthVisual();
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
