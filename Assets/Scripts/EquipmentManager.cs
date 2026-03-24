using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages zone equipment assignments, spawns equipment GameObjects during runs,
/// and handles scarecrow repel/cooldown logic.
///
/// Lifecycle:
///   - Pre-run: SeedSelectionPopup calls AssignEquipment() per zone
///   - OnRunStarted: spawns visual GameObjects at zone centers, resets cooldowns
///   - During run: TryRepelThreat() / CheckFlightPathInterception() called by threats
///   - Update(): ticks cooldown timers, refills charges when cooldown expires
///   - OnRunEnded: destroys visuals, clears runtime state
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // Per-zone runtime state
    // ─────────────────────────────────────────────────────────────────────

    private class ZoneEquipmentState
    {
        public EquipmentData data;
        public GameObject visualInstance;
        public Vector3 position;          // world position of the equipment
        public int zoneId;

        // Repel state (Scarecrow / Fence)
        public float cooldownTimer;       // counts down to 0
        public int repelChargesRemaining;
        public bool isOnCooldown;

        // Fence state (line segments for the two outer edges)
        public Vector2 fenceEdgeAStart, fenceEdgeAEnd;
        public Vector2 fenceEdgeBStart, fenceEdgeBEnd;

        // Sprinkler state
        public bool sprinklerActive;      // true = watering, false = inactive
        public float sprinklerCycleTimer; // counts down current phase
        public bool sprinklerPinging;     // true = ping animation running

        // Scene-placed fence visual (not instantiated, found in scene)
        public FenceVisual fenceVisual;
    }

    private Dictionary<int, ZoneEquipmentState> zoneStates = new Dictionary<int, ZoneEquipmentState>();

    // Pre-run assignments (set by SeedSelectionPopup, survive between Show/Confirm)
    private Dictionary<int, EquipmentData> assignments = new Dictionary<int, EquipmentData>();

    // Cached scene FenceVisuals (found once, reused across runs)
    private Dictionary<int, FenceVisual> sceneFences = new Dictionary<int, FenceVisual>();

    // ─────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        CacheSceneFences();

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded += OnRunEnded;
        }

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunEnded;
        }

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }
    }

    private void Update()
    {
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) return;

        foreach (var kvp in zoneStates)
        {
            ZoneEquipmentState state = kvp.Value;

            if (state.data.equipmentType == EquipmentType.Sprinkler)
                UpdateSprinkler(state, kvp.Key);
            else
                UpdateRepelEquipment(state, kvp.Key);
        }
    }

    private void UpdateRepelEquipment(ZoneEquipmentState state, int zoneId)
    {
        if (!state.isOnCooldown) return;

        state.cooldownTimer -= Time.deltaTime;
        if (state.cooldownTimer <= 0f)
        {
            state.isOnCooldown = false;
            state.cooldownTimer = 0f;
            state.repelChargesRemaining = GetEffectiveCapacity(state.data);
            Debug.Log($"[Equipment] {state.data.displayName} in Zone {zoneId} recharged — {state.repelChargesRemaining} charges ready");
        }
    }

    private void UpdateSprinkler(ZoneEquipmentState state, int zoneId)
    {
        state.sprinklerCycleTimer -= Time.deltaTime;

        if (state.sprinklerActive)
        {
            // Water plants within AoE
            WaterPlantsInRange(state);

            // Start ping animation if not already running
            if (state.visualInstance != null)
            {
                SprinklerVisual sv = state.visualInstance.GetComponent<SprinklerVisual>();
                if (sv != null && !state.sprinklerPinging)
                {
                    sv.SetRadius(GetEffectiveAoE(state.data));
                    sv.StartPinging();
                    state.sprinklerPinging = true;
                }
            }

            // Check if active phase ended
            if (state.sprinklerCycleTimer <= 0f)
            {
                state.sprinklerActive = false;
                state.sprinklerCycleTimer = GetEffectiveCooldown(state.data);

                // Stop ping animation
                if (state.visualInstance != null)
                {
                    SprinklerVisual sv = state.visualInstance.GetComponent<SprinklerVisual>();
                    if (sv != null)
                    {
                        sv.StopPinging();
                        state.sprinklerPinging = false;
                    }
                }
            }
        }
        else
        {
            // Inactive phase — check if cooldown ended
            if (state.sprinklerCycleTimer <= 0f)
            {
                state.sprinklerActive = true;
                state.sprinklerCycleTimer = state.data.activeDurationSeconds;
            }
        }
    }

    private void WaterPlantsInRange(ZoneEquipmentState state)
    {
        if (FarmGrid.Instance == null) return;

        float radius = GetEffectiveAoE(state.data);
        float moisturePerSecond = GetEffectiveWaterPower(state.data);
        float moistureThisFrame = moisturePerSecond * Time.deltaTime;

        var tiles = FarmGrid.Instance.GetOccupiedTilesInZone(state.zoneId);
        foreach (SoilTile tile in tiles)
        {
            if (tile.CurrentPlant == null) continue;

            float dist = Vector3.Distance(tile.transform.position, state.position);
            if (dist > radius) continue;

            Plant plant = tile.CurrentPlant.GetComponent<Plant>();
            if (plant != null)
                plant.ApplyRain(moistureThisFrame);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scene Fence Cache
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Find all FenceVisual components in the scene and cache by zoneId.
    /// Called once on Start. These are reused across runs (shown/hidden, not instantiated).
    /// </summary>
    private void CacheSceneFences()
    {
        sceneFences.Clear();
        FenceVisual[] fences = FindObjectsByType<FenceVisual>(FindObjectsSortMode.None);
        foreach (FenceVisual fv in fences)
        {
            sceneFences[fv.zoneId] = fv;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Pre-Run Assignment API (called by SeedSelectionPopup)
    // ─────────────────────────────────────────────────────────────────────

    public void AssignEquipment(int zoneId, EquipmentData data)
    {
        if (data != null)
            assignments[zoneId] = data;
        else
            assignments.Remove(zoneId);
    }

    public void RemoveEquipment(int zoneId)
    {
        assignments.Remove(zoneId);
    }

    public EquipmentData GetAssignment(int zoneId)
    {
        assignments.TryGetValue(zoneId, out EquipmentData data);
        return data;
    }

    public void ClearAllAssignments()
    {
        assignments.Clear();
    }

    private void OnUpgradePurchased(string upgradeID)
    {
        // Refresh home screen visuals so fence level updates immediately
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive)
        {
            ShowHomeScreenEquipment();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Home Screen Equipment Visuals
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Show equipment visuals on the home screen (pre-run) based on current assignments.
    /// Fences use scene-placed FenceVisuals; scarecrows instantiate their prefab.
    /// </summary>
    public void ShowHomeScreenEquipment()
    {
        HideHomeScreenEquipment();
        CacheSceneFences();

        foreach (var kvp in assignments)
        {
            int zoneId = kvp.Key;
            EquipmentData data = kvp.Value;
            if (data == null) continue;

            if (data.equipmentType == EquipmentType.Fence)
            {
                if (sceneFences.TryGetValue(zoneId, out FenceVisual fenceVis))
                {
                    int fenceLevel = 1 + GetUpgradeLevel(data.aoeUpgradeID);
                    fenceVis.Show(fenceLevel);
                }
            }
            else if (data.visualPrefab != null && FarmGrid.Instance != null)
            {
                Vector3 zoneCenter = FarmGrid.Instance.GetZoneCenter(zoneId);
                GameObject visual = Instantiate(data.visualPrefab, zoneCenter, Quaternion.identity);
                visual.name = $"HomeEquip_{data.displayName}_Zone{zoneId}";
                homeScreenVisuals.Add(visual);
            }
        }
    }

    /// <summary>
    /// Hide all home screen equipment visuals.
    /// </summary>
    public void HideHomeScreenEquipment()
    {
        // Destroy instantiated visuals (scarecrow, sprinkler)
        foreach (GameObject go in homeScreenVisuals)
        {
            if (go != null) Destroy(go);
        }
        homeScreenVisuals.Clear();

        // Hide scene-placed fences
        foreach (var kvp in sceneFences)
        {
            if (kvp.Value != null)
                kvp.Value.Hide();
        }
    }

    private List<GameObject> homeScreenVisuals = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────
    // Run Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void OnRunStarted()
    {
        // Clean up home screen visuals (run visuals replace them)
        HideHomeScreenEquipment();

        // Re-cache scene fences each run (they may not exist during Start)
        CacheSceneFences();

        // Build runtime state from assignments
        zoneStates.Clear();

        foreach (var kvp in assignments)
        {
            int zoneId = kvp.Key;
            EquipmentData data = kvp.Value;
            if (data == null) continue;

            Vector3 zoneCenter = Vector3.zero;
            if (FarmGrid.Instance != null)
                zoneCenter = FarmGrid.Instance.GetZoneCenter(zoneId);

            ZoneEquipmentState state = new ZoneEquipmentState
            {
                data = data,
                position = zoneCenter,
                zoneId = zoneId,
                cooldownTimer = 0f,
                repelChargesRemaining = GetEffectiveCapacity(data),
                isOnCooldown = false,
                // Sprinkler starts active
                sprinklerActive = true,
                sprinklerCycleTimer = data.activeDurationSeconds
            };

            // Compute fence edge segments
            if (data.equipmentType == EquipmentType.Fence && FarmGrid.Instance != null)
            {
                int fenceLevel = 1 + GetUpgradeLevel(data.aoeUpgradeID);
                float coverage = data.GetFenceCoverage(fenceLevel);
                var edges = FarmGrid.Instance.GetZoneOuterEdges(zoneId, coverage);
                state.fenceEdgeAStart = edges.edgeAStart;
                state.fenceEdgeAEnd   = edges.edgeAEnd;
                state.fenceEdgeBStart = edges.edgeBStart;
                state.fenceEdgeBEnd   = edges.edgeBEnd;
            }

            // Visuals: fences use scene-placed objects, others instantiate prefabs
            if (data.equipmentType == EquipmentType.Fence)
            {
                if (sceneFences.TryGetValue(zoneId, out FenceVisual fenceVis))
                {
                    int fenceLevel = 1 + GetUpgradeLevel(data.aoeUpgradeID);
                    fenceVis.Show(fenceLevel);
                    state.fenceVisual = fenceVis;
                }
            }
            else if (data.visualPrefab != null)
            {
                state.visualInstance = Instantiate(data.visualPrefab, zoneCenter, Quaternion.identity);
                state.visualInstance.name = $"Equipment_{data.displayName}_Zone{zoneId}";
            }

            zoneStates[zoneId] = state;

            Debug.Log($"[Equipment] Activated {data.displayName} in Zone {zoneId} — " +
                      $"Capacity: {GetEffectiveCapacity(data)}, " +
                      $"Cooldown: {GetEffectiveCooldown(data):F1}s");
        }
    }

    private void OnRunEnded()
    {
        foreach (var kvp in zoneStates)
        {
            // Destroy instantiated visuals (scarecrow, sprinkler)
            if (kvp.Value.visualInstance != null)
                Destroy(kvp.Value.visualInstance);

            // Hide scene-placed fence visuals
            if (kvp.Value.fenceVisual != null)
                kvp.Value.fenceVisual.Hide();
        }
        zoneStates.Clear();

        // Restore home screen equipment visuals
        ShowHomeScreenEquipment();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Repel API (called by threat scripts)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Try to repel a threat at a specific position in a specific zone.
    /// Returns true if the threat was repelled (charge consumed).
    /// </summary>
    public bool TryRepelThreat(int zoneId, AnimalThreatType threatType, Vector3 threatPosition)
    {
        if (!zoneStates.TryGetValue(zoneId, out ZoneEquipmentState state)) return false;
        if (state.data.equipmentType == EquipmentType.Fence) return false; // fences use TryFenceInterception
        if (state.data.countersTheatType != threatType) return false;
        if (state.isOnCooldown) return false;
        if (state.repelChargesRemaining <= 0) return false;

        // Check if threat is within AoE
        float dist = Vector3.Distance(threatPosition, state.position);
        float radius = GetEffectiveAoE(state.data);
        if (dist > radius) return false;

        // Repel!
        ConsumeCharge(state);
        if (RunStats.Instance != null && state.data.equipmentType == EquipmentType.Scarecrow)
            RunStats.Instance.AddCrowRepelledByScarecrow();
        Debug.Log($"[Equipment] {state.data.displayName} repelled {threatType} in Zone {zoneId} — " +
                  $"charges left: {state.repelChargesRemaining}");
        return true;
    }

    /// <summary>
    /// Check ALL zones' equipment for interception of a threat at the given position.
    /// Returns the zone ID that repelled, or -1 if no interception.
    /// This enables cross-zone interception (e.g., Zone 1 scarecrow repels crow flying past toward Zone 3).
    /// </summary>
    public int CheckFlightPathInterception(Vector3 threatPosition, AnimalThreatType threatType)
    {
        foreach (var kvp in zoneStates)
        {
            ZoneEquipmentState state = kvp.Value;
            if (state.data.equipmentType == EquipmentType.Fence) continue; // fences use TryFenceInterception
            if (state.data.countersTheatType != threatType) continue;
            if (state.isOnCooldown) continue;
            if (state.repelChargesRemaining <= 0) continue;

            float dist = Vector3.Distance(threatPosition, state.position);
            float radius = GetEffectiveAoE(state.data);
            if (dist <= radius)
            {
                ConsumeCharge(state);
                if (RunStats.Instance != null && state.data.equipmentType == EquipmentType.Scarecrow)
                    RunStats.Instance.AddCrowRepelledByScarecrow();
                Debug.Log($"[Equipment] {state.data.displayName} intercepted {threatType} flying past Zone {kvp.Key} — " +
                          $"charges left: {state.repelChargesRemaining}");
                return kvp.Key;
            }
        }
        return -1;
    }

    /// <summary>
    /// Consume a repel charge and start cooldown if depleted.
    /// </summary>
    private void ConsumeCharge(ZoneEquipmentState state)
    {
        state.repelChargesRemaining--;
        if (state.repelChargesRemaining <= 0)
        {
            state.isOnCooldown = true;
            state.cooldownTimer = GetEffectiveCooldown(state.data);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Fence Interception (line-segment based)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Check if a deer moving from prevPos to currentPos crosses any fence segment.
    /// Only call this for incoming deer (not exiting). Returns the zone ID that
    /// repelled, or -1 if no interception.
    /// </summary>
    public int TryFenceInterception(Vector3 prevPos, Vector3 currentPos, AnimalThreatType threatType)
    {
        Vector2 p1 = new Vector2(prevPos.x, prevPos.y);
        Vector2 p2 = new Vector2(currentPos.x, currentPos.y);

        foreach (var kvp in zoneStates)
        {
            ZoneEquipmentState state = kvp.Value;
            if (state.data.equipmentType != EquipmentType.Fence) continue;
            if (state.data.countersTheatType != threatType) continue;
            if (state.isOnCooldown) continue;
            if (state.repelChargesRemaining <= 0) continue;

            if (SegmentsIntersect(p1, p2, state.fenceEdgeAStart, state.fenceEdgeAEnd) ||
                SegmentsIntersect(p1, p2, state.fenceEdgeBStart, state.fenceEdgeBEnd))
            {
                ConsumeCharge(state);
                if (RunStats.Instance != null) RunStats.Instance.AddDeerRepelledByFence();
                Debug.Log($"[Equipment] {state.data.displayName} blocked deer in Zone {kvp.Key} — " +
                          $"charges left: {state.repelChargesRemaining}");
                return kvp.Key;
            }
        }
        return -1;
    }

    /// <summary>
    /// Standard 2D line-segment intersection test using cross products.
    /// Returns true if segment (a1→a2) crosses segment (b1→b2).
    /// </summary>
    private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        Vector2 d1 = a2 - a1;
        Vector2 d2 = b2 - b1;
        float cross = d1.x * d2.y - d1.y * d2.x;

        if (Mathf.Abs(cross) < 1e-6f) return false; // parallel

        Vector2 d3 = b1 - a1;
        float t = (d3.x * d2.y - d3.y * d2.x) / cross;
        float u = (d3.x * d1.y - d3.y * d1.x) / cross;

        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Effective Stats (base + upgrades)
    // ─────────────────────────────────────────────────────────────────────

    public float GetEffectiveAoE(EquipmentData data)
    {
        int level = GetUpgradeLevel(data.aoeUpgradeID);
        return data.baseAoERadius + (level * data.aoeBonusPerLevel);
    }

    public float GetEffectiveCooldown(EquipmentData data)
    {
        int level = GetUpgradeLevel(data.cooldownUpgradeID);
        float cd = data.baseCooldownSeconds - (level * data.cooldownReductionPerLevel);
        return Mathf.Max(cd, data.minCooldownSeconds);
    }

    public int GetEffectiveCapacity(EquipmentData data)
    {
        int level = GetUpgradeLevel(data.capacityUpgradeID);
        return data.baseRepelCapacity + (level * data.capacityBonusPerLevel);
    }

    public float GetEffectiveWaterPower(EquipmentData data)
    {
        int level = GetUpgradeLevel(data.waterPowerUpgradeID);
        return data.baseMoisturePowerPerSecond + (level * data.waterPowerBonusPerLevel);
    }

    /// <summary>
    /// Convenience: get effective AoE for a specific zone's current equipment.
    /// </summary>
    public float GetAoERadius(int zoneId)
    {
        if (zoneStates.TryGetValue(zoneId, out ZoneEquipmentState state))
            return GetEffectiveAoE(state.data);
        return 0f;
    }

    /// <summary>
    /// Convenience: get effective cooldown for a specific zone's current equipment.
    /// </summary>
    public float GetCooldown(int zoneId)
    {
        if (zoneStates.TryGetValue(zoneId, out ZoneEquipmentState state))
            return GetEffectiveCooldown(state.data);
        return 0f;
    }

    /// <summary>
    /// Convenience: get effective capacity for a specific zone's current equipment.
    /// </summary>
    public int GetCapacity(int zoneId)
    {
        if (zoneStates.TryGetValue(zoneId, out ZoneEquipmentState state))
            return GetEffectiveCapacity(state.data);
        return 0;
    }

    private int GetUpgradeLevel(string upgradeID)
    {
        if (UpgradeManager.Instance == null) return 0;
        return UpgradeManager.Instance.GetCurrentLevel(upgradeID);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Debug
    // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Show Equipment State")]
    private void ShowEquipmentState()
    {
        Debug.Log("=== EQUIPMENT MANAGER ===");
        Debug.Log($"Assignments: {assignments.Count}");
        foreach (var kvp in assignments)
            Debug.Log($"  Zone {kvp.Key}: {kvp.Value.displayName}");

        Debug.Log($"Active states: {zoneStates.Count}");
        foreach (var kvp in zoneStates)
        {
            var s = kvp.Value;
            Debug.Log($"  Zone {kvp.Key}: {s.data.displayName} — " +
                      $"charges: {s.repelChargesRemaining}, cooldown: {s.isOnCooldown} ({s.cooldownTimer:F1}s)");
        }
    }
#endif
}
