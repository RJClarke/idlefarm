using UnityEngine;

/// <summary>
/// Manages visual changes when zones are unlocked
/// - Removes obstacle GameObjects (trees, rocks)
/// - Reveals dirt tilemap layers (grass → dirt transformation)
/// 
/// SETUP REQUIRED:
/// 1. Create separate Tilemap GameObjects for each zone's dirt pattern
/// 2. Paint dirt patterns on each zone tilemap
/// 3. Disable zone tilemaps initially (uncheck in Inspector)
/// 4. Place tree/rock GameObjects over locked zones
/// 5. Assign references in Inspector
/// </summary>
public class ZoneUnlockVisuals : MonoBehaviour
{
    [Header("Zone 2 - Second Zone")]
    [Tooltip("Tree/rock GameObject blocking zone 2")]
    [SerializeField] private GameObject zone2Obstacle;
    
    [Tooltip("Tilemap GameObject with dirt pattern for zone 2 (should start disabled)")]
    [SerializeField] private GameObject zone2DirtTilemap;
    
    [Header("Zone 3 - Third Zone")]
    [Tooltip("Tree/rock GameObject blocking zone 3")]
    [SerializeField] private GameObject zone3Obstacle;
    
    [Tooltip("Tilemap GameObject with dirt pattern for zone 3 (should start disabled)")]
    [SerializeField] private GameObject zone3DirtTilemap;
    
    [Header("Zone 4 - Fourth Zone")]
    [Tooltip("Tree/rock GameObject blocking zone 4")]
    [SerializeField] private GameObject zone4Obstacle;
    
    [Tooltip("Tilemap GameObject with dirt pattern for zone 4 (should start disabled)")]
    [SerializeField] private GameObject zone4DirtTilemap;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private void Start()
    {
        // Subscribe to upgrade events
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }
        else
        {
            Debug.LogWarning("UpgradeManager not found - zone unlock visuals won't work!");
        }

        // Check initial state (in case zones are already unlocked from save)
        CheckAllZones();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }
    }

    /// <summary>
    /// Called when any upgrade is purchased
    /// </summary>
    private void OnUpgradePurchased(string upgradeId)
    {
        switch (upgradeId)
        {
            case "zone_unlock_2":
                UnlockZone(2, zone2Obstacle, zone2DirtTilemap);
                break;
                
            case "zone_unlock_3":
                UnlockZone(3, zone3Obstacle, zone3DirtTilemap);
                break;
                
            case "zone_unlock_4":
                UnlockZone(4, zone4Obstacle, zone4DirtTilemap);
                break;
        }
    }

    /// <summary>
    /// Check all zones on start (handles save/load)
    /// </summary>
    private void CheckAllZones()
    {
        if (UpgradeManager.Instance == null) return;

        // Check each zone unlock status
        if (UpgradeManager.Instance.GetPermanentLevel("zone_unlock_2") > 0)
        {
            UnlockZone(2, zone2Obstacle, zone2DirtTilemap);
        }

        if (UpgradeManager.Instance.GetPermanentLevel("zone_unlock_3") > 0)
        {
            UnlockZone(3, zone3Obstacle, zone3DirtTilemap);
        }

        if (UpgradeManager.Instance.GetPermanentLevel("zone_unlock_4") > 0)
        {
            UnlockZone(4, zone4Obstacle, zone4DirtTilemap);
        }
    }

    /// <summary>
    /// Unlock a zone - remove obstacle and reveal dirt tilemap
    /// </summary>
    private void UnlockZone(int zoneID, GameObject obstacle, GameObject dirtTilemap)
    {
        // Remove the obstacle (tree/rock)
        if (obstacle != null)
        {
            Destroy(obstacle);
            


        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"  ⚠️ No obstacle assigned for Zone {zoneID}");
        }

        // Show the dirt tilemap (grass → dirt transformation)
        if (dirtTilemap != null)
        {
            dirtTilemap.SetActive(true);
            


        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"  ⚠️ No dirt tilemap assigned for Zone {zoneID}");
        }



    }

