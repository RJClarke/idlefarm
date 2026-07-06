using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Data structure for seed selection popup
/// Tracks which crops are assigned to which zones
/// Supports save/load via PlayerPrefs
/// </summary>
[Serializable]
public class SeedSelectionData
{
    // Zone ID -> Crop Name mappings
    public Dictionary<int, string> zoneAssignments = new Dictionary<int, string>();

    private const string PREFS_KEY = "SeedSelectionData";

    /// <summary>
    /// Assign a crop to a zone
    /// </summary>
    public void AssignCrop(int zoneID, CropData crop)
    {
        if (crop == null)
        {
            zoneAssignments.Remove(zoneID);
        }
        else
        {
            zoneAssignments[zoneID] = crop.cropName;
        }
    }

    /// <summary>
    /// Get crop name assigned to a zone (null if none)
    /// </summary>
    public string GetCropName(int zoneID)
    {
        if (zoneAssignments.TryGetValue(zoneID, out string cropName))
        {
            return cropName;
        }
        return null;
    }

    /// <summary>
    /// Clear assignment for a zone
    /// </summary>
    public void ClearZone(int zoneID)
    {
        zoneAssignments.Remove(zoneID);
    }

    /// <summary>
    /// Check if a crop is already assigned to any zone
    /// </summary>
    public bool IsCropAssigned(string cropName)
    {
        return zoneAssignments.ContainsValue(cropName);
    }

    /// <summary>
    /// Get which zone a crop is assigned to (-1 if not assigned)
    /// </summary>
    public int GetZoneForCrop(string cropName)
    {
        foreach (var kvp in zoneAssignments)
        {
            if (kvp.Value == cropName)
            {
                return kvp.Key;
            }
        }
        return -1;
    }

    /// <summary>
    /// True if at least one zone has a crop assigned (i.e. anything is equipped at all).
    /// </summary>
    public bool HasAnyAssignment() => zoneAssignments.Count > 0;

    /// <summary>
    /// Check if all unlocked zones have assignments
    /// </summary>
    public bool AreAllUnlockedZonesFilled()
    {
        // Check zones 1-4 based on unlock status
        bool zone1Unlocked = true; // Always unlocked
        bool zone2Unlocked = UpgradeManager.Instance != null && 
                             UpgradeManager.Instance.GetPermanentLevel("zone_unlock_2") > 0;
        bool zone3Unlocked = UpgradeManager.Instance != null && 
                             UpgradeManager.Instance.GetPermanentLevel("zone_unlock_3") > 0;
        bool zone4Unlocked = UpgradeManager.Instance != null && 
                             UpgradeManager.Instance.GetPermanentLevel("zone_unlock_4") > 0;

        // Check if all unlocked zones have crops
        if (zone1Unlocked && !zoneAssignments.ContainsKey(1)) return false;
        if (zone2Unlocked && !zoneAssignments.ContainsKey(2)) return false;
        if (zone3Unlocked && !zoneAssignments.ContainsKey(3)) return false;
        if (zone4Unlocked && !zoneAssignments.ContainsKey(4)) return false;

        return true;
    }

    /// <summary>
    /// Get first empty unlocked zone (1-4, or -1 if none)
    /// </summary>
    public int GetFirstEmptyZone()
    {
        // Check in order: 1, 2, 3, 4
        for (int zoneID = 1; zoneID <= 4; zoneID++)
        {
            if (IsZoneUnlocked(zoneID) && !zoneAssignments.ContainsKey(zoneID))
            {
                return zoneID;
            }
        }
        return -1; // All unlocked zones filled
    }

    /// <summary>
    /// Check if a zone is unlocked
    /// </summary>
    public bool IsZoneUnlocked(int zoneID)
    {
        if (zoneID == 1) return true; // Zone 1 always unlocked

        if (UpgradeManager.Instance == null) return false;

        return UpgradeManager.Instance.GetPermanentLevel($"zone_unlock_{zoneID}") > 0;
    }

    /// <summary>
    /// Convert to Dictionary<int, CropData> for HelperManager
    /// </summary>
    public Dictionary<int, CropData> ToZoneSeedDictionary(CropDatabase database)
    {
        Dictionary<int, CropData> result = new Dictionary<int, CropData>();

        foreach (var kvp in zoneAssignments)
        {
            CropData crop = database.GetCropByName(kvp.Value);
            if (crop != null)
            {
                result[kvp.Key] = crop;
            }
        }

        return result;
    }

    /// <summary>
    /// Save to PlayerPrefs
    /// </summary>
    public void Save()
    {
        string json = JsonUtility.ToJson(new SerializableData(zoneAssignments));
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load from PlayerPrefs
    /// </summary>
    public static SeedSelectionData Load()
    {
        SeedSelectionData data = new SeedSelectionData();

        if (PlayerPrefs.HasKey(PREFS_KEY))
        {
            string json = PlayerPrefs.GetString(PREFS_KEY);
            SerializableData loadedData = JsonUtility.FromJson<SerializableData>(json);
            
            if (loadedData != null && loadedData.keys != null && loadedData.values != null)
            {
                for (int i = 0; i < loadedData.keys.Length && i < loadedData.values.Length; i++)
                {
                    data.zoneAssignments[loadedData.keys[i]] = loadedData.values[i];
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Clear all assignments
    /// </summary>
    public void Clear()
    {
        zoneAssignments.Clear();
    }

    // Helper class for JSON serialization (Dictionary not directly serializable)
    [Serializable]
    private class SerializableData
    {
        public int[] keys;
        public string[] values;

        public SerializableData(Dictionary<int, string> dict)
        {
            keys = new int[dict.Count];
            values = new string[dict.Count];

            int index = 0;
            foreach (var kvp in dict)
            {
                keys[index] = kvp.Key;
                values[index] = kvp.Value;
                index++;
            }
        }
    }
}