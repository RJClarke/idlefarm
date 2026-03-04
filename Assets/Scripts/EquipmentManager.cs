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

        // Sprinkler state
        public bool sprinklerActive;      // true = watering, false = inactive
        public float sprinklerCycleTimer; // counts down current phase
    }

    private Dictionary<int, ZoneEquipmentState> zoneStates = new Dictionary<int, ZoneEquipmentState>();

    // Pre-run assignments (set by SeedSelectionPopup, survive between Show/Confirm)
    private Dictionary<int, EquipmentData> assignments = new Dictionary<int, EquipmentData>();

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

            // Check if active phase ended
            if (state.sprinklerCycleTimer <= 0f)
            {
                state.sprinklerActive = false;
                state.sprinklerCycleTimer = GetEffectiveCooldown(state.data);
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

    // ─────────────────────────────────────────────────────────────────────
    // Run Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void OnRunStarted()
    {
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

            // Spawn visual prefab
            if (data.visualPrefab != null)
            {
                state.visualInstance = Instantiate(data.visualPrefab, zoneCenter, Quaternion.identity);
                state.visualInstance.name = $"Equipment_{data.displayName}_Zone{zoneId}";

                // Set zone ID on FenceVisual so it draws the correct L-shape
                FenceVisual fenceVis = state.visualInstance.GetComponent<FenceVisual>();
                if (fenceVis != null)
                    fenceVis.zoneId = zoneId;
            }

            zoneStates[zoneId] = state;

            Debug.Log($"[Equipment] Spawned {data.displayName} in Zone {zoneId} — " +
                      $"AoE: {GetEffectiveAoE(data):F1}, Capacity: {GetEffectiveCapacity(data)}, " +
                      $"Cooldown: {GetEffectiveCooldown(data):F1}s");
        }
    }

    private void OnRunEnded()
    {
        // Destroy all visual instances
        foreach (var kvp in zoneStates)
        {
            if (kvp.Value.visualInstance != null)
                Destroy(kvp.Value.visualInstance);
        }
        zoneStates.Clear();
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
        if (state.data.countersTheatType != threatType) return false;
        if (state.isOnCooldown) return false;
        if (state.repelChargesRemaining <= 0) return false;

        // Check if threat is within AoE
        float dist = Vector3.Distance(threatPosition, state.position);
        float radius = GetEffectiveAoE(state.data);
        if (dist > radius) return false;

        // Repel!
        ConsumeCharge(state);
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
            if (state.data.countersTheatType != threatType) continue;
            if (state.isOnCooldown) continue;
            if (state.repelChargesRemaining <= 0) continue;

            float dist = Vector3.Distance(threatPosition, state.position);
            float radius = GetEffectiveAoE(state.data);
            if (dist <= radius)
            {
                ConsumeCharge(state);
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