#if UNITY_EDITOR
    /// <summary>
    /// Test unlock Zone 2 in editor
    /// </summary>
    [ContextMenu("Test Unlock Zone 2")]
    private void TestUnlockZone2()
    {
        UnlockZone(2, zone2Obstacle, zone2DirtTilemap);
    }

    /// <summary>
    /// Test unlock Zone 3 in editor
    /// </summary>
    [ContextMenu("Test Unlock Zone 3")]
    private void TestUnlockZone3()
    {
        UnlockZone(3, zone3Obstacle, zone3DirtTilemap);
    }

    /// <summary>
    /// Test unlock Zone 4 in editor
    /// </summary>
    [ContextMenu("Test Unlock Zone 4")]
    private void TestUnlockZone4()
    {
        UnlockZone(4, zone4Obstacle, zone4DirtTilemap);
    }

    /// <summary>
    /// Reset all zones (for testing)
    /// </summary>
    [ContextMenu("Reset All Zones (Re-lock)")]
    private void ResetAllZones()
    {
        // Re-enable obstacles if they exist
        if (zone2Obstacle != null) zone2Obstacle.SetActive(true);
        if (zone3Obstacle != null) zone3Obstacle.SetActive(true);
        if (zone4Obstacle != null) zone4Obstacle.SetActive(true);

        // Hide dirt tilemaps
        if (zone2DirtTilemap != null) zone2DirtTilemap.SetActive(false);
        if (zone3DirtTilemap != null) zone3DirtTilemap.SetActive(false);
        if (zone4DirtTilemap != null) zone4DirtTilemap.SetActive(false);

        Debug.Log("🔒 All zones re-locked for testing");
    }

    /// <summary>
    /// Validate setup in editor
    /// </summary>
    [ContextMenu("Validate Setup")]
    private void ValidateSetup()
    {
        Debug.Log("=== Zone Unlock Visuals Setup Validation ===");
        
        int errors = 0;
        int warnings = 0;

        // Check Zone 2
        Debug.Log("Zone 2:");
        if (zone2Obstacle == null)
        {
            Debug.LogWarning("  ⚠️ No obstacle assigned");
            warnings++;
        }
        else
        {
            Debug.Log($"  ✓ Obstacle: {zone2Obstacle.name}");
        }

        if (zone2DirtTilemap == null)
        {
            Debug.LogError("  ❌ No dirt tilemap assigned!");
            errors++;
        }
        else
        {
            Debug.Log($"  ✓ Dirt tilemap: {zone2DirtTilemap.name}");
            if (zone2DirtTilemap.activeSelf)
            {
                Debug.LogWarning("  ⚠️ Dirt tilemap should start DISABLED");
                warnings++;
            }
        }

        // Check Zone 3
        Debug.Log("Zone 3:");
        if (zone3Obstacle == null)
        {
            Debug.LogWarning("  ⚠️ No obstacle assigned");
            warnings++;
        }
        else
        {
            Debug.Log($"  ✓ Obstacle: {zone3Obstacle.name}");
        }

        if (zone3DirtTilemap == null)
        {
            Debug.LogError("  ❌ No dirt tilemap assigned!");
            errors++;
        }
        else
        {
            Debug.Log($"  ✓ Dirt tilemap: {zone3DirtTilemap.name}");
            if (zone3DirtTilemap.activeSelf)
            {
                Debug.LogWarning("  ⚠️ Dirt tilemap should start DISABLED");
                warnings++;
            }
        }

        // Check Zone 4
        Debug.Log("Zone 4:");
        if (zone4Obstacle == null)
        {
            Debug.LogWarning("  ⚠️ No obstacle assigned");
            warnings++;
        }
        else
        {
            Debug.Log($"  ✓ Obstacle: {zone4Obstacle.name}");
        }

        if (zone4DirtTilemap == null)
        {
            Debug.LogError("  ❌ No dirt tilemap assigned!");
            errors++;
        }
        else
        {
            Debug.Log($"  ✓ Dirt tilemap: {zone4DirtTilemap.name}");
            if (zone4DirtTilemap.activeSelf)
            {
                Debug.LogWarning("  ⚠️ Dirt tilemap should start DISABLED");
                warnings++;
            }
        }

        // Summary
        Debug.Log("===================");
        if (errors == 0 && warnings == 0)
        {
            Debug.Log("✅ Setup looks good!");
        }
        else
        {
            Debug.Log($"❌ {errors} errors, ⚠️ {warnings} warnings");
        }
    }
#endif
}