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
    [Tooltip("How many degrees the tree jerks when hit. Rotated about its base (like the wind sway) so " +
             "it recoils from the ground instead of sliding the whole sprite sideways.")]
    [SerializeField] private float shakeDegrees = 5f;
    [Tooltip("Stable id for save/load. Leave empty to use the scene hierarchy path.")]
    [SerializeField] private string treeId;

    private SpriteRenderer sr;
    private int hitsSoFar;
    private long plantedUtcTicks;
    private int shownStage = -1;

    /// <summary>Stable identity used to persist this tree's growth. Defaults to the hierarchy path.</summary>
    public string TreeId => !string.IsNullOrEmpty(treeId) ? treeId : HierarchyPath(transform);

    /// <summary>World-space AABB of the current sprite — the tree's visible footprint, used by
    /// WoodcuttingManager to hit-test taps against the actual tree instead of a fat radius.</summary>
    public Bounds WorldBounds => sr != null ? sr.bounds : new Bounds(transform.position, Vector3.zero);

    /// <summary>True if a world point falls on this tree's sprite, expanded by a small padding for
    /// touch forgiveness. Saplings have tiny sprites, so their tap zone shrinks with them.</summary>
    public bool ContainsWorldPoint(Vector2 world, float padding)
    {
        if (sr == null) return false;
        Bounds b = sr.bounds;
        b.Expand(new Vector3(padding * 2f, padding * 2f, 0f)); // Expand takes total size, not per-side
        return world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y;
    }

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
            // Too hard for this axe: point the player at the fix, no chop progress. Levels are shown
            // 1-based to the player (bought axe = Lv 1), so a requiredAxeLevel of N reads as "level N+1".
            if (wm != null) wm.ShowHint(transform.position, $"Upgrade axe to level {data.requiredAxeLevel + 1}");
            return;
        }

        // Wood storage is full: refuse the swing entirely (no hit, no tree damage) rather than
        // waste chops against a tree for nothing once there's nowhere for the wood to go.
        if (CurrencyManager.Instance != null && WoodcuttingMath.IsAtCap(CurrencyManager.Instance.Wood, CurrencyManager.Instance.MaxWood))
        {
            if (wm != null) wm.ShowHint(transform.position, "Wood storage full!");
            return;
        }

        int stage = WoodcuttingMath.StageIndex(GrowthFraction(), data.stageCount);
        int yield = WoodcuttingMath.StageYield(data.woodYield, stage, data.stageCount);
        if (yield <= 0)
        {
            // Sapling — worth nothing yet; shake for feedback but make no chop progress. Seeing zero
            // wood come off it is the point: it teaches that saplings aren't ready.
            Shake();
            return;
        }

        int fullHits = WoodcuttingMath.EffectiveHitsToFell(data.baseHitsToFell, axe, reduction);
        int needed = WoodcuttingMath.StageHits(fullHits, stage, data.stageCount);

        hitsSoFar++;
        Shake();

        // Drip the yield out per swing — small teasers early, the bulk on the felling blow (10 wood
        // over 5 hits → 1,1,1,1,6) instead of one lump, so the wood you're getting, and that an
        // immature tree gives less, is visible on every hit while the final blow stays the big reward.
        int swingWood = WoodcuttingMath.SwingWood(yield, hitsSoFar, needed);
        if (swingWood > 0 && FarmSkillsManager.Instance != null)
            swingWood = Mathf.RoundToInt(swingWood * (1f + FarmSkillsManager.Instance.GetBonus(FarmSkillTrack.Forestry)));
        if (swingWood > 0)
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddWood(swingWood);
            if (wm != null) wm.NotifyWoodGathered(swingWood); // "collect X wood" quest
            Bounds b = WorldBounds;
            FloatingTextManager.ShowWood(swingWood, new Vector3(b.center.x, b.max.y, 0f));
        }

        if (hitsSoFar >= needed) Fell();
    }

    // A quick, aggressive recoil rotated about the tree's base (bottom-pivot sprite), so the trunk
    // pivots from the ground like the wind sway rather than the whole sprite sliding sideways. Cancel
    // any in-flight shake first so rapid taps re-fire crisply and always settle back to upright.
    private void Shake()
    {
        LeanTween.cancel(gameObject);
        transform.localEulerAngles = Vector3.zero;
        LeanTween.rotateZ(gameObject, shakeDegrees, 0.04f)
            .setLoopPingPong(1)
            .setOnComplete(() => { if (this != null) transform.localEulerAngles = Vector3.zero; });
    }

    private void Fell()
    {
        // Wood was already credited swing-by-swing in HandleTap; felling just resets the tree.
        if (WoodcuttingManager.Instance != null) WoodcuttingManager.Instance.NotifyTreeFelled(); // "chop X trees" quest
        // Cutting restarts growth from a fresh sapling, cooldown from now.
        hitsSoFar = 0;
        shownStage = -1;
        transform.localEulerAngles = Vector3.zero; // clear any residual shake tilt
        plantedUtcTicks = System.DateTime.UtcNow.Ticks;
        ApplyGrowthVisual();
    }
}
