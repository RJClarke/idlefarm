using UnityEngine;

/// <summary>
/// Drives a sprite's <c>sortingOrder</c> from its world Y so things lower on screen draw in
/// front (2.5D depth sorting). Entities land in the "entity band" (~1000–3000) within the
/// single Default sorting layer, above ground (&lt;1000) and below weather/VFX (&gt;5000).
///
/// Attach to any world entity (helper, animal, threat, tree, building, crop). For multi-sprite
/// entities it drives every child SpriteRenderer while preserving their original relative
/// ordering (e.g. a dropped egg stays behind/in front of the body as authored).
/// </summary>
[DisallowMultipleComponent]
public class YSort : MonoBehaviour
{
    public const int ENTITY_BASE = 2000;
    public const int PRECISION = 10;

    [Tooltip("Shifts the sort anchor toward the entity's feet/base. Negative moves it down. " +
             "Ignored when Auto Foot is on.")]
    [SerializeField] private float footOffset = 0f;

    [Tooltip("Auto-anchor to the sprite's bottom edge (from renderer bounds) so pivot placement " +
             "doesn't matter. Recommended for most props/entities.")]
    [SerializeField] private bool autoFoot = true;

    [Tooltip("Static objects (trees/buildings) compute once in Start; movers recompute every frame.")]
    [SerializeField] private bool isStatic = false;

    private SpriteRenderer[] renderers;
    private int[] relativeOffsets;

    private void Awake() => CacheRenderers();

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        relativeOffsets = new int[renderers.Length];
        int maxOrder = int.MinValue;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) maxOrder = Mathf.Max(maxOrder, renderers[i].sortingOrder);
        if (maxOrder == int.MinValue) maxOrder = 0;
        for (int i = 0; i < renderers.Length; i++)
            relativeOffsets[i] = (renderers[i] != null ? renderers[i].sortingOrder : 0) - maxOrder; // <= 0
    }

    private void Start() => Apply();

    private void LateUpdate()
    {
        if (!isStatic) Apply();
    }

    private void Apply()
    {
        if (renderers == null || renderers.Length == 0) return;
        float anchorY = transform.position.y + (autoFoot ? AutoFootOffset() : footOffset);
        int baseOrder = ENTITY_BASE - Mathf.RoundToInt(anchorY * PRECISION);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sortingOrder = baseOrder + relativeOffsets[i];
    }

    /// <summary>Distance from the transform origin down to the sprites' bottom edge (≤ 0).</summary>
    private float AutoFootOffset()
    {
        float minY = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].sprite != null)
                minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        return minY == float.MaxValue ? 0f : (minY - transform.position.y);
    }

    /// <summary>Re-scan child renderers (call after adding/removing a child sprite at runtime).</summary>
    public void RefreshRenderers()
    {
        CacheRenderers();
        Apply();
    }

    /// <summary>Convenience: add (or fetch) a YSort on <paramref name="go"/> with the given anchor/static flags.</summary>
    public static YSort Ensure(GameObject go, bool isStatic = false, bool autoFoot = true)
    {
        var ys = go.GetComponent<YSort>();
        if (ys == null) ys = go.AddComponent<YSort>();
        ys.autoFoot = autoFoot;
        ys.isStatic = isStatic;
        return ys;
    }
}
