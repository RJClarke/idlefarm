using UnityEngine;

/// <summary>
/// Attach to a mover (helper, animal). Periodically rustles any crops it's brushing past — a quick,
/// localized wobble distinct from the ambient wind sway. Cheap: polls the (small) occupied-tile list
/// on a short interval rather than every frame.
/// </summary>
public class CropAgitator : MonoBehaviour
{
    [Tooltip("How close (world units) the crop must be to get rustled.")]
    [SerializeField] private float radius = 0.6f;
    [Tooltip("Seconds between proximity checks.")]
    [SerializeField] private float interval = 0.12f;
    [Tooltip("Rustle strength applied to brushed crops (0..1).")]
    [SerializeField] private float strength = 0.85f;

    private float timer;

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f || FarmGrid.Instance == null) return;
        timer = interval;

        Vector3 p = transform.position;
        float r2 = radius * radius;

        var tiles = FarmGrid.Instance.GetOccupiedTiles();
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile == null || tile.CurrentPlant == null) continue;
            if (((Vector3)tile.transform.position - p).sqrMagnitude > r2) continue;

            var visuals = tile.CurrentPlant.GetComponent<PlantVisuals>();
            if (visuals != null) visuals.Rustle(strength);
        }
    }
}
