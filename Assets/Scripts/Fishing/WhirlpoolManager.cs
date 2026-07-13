using UnityEngine;

/// <summary>
/// Owns the placeholder whirlpool hotspot (spec 2026-07-12). One at a time: spawns at a random
/// reachable water point (inside the water collider AND within cast range), lives a while, then
/// despawns and waits before the next. Holds 2–4 fish; each hotspot bite consumes one; empty →
/// despawn. Present-only, not persisted — a fast bite already earned lives in the cast's saved
/// bite time. LakeNode queries IsInside each frame and calls ConsumeFish on a hotspot bite.
/// </summary>
public class WhirlpoolManager : MonoBehaviour
{
    [SerializeField] private LakeNode lake;
    [Tooltip("Water outline used to keep spawns on the water (the painted Tilemap_water CompositeCollider2D).")]
    [SerializeField] private Collider2D waterCollider;
    [SerializeField] private SpriteRenderer circle;         // whirlpool.png, toggled on spawn

    [Header("Tuning")]
    [SerializeField] private float radius = 1.1f;
    [Tooltip("How long a whirlpool lingers before it times out (unless you empty it first). ~5 min.")]
    [SerializeField] private Vector2 lifetimeSecondsRange = new Vector2(270f, 330f);
    [Tooltip("Gap before the next whirlpool after one TIMED OUT un-emptied (kept short so the water stays lively).")]
    [SerializeField] private Vector2 gapSecondsRange = new Vector2(15f, 30f);
    [Tooltip("Gap before the next whirlpool after you EMPTY one — the reward throttle (~30 min).")]
    [SerializeField] private float emptyCooldownSeconds = 1800f;
    [Tooltip("While your bobber is inside a whirlpool, re-check this often instead of despawning it.")]
    [SerializeField] private float occupiedRecheckSeconds = 5f;
    [SerializeField] private int minFish = 2;
    [SerializeField] private int maxFish = 4;

    private bool active;
    private Vector2 center;
    private int fishRemaining;
    private float timer;

    public bool IsInside(Vector3 worldPoint)
        => active && FishingMath.PointInCircle(center, radius, worldPoint);

    public void ConsumeFish()
    {
        if (!active) return;
        fishRemaining--;
        Debug.Log($"[Whirlpool] Fish consumed, {fishRemaining} left.");
        if (fishRemaining <= 0) Despawn(emptied: true); // emptying triggers the long cooldown
    }

    // True while a cast line's bobber is sitting inside this whirlpool — used to defer despawn so it
    // never vanishes out from under the player mid-wait.
    private bool Occupied()
    {
        if (!active || lake == null) return false;
        var fm = FishingManager.Instance;
        if (fm == null) return false;
        bool casting = fm.State == FishingManager.CastState.Waiting || fm.State == FishingManager.CastState.Bite;
        return casting && FishingMath.PointInCircle(center, radius, lake.CurrentBobberWorldPos());
    }

    private void Awake()
    {
        if (circle != null) circle.enabled = false;
        timer = Random.Range(gapSecondsRange.x, gapSecondsRange.y);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (active)
        {
            // Don't despawn while the player is fishing it; re-check shortly instead.
            if (Occupied()) { timer = occupiedRecheckSeconds; return; }
            Despawn(emptied: false);
        }
        else Spawn();
    }

    private void Spawn()
    {
        if (lake == null) { timer = gapSecondsRange.x; return; }
        if (!TryPickPoint(out center)) { timer = 2f; return; } // retry shortly
        active = true;
        fishRemaining = Random.Range(minFish, maxFish + 1);
        timer = Random.Range(lifetimeSecondsRange.x, lifetimeSecondsRange.y);
        if (circle != null)
        {
            circle.transform.position = new Vector3(center.x, center.y, circle.transform.position.z);
            circle.transform.localScale = Vector3.one * (radius * 2f); // sprite is unit-ish; tune in scene
            circle.enabled = true;
        }
        Debug.Log($"[Whirlpool] Spawned with {fishRemaining} fish at {center}.");
    }

    private void Despawn(bool emptied)
    {
        active = false;
        if (circle != null) circle.enabled = false;
        // Emptying one gates the next spawn behind the long cooldown; a timeout uses the short gap.
        timer = emptied ? emptyCooldownSeconds : Random.Range(gapSecondsRange.x, gapSecondsRange.y);
        Debug.Log($"[Whirlpool] Despawned (emptied={emptied}); next in {timer:0}s.");
    }

    // Rejection-sample a point within cast range of the origin that lands on the water.
    private bool TryPickPoint(out Vector2 p)
    {
        Vector2 origin = lake.CastOrigin;
        float maxR = lake.MaxCastRange;
        for (int i = 0; i < 24; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value) * maxR;         // uniform in disc
            p = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (waterCollider == null || waterCollider.OverlapPoint(p)) return true;
        }
        p = origin; return false;
    }
}
