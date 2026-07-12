using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev/playtest helpers for Phase 2 (Lake + Fishing + Smokehouse) under Tools > Smokehouse.
/// All items act on the LIVE play-mode singletons — enter play mode first.
/// </summary>
public static class SmokehouseDevMenu
{
    [MenuItem("Tools/Smokehouse/Grant 20000 Coins + 5000 Wood")]
    private static void GrantCurrency()
    {
        if (CurrencyManager.Instance == null) { Debug.LogWarning("[SmokehouseDev] No CurrencyManager (enter play mode)."); return; }
        CurrencyManager.Instance.AddCoins(20000);
        CurrencyManager.Instance.AddWood(5000);
        Debug.Log($"[SmokehouseDev] Coins={CurrencyManager.Instance.Coins}, Wood={CurrencyManager.Instance.Wood}.");
    }

    [MenuItem("Tools/Smokehouse/Buy + Max Pole")]
    private static void MaxPole()
    {
        var fm = FishingManager.Instance;
        if (fm == null) { Debug.LogWarning("[SmokehouseDev] No FishingManager."); return; }
        if (!fm.HasPole) fm.TryBuyPole();
        int guard = 0;
        while (fm.CanUpgradePole() && guard++ < 10) fm.TryUpgradePole();
        Debug.Log($"[SmokehouseDev] HasPole={fm.HasPole}, PoleLevel={fm.PoleLevel}/{fm.MaxPoleLevel}.");
    }

    [MenuItem("Tools/Smokehouse/Cast Line")]
    private static void Cast()
    {
        var fm = FishingManager.Instance;
        if (fm == null) { Debug.LogWarning("[SmokehouseDev] No FishingManager."); return; }
        bool cast = fm.Cast(1f, Vector2.up); // dev: full-power straight cast
        Debug.Log($"[SmokehouseDev] Cast()={cast}, State={fm.State}.");
    }

    [MenuItem("Tools/Smokehouse/Stock Pantry (1 of each raw fish)")]
    private static void StockPantry()
    {
        if (PantryManager.Instance == null) { Debug.LogWarning("[SmokehouseDev] No PantryManager."); return; }
        for (int t = 1; t <= FishTiers.Count; t++) PantryManager.Instance.AddRaw(t);
        Debug.Log($"[SmokehouseDev] Raw fish now Perch={PantryManager.Instance.GetRaw(1)}, Bass={PantryManager.Instance.GetRaw(2)}, Pike={PantryManager.Instance.GetRaw(3)}.");
    }

    [MenuItem("Tools/Smokehouse/Mark Smokehouse Built")]
    private static void MarkBuilt() => BuildingState.MarkBuilt(BuildingState.SmokehouseKey);

    [MenuItem("Tools/Smokehouse/Load Perch into Smoker")]
    private static void LoadPerch()
    {
        var sm = SmokehouseManager.Instance;
        if (sm == null) { Debug.LogWarning("[SmokehouseDev] No SmokehouseManager."); return; }
        bool loaded = sm.TryLoadFish(1);
        Debug.Log($"[SmokehouseDev] TryLoadFish(Perch)={loaded}. Slots owned={sm.SlotsOwned}.");
    }

    [MenuItem("Tools/Smokehouse/Fill Furnace")]
    private static void FillFurnace()
    {
        var sm = SmokehouseManager.Instance;
        if (sm == null) { Debug.LogWarning("[SmokehouseDev] No SmokehouseManager."); return; }
        sm.FillFurnace();
        Debug.Log($"[SmokehouseDev] Fuel now {sm.State.fuelWood:0.#}.");
    }

    [MenuItem("Tools/Smokehouse/Fast-Forward Smoking (2s left)")]
    private static void FastForward()
    {
        var sm = SmokehouseManager.Instance;
        if (sm == null) { Debug.LogWarning("[SmokehouseDev] No SmokehouseManager."); return; }
        int touched = 0;
        foreach (var s in sm.State.slots)
            if (ProcessingMath.SlotIsCooking(s)) { s.cookSecondsRemaining = 2; touched++; }
        Debug.Log($"[SmokehouseDev] Fast-forwarded {touched} smoking slot(s) to 2s. (Play running → will finish + drain to Pantry.)");
    }

    [MenuItem("Tools/Smokehouse/Report Pantry + Coins")]
    private static void Report()
    {
        var p = PantryManager.Instance;
        var c = CurrencyManager.Instance;
        if (p == null || c == null) { Debug.LogWarning("[SmokehouseDev] Missing managers."); return; }
        Debug.Log($"[SmokehouseDev] Coins={c.Coins} | Raw P/B/Pike={p.GetRaw(1)}/{p.GetRaw(2)}/{p.GetRaw(3)} | Smoked P/B/Pike={p.GetSmoked(1)}/{p.GetSmoked(2)}/{p.GetSmoked(3)}.");
    }

    [MenuItem("Tools/Smokehouse/Sell 1 Smoked Perch")]
    private static void SellSmokedPerch()
    {
        var sm = SmokehouseManager.Instance;
        if (sm == null) { Debug.LogWarning("[SmokehouseDev] No SmokehouseManager."); return; }
        bool sold = sm.TrySellSmoked(1);
        Debug.Log($"[SmokehouseDev] TrySellSmoked(Perch)={sold}. Coins={CurrencyManager.Instance.Coins}.");
    }

    [MenuItem("Tools/Smokehouse/Open Popup")]
    private static void OpenPopup()
    {
        if (SmokehousePopupUITK.Instance == null) { Debug.LogWarning("[SmokehouseDev] No SmokehousePopupUITK."); return; }
        SmokehousePopupUITK.Instance.Open();
    }

    // Undo the "Mark Smokehouse Built" testing side-effect (PlayerPrefs persists across sessions),
    // so the Carpenter "Build Smokehouse" flow can be tested fresh. Works in edit mode.
    [MenuItem("Tools/Smokehouse/Reset Smokehouse Built Flag")]
    private static void ResetBuilt()
    {
        PlayerPrefs.DeleteKey(BuildingState.SmokehouseKey);
        PlayerPrefs.Save();
        Debug.Log("[SmokehouseDev] Cleared Smokehouse built flag (PlayerPrefs). Rebuild via the Carpenter.");
    }
}
