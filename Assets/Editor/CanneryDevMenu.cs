using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev/playtest helpers for the Cannery (Pantry Economy Phase 1) under Tools > Cannery.
/// All items act on the LIVE play-mode singletons — enter play mode first.
/// </summary>
public static class CanneryDevMenu
{
    [MenuItem("Tools/Cannery/Grant 5000 Coins + 1000 Wood")]
    private static void GrantCurrency()
    {
        if (CurrencyManager.Instance == null) { Debug.LogWarning("[CanneryDev] No CurrencyManager (enter play mode)."); return; }
        CurrencyManager.Instance.AddCoins(5000);
        CurrencyManager.Instance.AddWood(1000);
    }

    [MenuItem("Tools/Cannery/Mark Cannery Built")]
    private static void MarkBuilt() => BuildingState.MarkBuilt(BuildingState.CanneryKey);

    [MenuItem("Tools/Cannery/Intake Test Crop x4")]
    private static void IntakeTestCrop()
    {
        if (CanneryManager.Instance == null) { Debug.LogWarning("[CanneryDev] No CanneryManager."); return; }
        string[] guids = AssetDatabase.FindAssets("t:CropData");
        if (guids.Length == 0) { Debug.LogWarning("[CanneryDev] No CropData assets found."); return; }
        var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        int accepted = 0;
        for (int i = 0; i < 4; i++)
            if (CanneryManager.Instance.TryIntake(crop)) accepted++;
        Debug.Log($"[CanneryDev] Intake '{crop.cropName}': {accepted}/4 accepted.");
    }

    [MenuItem("Tools/Cannery/Fill Furnace")]
    private static void FillFurnace()
    {
        if (CanneryManager.Instance == null) { Debug.LogWarning("[CanneryDev] No CanneryManager."); return; }
        CanneryManager.Instance.FillFurnace();
        Debug.Log($"[CanneryDev] Fuel now {CanneryManager.Instance.State.fuelWood:0.#}.");
    }

    [MenuItem("Tools/Cannery/Fast-Forward Cooking (5s left)")]
    private static void FastForwardCooking()
    {
        if (CanneryManager.Instance == null) { Debug.LogWarning("[CanneryDev] No CanneryManager."); return; }
        var st = CanneryManager.Instance.State;
        int touched = 0;
        foreach (var s in st.slots)
            if (ProcessingMath.SlotIsCooking(s)) { s.cookSecondsRemaining = 5; touched++; }
        Debug.Log($"[CanneryDev] Fast-forwarded {touched} cooking jar(s) to 5s remaining.");
    }

    [MenuItem("Tools/Cannery/Open Popup")]
    private static void OpenPopup()
    {
        if (CanneryPopupUITK.Instance == null) { Debug.LogWarning("[CanneryDev] No CanneryPopupUITK."); return; }
        CanneryPopupUITK.Instance.Open();
    }

    [MenuItem("Tools/Cannery/Sell All Jars")]
    private static void SellAll()
    {
        if (CanneryManager.Instance == null) { Debug.LogWarning("[CanneryDev] No CanneryManager."); return; }
        int gained = CanneryManager.Instance.SellAllJars();
        Debug.Log($"[CanneryDev] Sold all jars for {gained} coins.");
    }
}
