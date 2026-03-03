using UnityEngine;
using System.Collections;

/// <summary>
/// Abstract base class for all animal/pest threats (Deer, Crow).
///
/// Shared lifecycle:
///   1. Initialize() called by ThreatWaveManager — sets data + hunger
///   2. EnterFarm() coroutine — subclass moves threat from off-screen to starting position
///   3. Eat loop — find target → bite × N → move to next → repeat until hunger = 0 or no targets
///   4. ExitFarm() coroutine — subclass moves threat off-screen
///   5. Destroy self
///
/// Hunger mechanic:
///   Damage dealt = baseDamage × stageMultiplier
///   Hunger filled = same value (so attacking a Sapling fills hunger faster AND hurts the plant more)
///
/// Weather threats are a separate system — this base class is animals only.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public abstract class AnimalThreat : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // Fields set by Initialize()
    // ─────────────────────────────────────────────────────────────────────

    protected AnimalThreatData data;
    protected float hungerRemaining;
    protected int assignedZoneId;

    protected SpriteRenderer spriteRenderer;
    protected Animator animator;

    // ─────────────────────────────────────────────────────────────────────
    // Public State
    // ─────────────────────────────────────────────────────────────────────

    public bool IsDone { get; private set; } = false;

    public AnimalThreatType ThreatType => data != null ? data.threatType : AnimalThreatType.Deer;

    // ─────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // Always use unscaled time so animations don't speed up during game speed-up mode
        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ThreatWaveManager immediately after spawning.
    /// </summary>
    public virtual void Initialize(AnimalThreatData threatData, float hunger, int zoneId)
    {
        data            = threatData;
        hungerRemaining = hunger;
        assignedZoneId  = zoneId;

        // Only build placeholder if no prefabs are assigned in the data
        if (threatData.prefabs == null || threatData.prefabs.Length == 0)
            BuildPlaceholderSprite();

        StartCoroutine(ThreatLifecycle());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Animation Helper
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the AnimState integer on the Animator.
    /// 0 = Walk, 1 = Run, 2 = Eat, 3 = Idle
    /// </summary>
    protected void SetAnimation(int state)
    {
        if (animator != null)
            animator.SetInteger("AnimState", state);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Abstract — subclasses define approach/exit and target selection
    // ─────────────────────────────────────────────────────────────────────

    protected abstract IEnumerator EnterFarm();
    protected abstract IEnumerator ExitFarm();
    protected abstract Plant FindFirstTarget();
    protected abstract Plant FindNextTarget(Vector3 currentPosition);

    // ─────────────────────────────────────────────────────────────────────
    // Animation Hooks — override in subclasses for per-animal animations
    // ─────────────────────────────────────────────────────────────────────

    protected virtual void OnEnteringFarm() { }
    protected virtual void OnMovingToPlant() { }
    protected virtual void OnSearchingForPlant() { }
    protected virtual void OnEatingPlant() { }
    protected virtual void OnExitingFarm() { }

    // ─────────────────────────────────────────────────────────────────────
    // Core Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator ThreatLifecycle()
    {
        // Phase 1 — Approach the farm
        OnEnteringFarm();
        yield return StartCoroutine(EnterFarm());

        // Phase 2 — Eat until full or no valid targets remain
        Plant currentTarget = FindFirstTarget();

        while (hungerRemaining > 0f && currentTarget != null)
        {
            Vector3 targetPos = currentTarget.transform.position;

            // Walk to the plant
            OnMovingToPlant();
            yield return StartCoroutine(MoveTo(targetPos, data.moveSpeed));

            // Re-validate: plant may have been harvested while we were walking
            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                OnSearchingForPlant();
                currentTarget = FindNextTarget(transform.position);
                continue;
            }

            // Eat it
            OnEatingPlant();
            yield return StartCoroutine(EatPlant(currentTarget));

            // Find next if still hungry
            if (hungerRemaining > 0f)
            {
                OnSearchingForPlant();
                currentTarget = FindNextTarget(transform.position);
            }
        }

        // Phase 3 — Leave the farm
        OnExitingFarm();
        yield return StartCoroutine(ExitFarm());

        FinishThreat();
    }

    /// <summary>
    /// Bite a single plant N times, waiting biteInterval between each bite.
    /// Stops early if: plant is destroyed, hunger reaches 0, or bite count is reached.
    /// </summary>
    private IEnumerator EatPlant(Plant plant)
    {
        if (plant == null) yield break;

        int biteCount = Random.Range(data.minBitesPerPlant, data.maxBitesPerPlant + 1);

        for (int i = 0; i < biteCount; i++)
        {
            yield return new WaitForSeconds(data.biteInterval);

            if (plant == null || !plant.gameObject.activeInHierarchy) yield break;
            if (hungerRemaining <= 0f) yield break;

            float damage = data.GetBiteDamage(plant.CurrentStage);
            if (damage <= 0f) yield break;

            float hpBefore        = plant.CurrentHP;
            plant.TakeDamage(damage);

            float hpAfter         = plant != null ? plant.CurrentHP : 0f;
            float hungerSatisfied = hpBefore - hpAfter;
            hungerRemaining       = Mathf.Max(0f, hungerRemaining - hungerSatisfied);

            Debug.Log($"[{data.threatName}] Bite {i + 1}/{biteCount}: " +
                      $"-{damage:F0} HP on {plant.CropData?.cropName} ({plant.CurrentStage})  " +
                      $"Hunger left: {hungerRemaining:F0}");

            if (plant == null || !plant.gameObject.activeInHierarchy) yield break;
            if (hungerRemaining <= 0f) yield break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Shared Movement — flips sprite to face direction of travel
    // ─────────────────────────────────────────────────────────────────────

    protected IEnumerator MoveTo(Vector3 worldPos, float speed)
    {
        while (Vector3.Distance(transform.position, worldPos) > 0.02f)
        {
            // Flip sprite to face the direction we're moving
            if (spriteRenderer != null)
            {
                float dirX = worldPos.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.01f)
                    spriteRenderer.flipX = dirX < 0f;
            }

            transform.position = Vector3.MoveTowards(
                transform.position, worldPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = worldPos;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Camera Edge Helpers
    // ─────────────────────────────────────────────────────────────────────

    protected float ScreenTopY(float padding = 1.5f) =>
        Camera.main.transform.position.y + Camera.main.orthographicSize + padding;

    protected float ScreenBottomY(float padding = 1.5f) =>
        Camera.main.transform.position.y - Camera.main.orthographicSize - padding;

    protected float ScreenLeftX(float padding = 1.5f)
    {
        float hw = Camera.main.orthographicSize * Camera.main.aspect;
        return Camera.main.transform.position.x - hw - padding;
    }

    protected float ScreenRightX(float padding = 1.5f)
    {
        float hw = Camera.main.orthographicSize * Camera.main.aspect;
        return Camera.main.transform.position.x + hw + padding;
    }

    protected Vector3 GetScreenEdgePoint(float angleDegrees, float padding = 1.5f)
    {
        float rad      = angleDegrees * Mathf.Deg2Rad;
        float dirX     = Mathf.Cos(rad);
        float dirY     = Mathf.Sin(rad);
        float hw       = Camera.main.orthographicSize * Camera.main.aspect + padding;
        float hh       = Camera.main.orthographicSize + padding;
        Vector3 camPos = Camera.main.transform.position;

        float scaleX = dirX != 0f ? Mathf.Abs(hw / dirX) : float.MaxValue;
        float scaleY = dirY != 0f ? Mathf.Abs(hh / dirY) : float.MaxValue;
        float scale  = Mathf.Min(scaleX, scaleY);

        return new Vector3(camPos.x + dirX * scale, camPos.y + dirY * scale, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Plant Query Helpers
    // ─────────────────────────────────────────────────────────────────────

    protected Plant FindBestPlantInRadius(Vector3 origin, float radius, Plant exclude = null)
    {
        if (FarmGrid.Instance == null) return null;

        var tiles       = FarmGrid.Instance.GetOccupiedTilesInZone(assignedZoneId);
        Plant best      = null;
        float bestScore = -1f;

        foreach (SoilTile tile in tiles)
        {
            if (tile.CurrentPlant == null) continue;

            Plant plant = tile.CurrentPlant.GetComponent<Plant>();
            if (plant == null || plant == exclude) continue;

            if (Vector3.Distance(origin, tile.transform.position) > radius) continue;

            float multiplier = data.GetStageMultiplier(plant.CurrentStage);
            if (multiplier <= 0f) continue;

            float score = multiplier + Random.Range(0f, 0.2f);
            if (score > bestScore)
            {
                bestScore = score;
                best      = plant;
            }
        }

        return best;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Finish
    // ─────────────────────────────────────────────────────────────────────

    protected void FinishThreat()
    {
        IsDone = true;
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Placeholder Visual (used only when no prefab is assigned)
    // ─────────────────────────────────────────────────────────────────────

    private void BuildPlaceholderSprite()
    {
        const int size   = 16;
        Texture2D tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[]   pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = data.placeholderColor;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        float ppu = size / data.visualSize;
        spriteRenderer.sprite       = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, ppu);
        spriteRenderer.sortingOrder = 15;
    }
}