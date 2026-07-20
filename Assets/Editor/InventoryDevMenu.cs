using UnityEditor;
using UnityEngine;

/// <summary>Play-mode cheats for exercising the item inventory (Reputation Phase 1).</summary>
public static class InventoryDevMenu
{
    [MenuItem("Tools/Inventory/Grant 50 Of First 3 Crops")]
    private static void GrantCrops()
    {
        var inv = ItemInventoryManager.Instance;
        if (inv == null) { Debug.LogWarning("[InvDev] Needs play mode + manager."); return; }
        string[] names = { "Strawberry", "Blueberry", "Tomato" };
        foreach (string n in names) { inv.AddCrop(n, 50, out _); }
        Debug.Log("[InvDev] Granted 50x Strawberry/Blueberry/Tomato");
    }

    [MenuItem("Tools/Inventory/Grant 20 Eggs")]
    private static void GrantEggs()
    {
        if (ItemInventoryManager.Instance == null) { Debug.LogWarning("[InvDev] Needs play mode."); return; }
        ItemInventoryManager.Instance.AddEggs(20, out _);
        Debug.Log("[InvDev] Granted 20 eggs");
    }

    [MenuItem("Tools/Inventory/Toggle Collect Mode")]
    private static void ToggleCollect()
    {
        var inv = ItemInventoryManager.Instance;
        if (inv == null) { Debug.LogWarning("[InvDev] Needs play mode."); return; }
        inv.CollectMode = !inv.CollectMode;
        Debug.Log($"[InvDev] CollectMode = {inv.CollectMode}");
    }

    [MenuItem("Tools/Inventory/Log Stacks")]
    private static void LogStacks()
    {
        var inv = ItemInventoryManager.Instance;
        if (inv == null) { Debug.LogWarning("[InvDev] Needs play mode."); return; }
        var sb = new System.Text.StringBuilder("[InvDev] Stacks: ");
        foreach (var kv in inv.Crops) sb.Append($"{kv.Key}={kv.Value} ");
        sb.Append($"Eggs={inv.Eggs} CollectMode={inv.CollectMode}");
        Debug.Log(sb.ToString());
    }
}
