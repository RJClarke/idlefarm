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

    private SpriteRenderer spriteRenderer;
    private Coroutine roamCoroutine;
    private float chaseCooldownTimer;
    private bool isChasing;
    private bool isChaseModeActive;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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

            // Pick a random destination near current position or a random farm point
            Vector3 destination = GetRandomFarmPosition();

            // Walk to destination
            yield return StartCoroutine(MoveTo(destination, roamSpeed));

            // Idle for a bit
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
        // Run toward the deer
        while (deer != null && !deer.IsDone)
        {
            float dist = Vector3.Distance(transform.position, deer.transform.position);
            if (dist <= chaseReachDistance)
                break;

            // Face direction of travel
            float dirX = deer.transform.position.x - transform.position.x;
            if (spriteRenderer != null && Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0f;

            transform.position = Vector3.MoveTowards(
                transform.position,
                deer.transform.position,
                chaseSpeed * Time.deltaTime);

            yield return null;
        }

        // Repel the deer
        if (deer != null && !deer.IsDone)
        {
            deer.ForceRepel();
            Debug.Log("[FarmDog] Chased off a deer!");
        }

        // Reset cooldown
        chaseCooldownTimer = chaseCooldown;
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

            float dirX = target.x - transform.position.x;
            if (spriteRenderer != null && Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0f;

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
