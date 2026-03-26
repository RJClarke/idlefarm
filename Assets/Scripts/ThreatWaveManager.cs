using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ThreatWaveManager
///
/// Manages the escalating threat system over the course of a run.
///
/// Wave System:
///   - Run time is divided into equal-length "wave intervals" (default 60 seconds)
///   - Each interval, the wave number increments
///   - The wave number determines how many deer/crows are active and their hunger multiplier
///
/// Male/Female Deer:
///   - Assign both DeerF and DeerM prefabs to deerPrefabs array on AnimalThreatData
///   - A random prefab is picked each spawn for visual variety
///   - Falls back to a blank GameObject if no prefabs are assigned
/// </summary>
public class ThreatWaveManager : MonoBehaviour
{
    public static ThreatWaveManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────

    [Header("Threat Data")]
    [SerializeField] private AnimalThreatData deerData;
    [SerializeField] private AnimalThreatData crowData;

    [Header("Wave Timing")]
    [Tooltip("Seconds per wave interval.")]
    [SerializeField] private float waveIntervalSeconds = 60f;

    [Tooltip("Grace period (seconds) before ANY threat spawns at the start of a run.")]
    [SerializeField] private float initialGracePeriod = 15f;

    [Tooltip("How often the manager checks if new animals should be spawned (seconds).")]
    [SerializeField] private float spawnCheckInterval = 10f;

    [Header("Deer Scaling")]
    [SerializeField] private int deerStartWave = 1;
    [SerializeField] private int deerCountInterval = 20;
    [SerializeField] private int maxDeer = 6;

    [Header("Crow Scaling")]
    [SerializeField] private int crowStartWave = 10;
    [SerializeField] private int crowCountInterval = 20;
    [SerializeField] private int maxCrows = 6;

    [Header("Hunger Scaling")]
    [SerializeField] private float baseHunger = 500f;
    [SerializeField] private float crowBaseHunger = 80f;
    [SerializeField] private float hungerScalePerWave = 0.01f;

    [Header("Master Switch")]
    [SerializeField] private bool threatsEnabled = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private int debugCurrentWave = 0;
    [SerializeField] private int debugActiveDeer  = 0;
    [SerializeField] private int debugActiveCrows = 0;

    // ─────────────────────────────────────────────────────────────────────
    // Runtime State
    // ─────────────────────────────────────────────────────────────────────

    private List<AnimalThreat> activeThreats = new List<AnimalThreat>();

