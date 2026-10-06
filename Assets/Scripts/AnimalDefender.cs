using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shared behaviour for an equipped animal that defends the farm during a run: patrol the active
/// zones, and on a cooldown run down the nearest threat it cares about and scare it off.
///
/// AnimalManager drives the lifecycle — it finds this component by base type on the equipped
/// animal's visual and calls ActivateChaseMode/DeactivateChaseMode around the run. Subclasses
/// supply what differs: which threats they hunt, how fast/often they can chase, and which
/// animation slots their spritesheet actually has.
///
/// Animation states are the usual AnimState int convention (0-3 Idle, 4-7 Walk R/U/L/D); animals
/// with richer sheets expose more (the dog has Run at 8-11 and Bark at 12-15), which is why the
/// run and victory offsets are subclass properties rather than constants.
/// </summary>
public abstract class AnimalDefender : MonoBehaviour
{
    [Header("Roaming")]
    [SerializeField] protected float roamSpeed = 1.5f;
    [SerializeField] protected float idleDuration = 2f;
    [SerializeField] protected float roamRadius = 3f;

    [Header("Chase")]
    [SerializeField] protected float chaseSpeed = 4f;
    [SerializeField] protected float chaseCooldown = 30f;
    [SerializeField] protected float chaseReachDistance = 0.3f;
    [Range(0.5f, 5f)]
    [Tooltip("Length of the little victory beat after a successful scare (the dog's bark).")]
    [FormerlySerializedAs("barkDuration")] // keeps FarmDogVisual.prefab's tuned 2.5s
    [SerializeField] protected float victoryDuration = 1.2f;

    protected Animator animator;
    private AnimalVisual animalVisual;
    private Coroutine roamCoroutine;
    private float chaseCooldownTimer;
    private bool isChasing;
    private bool isChaseModeActive;

    // 0=R, 1=U, 2=L, 3=D
    private int facingDir = 3;

    protected const int IDLE_OFFSET = 0;
    protected const int WALK_OFFSET = 4;

    // ── Almanac read-outs (base values; bonuses are applied by the subclass hooks below) ──
    public float BaseWalkSpeed => roamSpeed;
    public float BaseRunSpeed => chaseSpeed;
    public float BaseChaseCooldown => chaseCooldown;
    public float RunSpeedMultiplier => SpeedMultiplier;
    public float ChaseCooldownDivisor => CooldownDivisor;
    public AnimalThreatType[] Chases => TargetTypes;

    /// <summary>This animal's id for per-animal run stats.</summary>
    protected string AnimalId => animalVisual != null && animalVisual.Data != null ? animalVisual.Data.animalID : LogName;

    // ── Subclass hooks ──────────────────────────────────────────────────────

    /// <summary>Threat types this animal will chase, in priority order.</summary>
    protected abstract AnimalThreatType[] TargetTypes { get; }

    /// <summary>Anim offset used while sprinting at a target. Sheets without a run cycle reuse Walk.</summary>
    protected virtual int RunOffset => WALK_OFFSET;

    /// <summary>Anim offset for the little victory beat after a successful scare (bark, honk, ...).</summary>
    protected virtual int VictoryOffset => IDLE_OFFSET;

    /// <summary>Multiplier on chase speed from research, etc. 1 = no bonus.</summary>
    protected virtual float SpeedMultiplier => 1f;

    /// <summary>Divisor on the cooldown from research, etc. 1 = no bonus.</summary>
    protected virtual float CooldownDivisor => 1f;

    /// <summary>Animator.speed while sprinting — lets a walk-only sheet read as hurrying.</summary>
    protected virtual float ChaseAnimSpeed => 1f;

    /// <summary>Record the scare in RunStats. Called once per threat actually repelled.</summary>
    protected abstract void RecordChase(AnimalThreat threat);

    /// <summary>Name used in logs.</summary>
    protected virtual string LogName => GetType().Name;

    // ── Unity ───────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        animalVisual = GetComponent<AnimalVisual>();
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        YSort.Ensure(gameObject);
    }

    private void Update()
    {
        if (!isChaseModeActive) return;

        if (!isChasing && chaseCooldownTimer > 0f)
            chaseCooldownTimer -= Time.deltaTime;

        if (!isChasing && chaseCooldownTimer <= 0f)
            TryChaseNearestThreat();
    }

    // ── Public API (called by AnimalManager) ────────────────────────────────

    /// <summary>Run started with this animal equipped — start patrolling and chasing.</summary>
    public void ActivateChaseMode()
    {
        if (isChaseModeActive) return;
        isChaseModeActive = true;
        chaseCooldownTimer = 5f; // small grace period before the first chase
        isChasing = false;

        // Take over animation control from AnimalVisual (the home-screen wanderer).
        if (animalVisual != null) animalVisual.PauseWander = true;

        roamCoroutine = StartCoroutine(RoamLoop());
        Debug.Log($"[{LogName}] Chase mode activated.");
    }

    /// <summary>Run ended — stop everything and hand the wander back to AnimalVisual.</summary>
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

        if (animator != null) animator.speed = 1f;
        if (animalVisual != null) animalVisual.PauseWander = false;

        Debug.Log($"[{LogName}] Chase mode deactivated.");
    }

    // ── Roaming ─────────────────────────────────────────────────────────────

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

    // ── Chasing ─────────────────────────────────────────────────────────────

    private void TryChaseNearestThreat()
    {
        if (ThreatWaveManager.Instance == null) return;

        AnimalThreat nearest = null;
        float bestDist = float.MaxValue;
        foreach (AnimalThreatType type in TargetTypes)
        {
            AnimalThreat candidate = ThreatWaveManager.Instance
                .FindNearestThreatOfType(type, transform.position);
            if (candidate == null) continue;

            float dist = Vector3.Distance(transform.position, candidate.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = candidate;
            }
        }

        if (nearest == null) return;

        isChasing = true;
        StartCoroutine(Chase(nearest));
    }

    private IEnumerator Chase(AnimalThreat threat)
    {
        if (animator != null) animator.speed = ChaseAnimSpeed;
        SetAnim(RunOffset);

        while (threat != null && !threat.IsDone)
        {
            if (Vector3.Distance(transform.position, threat.transform.position) <= chaseReachDistance)
                break;

            Vector3 dir = threat.transform.position - transform.position;
            UpdateFacing(dir);
            SetAnim(RunOffset);

            transform.position = Vector3.MoveTowards(
                transform.position,
                threat.transform.position,
                chaseSpeed * SpeedMultiplier * Time.deltaTime);

            yield return null;
        }

        if (animator != null) animator.speed = 1f;

        if (threat != null && !threat.IsDone)
        {
            threat.ForceRepel();
            RecordChase(threat);
            Debug.Log($"[{LogName}] Chased off a {threat.ThreatType}!");
        }

        SetAnim(VictoryOffset);
        yield return new WaitForSeconds(victoryDuration);
        SetAnim(IDLE_OFFSET);

        chaseCooldownTimer = chaseCooldown / Mathf.Max(0.01f, CooldownDivisor);
        isChasing = false;
    }

    // ── Movement / animation ────────────────────────────────────────────────

    private IEnumerator MoveTo(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (isChasing) yield break;

            Vector3 dir = target - transform.position;
            UpdateFacing(dir);
            SetAnim(WALK_OFFSET);

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
    }

    protected void SetAnim(int offset)
    {
        if (animator != null) animator.SetInteger("AnimState", offset + facingDir);
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
