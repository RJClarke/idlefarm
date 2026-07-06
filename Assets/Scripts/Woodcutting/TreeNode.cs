using UnityEngine;

/// <summary>
/// A choppable tree in the Woods. Grows continuously from a sapling to full grown over
/// WoodTreeData.growSeconds (UtcNow-based, so it keeps growing offline), stepping through
/// stages that scale it up. Tap to chop at any stage: cutting early yields only a portion of
/// the wood and restarts growth from a fresh sapling.
///
/// Tap routing lives in <see cref="WoodcuttingManager"/>: it reads the pointer once, finds the
/// nearest tree within a forgiving radius, and calls <see cref="HandleTap"/> on it. Centralizing
/// this (rather than each tree exact-hit-testing its own small collider) makes tapping fair on a
/// small target and avoids two trees both claiming one tap.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeNode : MonoBehaviour
{
    [SerializeField] private WoodTreeData data;
    [SerializeField] private float shakePixels = 3f;
    [Tooltip("Stable id for save/load. Leave empty to use the scene hierarchy path.")]
    [SerializeField] private string treeId;

    private SpriteRenderer sr;
    private int hitsSoFar;
    private long plantedUtcTicks;
    private int shownStage = -1;

    /// <summary>Stable identity used to persist this tree's growth. Defaults to the hierarchy path.</summary>
    public string TreeId => !string.IsNullOrEmpty(treeId) ? treeId : HierarchyPath(transform);

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        // Default: start at a random point in the growth cycle so the woods looks varied, not
        // uniform. Overwritten by ApplySaveState if this tree has persisted growth.
        double offset = data != null ? Random.value * data.growSeconds : 0.0;
        plantedUtcTicks = System.DateTime.UtcNow.Ticks - (long)(offset * System.TimeSpan.TicksPerSecond);
    }

    private void Start()
    {
        // Register after Awake so WoodcuttingManager exists; it pushes any saved growth back to us.
        if (WoodcuttingManager.Instance != null)
            WoodcuttingManager.Instance.RegisterTree(this);
    }

    private void OnDestroy()
    {
        if (WoodcuttingManager.Instance != null)
            WoodcuttingManager.Instance.UnregisterTree(this);
    }

    /// <summary>Snapshot growth for saving.</summary>
    public TreeSaveState CaptureSaveState() => new TreeSaveState
    {
        treeId = TreeId,
        plantedUtcTicks = plantedUtcTicks,
        hitsSoFar = hitsSoFar,
    };

    /// <summary>Restore saved growth. plantedUtcTicks is absolute, so time away is applied for free.</summary>
    public void ApplySaveState(TreeSaveState state)
    {
        if (state == null) return;
        plantedUtcTicks = state.plantedUtcTicks;
        hitsSoFar = state.hitsSoFar;
        shownStage = -1; // force the visual to re-evaluate against restored growth
        ApplyGrowthVisual();
    }

    private static string HierarchyPath(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent)
            sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private void Update()
    {
        if (data == null) return;
        ApplyGrowthVisual();
    }

    private float GrowthFraction()
    {
        long now = System.DateTime.UtcNow.Ticks;
        // Forward-only: a backward clock (planted timestamp now in the future) re-anchors to now so
        // the tree resumes growing from sapling instead of freezing until real time catches up.
        if (plantedUtcTicks > now) plantedUtcTicks = now;
        double elapsed = (now - plantedUtcTicks) / (double)System.TimeSpan.TicksPerSecond;
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

    /// <summary>Handle a tap that WoodcuttingManager routed to this tree. Applies the no-axe gate,
    /// then a chop at the current growth stage.</summary>
    public void HandleTap()
    {
        var wm = WoodcuttingManager.Instance;

        // No axe yet: every tree (softwood included) is off-limits. Point the player at the Carpenter
        // with a world-space hint anchored to the tapped tree, and don't make any chop progress.
        if (wm != null && !wm.HasAxe)
        {
            wm.ShowAxeHint(transform.position);
            return;
        }

        int axe = wm != null ? wm.AxeLevel : 0;
        int reduction = wm != null ? wm.HitsReductionPerLevel : 0;

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
}
