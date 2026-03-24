using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Farm Dog — a global farm defender that roams casually and chases deer.
///
/// Behavior:
///   - Spawns at run start if unlocked (dog_unlock level > 0)
///   - Roams randomly around the farm between active zones
///   - Every 30 seconds, scans for an active deer and chases it off
///   - Can scare 2 deer per minute (30s cooldown between chases)
///   - Destroyed at run end
///
/// Unlock: Purchased in Market for 500 Coins (UnlockData asset)
/// </summary>
public class FarmDog : MonoBehaviour
{
    public static FarmDog Instance { get; private set; }

    [Header("Roaming")]
    [SerializeField] private float roamSpeed = 1.5f;
    [SerializeField] private float idleDuration = 2f;
    [SerializeField] private float roamRadius = 3f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float chaseCooldown = 30f;
    [SerializeField] private float chaseReachDistance = 0.3f;

    [Header("Unlock")]
    [SerializeField] private string unlockID = "dog_unlock";

    private SpriteRenderer spriteRenderer;
    private GameObject dogInstance;
    private Coroutine roamCoroutine;
    private float chaseCooldownTimer;
    private bool isChasing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded += OnRunEnded;
        }
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunEnded;
        }
    }

    private void Update()
    {
        if (dogInstance == null) return;
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) return;

        if (!isChasing && chaseCooldownTimer > 0f)
            chaseCooldownTimer -= Time.deltaTime;

        if (!isChasing && chaseCooldownTimer <= 0f)
            TryChaseNearestDeer();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Run Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void OnRunStarted()
    {
        if (!IsUnlocked()) return;

        SpawnDog();
        chaseCooldownTimer = 5f; // small grace period before first chase
        isChasing = false;
        roamCoroutine = StartCoroutine(RoamLoop());

        Debug.Log("[FarmDog] Dog spawned for this run!");
    }

    private void OnRunEnded()
    {
        DespawnDog();
    }

    private bool IsUnlocked()
    {
        if (UpgradeManager.Instance == null) return false;
        return UpgradeManager.Instance.GetPermanentLevel(unlockID) > 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Spawn / Despawn
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnDog()
    {
        if (dogInstance != null) return;

        Vector3 spawnPos = GetRandomFarmPosition();

        dogInstance = new GameObject("[FarmDog]");
        dogInstance.transform.position = spawnPos;

        spriteRenderer = dogInstance.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 12;

        dogInstance.AddComponent<DogVisual>();
    }

    private void DespawnDog()
    {
        if (roamCoroutine != null)
        {
            StopCoroutine(roamCoroutine);
            roamCoroutine = null;
        }

        StopAllCoroutines();
        isChasing = false;

        if (dogInstance != null)
        {
            Destroy(dogInstance);
            dogInstance = null;
        }
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
        if (dogInstance == null) return;

        AnimalThreat nearestDeer = ThreatWaveManager.Instance
            .FindNearestThreatOfType(AnimalThreatType.Deer, dogInstance.transform.position);

        if (nearestDeer == null) return;

        isChasing = true;
        StartCoroutine(ChaseDeer(nearestDeer));
    }

    private IEnumerator ChaseDeer(AnimalThreat deer)
    {
        // Run toward the deer
        while (deer != null && !deer.IsDone)
        {
            float dist = Vector3.Distance(dogInstance.transform.position, deer.transform.position);
            if (dist <= chaseReachDistance)
                break;

            // Face direction of travel
            float dirX = deer.transform.position.x - dogInstance.transform.position.x;
            if (spriteRenderer != null && Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0f;

            dogInstance.transform.position = Vector3.MoveTowards(
                dogInstance.transform.position,
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
        if (dogInstance == null) yield break;

        while (Vector3.Distance(dogInstance.transform.position, target) > 0.05f)
        {
            if (dogInstance == null) yield break;
            if (isChasing) yield break;

            float dirX = target.x - dogInstance.transform.position.x;
            if (spriteRenderer != null && Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0f;

            dogInstance.transform.position = Vector3.MoveTowards(
                dogInstance.transform.position, target, speed * Time.deltaTime);

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
