using UnityEditor;
using UnityEngine;
using Research;

/// <summary>
/// DevTools for Pantry Economy Phase 3 (processing research gates). Drives the real research
/// pipeline (TryAssignResearch + DebugFinishCurrentResearches) so the kickoff / expansion /
/// efficiency flags unlock exactly as they would in play, then logs the effective slot caps and
/// fuel-efficiency bonuses the managers now read. Play-mode only. Menu: Tools/Pantry Phase 3.
/// </summary>
public static class PantryPhase3DevMenu
{
    private const string Menu = "Tools/Pantry Phase 3/";

    [MenuItem(Menu + "1. Grant 1,000,000 Coins", false, 0)]
    private static void GrantCoins()
    {
        if (!RequirePlay()) return;
        if (CurrencyManager.Instance == null) { Debug.LogWarning("[Phase3Dev] No CurrencyManager."); return; }
        CurrencyManager.Instance.AddCoins(1_000_000);
        Debug.Log($"[Phase3Dev] Granted coins → {CurrencyManager.Instance.Coins}.");
    }

    [MenuItem(Menu + "2. Complete Kickoff (Preserving + Smoking)", false, 20)]
    private static void CompleteKickoff()
    {
        if (!RequirePlay()) return;
        Complete("cannery_unlocked");
        Complete("smokehouse_unlocked");
        LogStatus();
    }

    [MenuItem(Menu + "3. Complete Cannery Expansion I + II", false, 21)]
    private static void CompleteCanneryExpansions()
    {
        if (!RequirePlay()) return;
        Complete("cannery_unlocked");      // ensure prereq
        Complete("cannery_expansion_1");
        Complete("cannery_expansion_2");
        LogStatus();
    }

    [MenuItem(Menu + "4. Complete Smokehouse Expansion", false, 22)]
    private static void CompleteSmokehouseExpansion()
    {
        if (!RequirePlay()) return;
        Complete("smokehouse_unlocked");   // ensure prereq
        Complete("smokehouse_expansion_1");
        LogStatus();
    }

    [MenuItem(Menu + "5. +5 Levels Each Fuel Efficiency", false, 23)]
    private static void LevelEfficiency()
    {
        if (!RequirePlay()) return;
        Complete("cannery_unlocked");
        Complete("smokehouse_unlocked");
        for (int i = 0; i < 5; i++)
        {
            Complete("cannery_burn_efficiency");
            Complete("smokehouse_burn_efficiency");
        }
        LogStatus();
    }

    [MenuItem(Menu + "Log Phase 3 Status", false, 40)]
    private static void LogStatusMenu()
    {
        if (!RequirePlay()) return;
        LogStatus();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Assign a research to the first unlocked slot and instantly finish one level.</summary>
    private static void Complete(string researchID)
    {
        var rm = ResearchManager.Instance;
        if (rm == null) { Debug.LogWarning("[Phase3Dev] No ResearchManager."); return; }

        // Make sure the assignment cost is always affordable.
        if (CurrencyManager.Instance != null && CurrencyManager.Instance.Coins < 500_000)
            CurrencyManager.Instance.AddCoins(1_000_000);

        int slot = FirstUnlockedSlot(rm);
        if (slot < 0) { Debug.LogWarning("[Phase3Dev] No unlocked research slot."); return; }

        if (!rm.TryAssignResearch(slot, researchID))
        {
            Debug.LogWarning($"[Phase3Dev] Could not assign '{researchID}' (visible/affordable/max?).");
            return;
        }
        rm.DebugFinishCurrentResearches();
        Debug.Log($"[Phase3Dev] Completed one level of '{researchID}'.");
    }

    private static int FirstUnlockedSlot(ResearchManager rm)
    {
        for (int i = 0; i < ResearchManager.SlotCount; i++)
            if (rm.IsSlotUnlocked(i)) return i;
        return -1;
    }

    private static void LogStatus()
    {
        var rm = ResearchManager.Instance;
        if (rm == null) { Debug.LogWarning("[Phase3Dev] No ResearchManager."); return; }

        string flags =
            $"Preserving={rm.IsFeatureUnlocked(FeatureFlag.CanneryUnlocked)}, " +
            $"Smoking={rm.IsFeatureUnlocked(FeatureFlag.SmokehouseUnlocked)}, " +
            $"CanneryExp1={rm.IsFeatureUnlocked(FeatureFlag.CanneryExpansion1)}, " +
            $"CanneryExp2={rm.IsFeatureUnlocked(FeatureFlag.CanneryExpansion2)}, " +
            $"SmokeExp1={rm.IsFeatureUnlocked(FeatureFlag.SmokehouseExpansion1)}";

        var cannery = CanneryManager.Instance;
        var smoke = SmokehouseManager.Instance;
        string caps =
            (cannery != null ? $"Cannery cap {cannery.MaxPurchasableSlots}→{cannery.EffectiveMaxPurchasableSlots}/{cannery.TotalMaxSlots}" : "Cannery n/a") + ", " +
            (smoke != null ? $"Smokehouse cap {smoke.MaxPurchasableSlots}→{smoke.EffectiveMaxPurchasableSlots}/{smoke.TotalMaxSlots}" : "Smokehouse n/a");

        string burn =
            $"CanneryEff bonus={rm.GetBonus(StatKey.CanneryBurnEfficiency):0.###}, " +
            $"SmokehouseEff bonus={rm.GetBonus(StatKey.SmokehouseBurnEfficiency):0.###}";

        Debug.Log($"[Phase3Dev] FLAGS: {flags}");
        Debug.Log($"[Phase3Dev] CAPS: {caps}");
        Debug.Log($"[Phase3Dev] BURN: {burn}");
    }

    private static bool RequirePlay()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[Phase3Dev] Enter Play mode first (managers only exist at runtime).");
        return false;
    }
}