    // ─────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded   += OnRunEnded;
        }
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded   -= OnRunEnded;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Run Events
    // ─────────────────────────────────────────────────────────────────────

    private void OnRunStarted()
    {
        if (!threatsEnabled) return;

        StopAllCoroutines();
        ClearAllThreats();
        StartCoroutine(SpawnLoop());


    }

    private void OnRunEnded()
    {
        StopAllCoroutines();
        ClearAllThreats();


    }

    // ─────────────────────────────────────────────────────────────────────
    // Main Spawn Loop
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(initialGracePeriod);

        while (true)
        {
            yield return new WaitForSeconds(spawnCheckInterval);

            if (RunManager.Instance == null || !RunManager.Instance.IsRunActive)
                yield break;

            PruneDeadThreats();

            int wave         = GetCurrentWave();
            float hungerMult = GetHungerMultiplier(wave);
            float hunger     = baseHunger * hungerMult;
            int targetDeer   = GetDeerCount(wave);
            int targetCrows  = GetCrowCount(wave);

            debugCurrentWave = wave;
            debugActiveDeer  = CountActiveOfType(AnimalThreatType.Deer);
            debugActiveCrows = CountActiveOfType(AnimalThreatType.Crow);

            int deerToSpawn = targetDeer - CountActiveOfType(AnimalThreatType.Deer);
            for (int i = 0; i < deerToSpawn; i++)
                SpawnAnimal(deerData, typeof(DeerThreat), hunger);

            float crowHunger = crowBaseHunger * hungerMult;
            int crowsToSpawn = targetCrows - CountActiveOfType(AnimalThreatType.Crow);
            for (int i = 0; i < crowsToSpawn; i++)
                SpawnAnimal(crowData, typeof(CrowThreat), crowHunger);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Wave Formulas
    // ─────────────────────────────────────────────────────────────────────

    private int GetCurrentWave()
    {
        if (RunManager.Instance == null) return 1;
        float runTime = RunManager.Instance.CurrentRunDuration;
        return Mathf.Max(1, Mathf.FloorToInt(runTime / waveIntervalSeconds) + 1);
    }

    private float GetHungerMultiplier(int wave) =>
        1f + (wave - 1) * hungerScalePerWave;

    private int GetDeerCount(int wave)
    {
        if (wave < deerStartWave) return 0;
        int count = Mathf.FloorToInt((float)(wave - deerStartWave) / deerCountInterval) + 1;
        return Mathf.Min(count, maxDeer);
    }

    private int GetCrowCount(int wave)
    {
        if (wave < crowStartWave) return 0;
        int count = Mathf.FloorToInt((float)(wave - crowStartWave) / crowCountInterval) + 1;
        return Mathf.Min(count, maxCrows);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Spawning
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnAnimal(AnimalThreatData data, System.Type threatType, float hunger)
    {
        if (data == null) return;

        int zoneId = PickZoneWithPlants();
        if (zoneId < 0) return;

        GameObject go;

        if (data.prefabs != null && data.prefabs.Length > 0)
        {
            // Pick random prefab (male or female)
            GameObject prefab = data.prefabs[Random.Range(0, data.prefabs.Length)];
            go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            go.name = $"[Threat] {data.threatName}";
        }
        else
        {
            // Legacy fallback — blank GameObject with placeholder sprite
            go = new GameObject($"[Threat] {data.threatName}");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
        }

        // Only add the threat component if the prefab doesn't already have one
        // (prevents double-component bug where two lifecycles run simultaneously)
        AnimalThreat threat = (AnimalThreat)go.GetComponent(threatType);
        if (threat == null)
            threat = (AnimalThreat)go.AddComponent(threatType);

        threat.Initialize(data, hunger, zoneId);

        activeThreats.Add(threat);


    }

    // ─────────────────────────────────────────────────────────────────────
    // Zone Selection
    // ─────────────────────────────────────────────────────────────────────

    private int PickZoneWithPlants()
    {
        if (FarmGrid.Instance == null) return -1;

        List<int> validZones = new List<int>();
        foreach (int id in FarmGrid.Instance.GetActiveZoneIds())
        {
            var tiles = FarmGrid.Instance.GetOccupiedTilesInZone(id);
            if (tiles.Count > 0)
                validZones.Add(id);
        }

        if (validZones.Count == 0) return -1;
        return validZones[Random.Range(0, validZones.Count)];
    }

    // ─────────────────────────────────────────────────────────────────────
    // Housekeeping
    // ─────────────────────────────────────────────────────────────────────

    private void PruneDeadThreats()
    {
        activeThreats.RemoveAll(t => t == null || t.IsDone);
    }

    private int CountActiveOfType(AnimalThreatType type)
    {
        int count = 0;
        foreach (AnimalThreat t in activeThreats)
            if (t != null && !t.IsDone && t.ThreatType == type)
                count++;
        return count;
    }

    private void ClearAllThreats()
    {
        foreach (AnimalThreat t in activeThreats)
            if (t != null) Destroy(t.gameObject);
        activeThreats.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    public void SetThreatsEnabled(bool enabled)
    {
        threatsEnabled = enabled;
        if (!enabled) { StopAllCoroutines(); ClearAllThreats(); }
        else if (RunManager.Instance != null && RunManager.Instance.IsRunActive)
            OnRunStarted();
    }

    public int CurrentWave     => GetCurrentWave();
    public float CurrentHunger => baseHunger * GetHungerMultiplier(GetCurrentWave());

    /// <summary>
    /// Find the nearest active threat of a given type to a world position.
    /// Used by FarmDog to locate deer to chase.
    /// </summary>
    public AnimalThreat FindNearestThreatOfType(AnimalThreatType type, Vector3 position)
    {
        PruneDeadThreats();

        AnimalThreat nearest = null;
        float bestDist = float.MaxValue;

        foreach (AnimalThreat t in activeThreats)
        {
            if (t == null || t.IsDone) continue;
            if (t.ThreatType != type) continue;

            float dist = Vector3.Distance(position, t.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = t;
            }
        }

        return nearest;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public Force Spawn (Dev Tools)
    // ─────────────────────────────────────────────────────────────────────

    public void ForceSpawnDeer()
    {
        if (deerData == null) { Debug.LogWarning("No deer AnimalThreatData assigned!"); return; }
        SpawnAnimal(deerData, typeof(DeerThreat), CurrentHunger);
    }

    public void ForceSpawnCrow()
    {
        if (crowData == null) { Debug.LogWarning("No crow AnimalThreatData assigned!"); return; }
        float hunger = crowBaseHunger * GetHungerMultiplier(GetCurrentWave());
        SpawnAnimal(crowData, typeof(CrowThreat), hunger);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Editor Debug
    // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Debug: Force Spawn Deer")]
    private void DebugSpawnDeer() => ForceSpawnDeer();

    [ContextMenu("Debug: Force Spawn Crow")]
    private void DebugSpawnCrow() => ForceSpawnCrow();

    [ContextMenu("Debug: Log Wave Info")]
    private void DebugLogWaveInfo()
    {
        int wave = GetCurrentWave();
        Debug.Log($"=== Wave {wave} ===");
        Debug.Log($"  Run time:       {(RunManager.Instance != null ? RunManager.Instance.CurrentRunDuration : 0f):F0}s");
        Debug.Log($"  Hunger:         {CurrentHunger:F0} ({GetHungerMultiplier(wave):F2}x)");
        Debug.Log($"  Crow hunger:    {crowBaseHunger * GetHungerMultiplier(wave):F0}");
        Debug.Log($"  Deer cap:       {GetDeerCount(wave)}");
        Debug.Log($"  Crow cap:       {GetCrowCount(wave)}");
        Debug.Log($"  Active deer:    {CountActiveOfType(AnimalThreatType.Deer)}");
        Debug.Log($"  Active crows:   {CountActiveOfType(AnimalThreatType.Crow)}");
    }

    [ContextMenu("Debug: Clear All Threats")]
    private void DebugClearAll() => ClearAllThreats();
#endif
}