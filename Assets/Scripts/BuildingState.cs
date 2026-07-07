using System;
using UnityEngine;

/// <summary>
/// Lightweight global registry for "has X been constructed?" persistent flags.
/// Used by Carpenter purchases + visibility gates on world buildings.
/// Persisted via PlayerPrefs; will move to JSON save when Carpenter gains more entries.
/// </summary>
public static class BuildingState
{
    public const string GreenhouseKey = "building_greenhouse_built";
    public const string CanneryKey = "building_cannery_built";

    public static event Action<string> OnBuildingBuilt; // key

    public static bool IsBuilt(string key)
        => !string.IsNullOrEmpty(key) && PlayerPrefs.GetInt(key, 0) == 1;

    public static void MarkBuilt(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (IsBuilt(key)) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        OnBuildingBuilt?.Invoke(key);
    }
}
