using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Farm Dog — a farm defender that roams and chases deer during runs.
///
/// Behavior:
///   - AnimalManager activates chase mode when a run starts with the dog equipped
///   - Roams randomly around the farm between active zones
///   - Every 30 seconds, scans for an active deer and chases it off
///   - Can scare 2 deer per minute (30s cooldown between chases)
///   - AnimalManager deactivates chase mode when the run ends
///
/// Animation states (AnimState int on Animator):
///   0-3  = Idle  R/U/L/D
///   4-7  = Walk  R/U/L/D
///   8-11 = Run   R/U/L/D
///  12-15 = Bark  R/U/L/D
///
/// Lifecycle is managed by AnimalManager — this component lives on the animal
/// visual prefab alongside AnimalVisual.
/// </summary>
public class FarmDog : MonoBehaviour
{
    [Header("Roaming")]
    [SerializeField] private float roamSpeed = 1.5f;
    [SerializeField] private float idleDuration = 2f;
    [SerializeField] private float roamRadius = 3f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float chaseCooldown = 30f;
    [SerializeField] private float chaseReachDistance = 0.3f;
    [Range(0.5f, 5f)]
    [SerializeField] private float barkDuration = 1.2f;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private AnimalVisual animalVisual;
    private Coroutine roamCoroutine;
    private float chaseCooldownTimer;
    private bool isChasing;
    private bool isChaseModeActive;

    // 0=R, 1=U, 2=L, 3=D
    private int facingDir = 3;

    private const int IDLE_OFFSET = 0;
    private const int WALK_OFFSET = 4;
    private const int RUN_OFFSET  = 8;
    private const int BARK_OFFSET = 12;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        animalVisual = GetComponent<AnimalVisual>();
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        YSort.Ensure(gameObject);
    }

    private void SetAnim(int offset)
    {
        if (animator != null)
            animator.SetInteger("AnimState", offset + facingDir);
    }

    private void UpdateFacing(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        const float verticalBias = 3.73f; // tan(75°) — U/D only within ±15° of vertical
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * verticalBias)
            facingDir = direction.y >= 0f ? 1 : 3;
        else
            facingDir = direction.x >= 0f ? 0 : 2;
    }

    private void Update()
    {
        if (!isChaseModeActive) return;

        if (!isChasing && chaseCooldownTimer > 0f)
            chaseCooldownTimer -= Time.deltaTime;

        if (!isChasing && chaseCooldownTimer <= 0f)
            TryChaseNearestDeer();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API (called by AnimalManager)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by AnimalManager when a run starts with the farm dog equipped.
    /// Enables roaming and deer-chasing behavior.
    /// </summary>
    public void ActivateChaseMode()
    {
        isChaseModeActive = true;
        chaseCooldownTimer = 5f; // small grace period before first chase
        isChasing = false;

        // Take over animation control from AnimalVisual (home screen wanderer)
        if (animalVisual != null) animalVisual.PauseWander = true;

        roamCoroutine = StartCoroutine(RoamLoop());

        Debug.Log("[FarmDog] Chase mode activated.");
    }

    /// <summary>
    /// Called by AnimalManager when the run ends.
    /// Stops all roaming and chasing behavior.
    /// </summary>
    public void DeactivateChaseMode()
    {
        isChaseModeActive = false;

        if (roamCoroutine != null)
        {
            StopCoroutine(roamCoroutine);
            roamCoroutine = null;
        }

        StopAllCoroutines();
        isChasing = false;

        // Return animation control to AnimalVisual (home screen wanderer)
        if (animalVisual != null) animalVisual.PauseWander = false;

        Debug.Log("[FarmDog] Chase mode deactivated.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Roaming
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator RoamLoop()
    {
        while (true)
        {
            if (isChasing)
            {
                yield return null;
                continue;
            }

            Vector3 destination = GetRandomFarmPosition();

            SetAnim(WALK_OFFSET);
            yield return StartCoroutine(MoveTo(destination, roamSpeed));

            SetAnim(IDLE_OFFSET);
            yield return new WaitForSeconds(idleDuration + Random.Range(0f, 1.5f));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Chase Deer
    // ─────────────────────────────────────────────────────────────────────

    private void TryChaseNearestDeer()
    {
        if (ThreatWaveManager.Instance == null) return;

        AnimalThreat nearestDeer = ThreatWaveManager.Instance
            .FindNearestThreatOfType(AnimalThreatType.Deer, transform.position);

        if (nearestDeer == null) return;

        isChasing = true;
        StartCoroutine(ChaseDeer(nearestDeer));
    }

    private IEnumerator ChaseDeer(AnimalThreat deer)
    {
        SetAnim(RUN_OFFSET);

        while (deer != null && !deer.IsDone)
        {
            float dist = Vector3.Distance(transform.position, deer.transform.position);
            if (dist <= chaseReachDistance)
                break;

            Vector3 dir = deer.transform.position - transform.position;
            UpdateFacing(dir);
            SetAnim(RUN_OFFSET);

            float effectiveSpeed = chaseSpeed;
            if (ResearchManager.Instance != null)
                effectiveSpeed *= 1f + ResearchManager.Instance.GetBonus(Research.StatKey.DogEfficiency);

            transform.position = Vector3.MoveTowards(
                transform.position,
                deer.transform.position,
                effectiveSpeed * Time.deltaTime);

            yield return null;
        }

        if (deer != null && !deer.IsDone)
        {
            deer.ForceRepel();
            Debug.Log("[FarmDog] Chased off a deer!");
        }

        // Bark victory then return to idle
        SetAnim(BARK_OFFSET);
        yield return new WaitForSeconds(barkDuration);
        SetAnim(IDLE_OFFSET);

        float effectiveCooldown = chaseCooldown;
        if (ResearchManager.Instance != null)
            effectiveCooldown /= Mathf.Max(0.01f, 1f + ResearchManager.Instance.GetBonus(Research.StatKey.DogCooldown));
        chaseCooldownTimer = effectiveCooldown;
        isChasing = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator MoveTo(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (isChasing) yield break;

            Vector3 dir = target - transform.position;
            UpdateFacing(dir);
            SetAnim(WALK_OFFSET);

            transform.position = Vector3.MoveTowards(
                transform.position, target, speed * Time.deltaTime);

            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private Vector3 GetRandomFarmPosition()
    {
        if (FarmGrid.Instance == null) return Vector3.zero;

        List<int> zoneIds = FarmGrid.Instance.GetActiveZoneIds();
        if (zoneIds.Count == 0) return Vector3.zero;

        int randomZone = zoneIds[Random.Range(0, zoneIds.Count)];
        Vector3 zoneCenter = FarmGrid.Instance.GetZoneCenter(randomZone);

        return zoneCenter + new Vector3(
            Random.Range(-roamRadius, roamRadius),
            Random.Range(-roamRadius, roamRadius),
            0f);
    }
}
