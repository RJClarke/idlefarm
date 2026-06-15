using UnityEngine;

/// <summary>
/// At startup, attaches a static auto-foot <see cref="YSort"/> to every SpriteRenderer found
/// under the assigned roots. Use for scene-placed static props (trees, decorations, buildings)
/// so they depth-sort with the moving entities without hand-adding a component to each — and
/// so future props added under those roots are covered automatically.
/// </summary>
public class YSortBootstrap : MonoBehaviour
{
    [Tooltip("Root object names whose SpriteRenderer descendants get a static YSort. Resolved by name at startup.")]
    [SerializeField] private string[] rootNames = { "Environment", "MarketBuildings", "GreenhouseBuilding" };

    private void Start()
    {
        if (rootNames == null) return;
        foreach (string name in rootNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            GameObject root = GameObject.Find(name);
            if (root == null) continue;
            var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
                if (sr != null) YSort.Ensure(sr.gameObject, isStatic: true);
        }
    }
}
