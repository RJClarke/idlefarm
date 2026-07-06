using System;
using UnityEngine;

/// <summary>
/// Welcome-back orchestrator. SaveManager hands us the previous lastSeenUtcTicks via
/// SeedLastSeen during load. On the next frame we collect catch-up reports from the
/// other managers and (if the gap is meaningful) open the OfflineProgressModalUITK.
/// </summary>
[DefaultExecutionOrder(900)]
public class OfflineProgressManager : MonoBehaviour
{
    public static OfflineProgressManager Instance { get; private set; }

    /// <summary>Skip the modal for gaps shorter than this (quick app-switch / lockscreen).</summary>
    public const double MinGapMinutes = 1.0;

    private static long pendingLastSeenUtcTicks;
    private static bool pendingFlag;

    private static bool pendingRunActive;
    private static float pendingRunFarmSeconds;
    private static int pendingRunMoney;

    /// <summary>Called by SaveManager.LoadGame with the timestamp from the save file.</summary>
    public static void SeedLastSeen(long lastSeenUtcTicks)
    {
        pendingLastSeenUtcTicks = lastSeenUtcTicks;
        pendingFlag = true;
    }

    /// <summary>Called by SaveManager.LoadGame with the saved active-run snapshot (if any).</summary>
    public static void SeedRunSnapshot(bool runActive, float runFarmSeconds, int runMoney)
    {
        pendingRunActive = runActive;
        pendingRunFarmSeconds = runFarmSeconds;
        pendingRunMoney = runMoney;
    }


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Defer one frame so all managers finish their own Awake/Start.
        StartCoroutine(RunWhenReady());
    }

    private System.Collections.IEnumerator RunWhenReady()
    {
        yield return null; // one frame
        TryShow();
    }

    private void TryShow()
    {
        if (!pendingFlag || pendingLastSeenUtcTicks == 0) return;
        // Forward-only: a device clock set backward yields a 0 gap (no modal, no grants) instead
        // of a negative TimeSpan that would flow into the offline simulator.
        TimeSpan gap = TimeSpan.FromSeconds(OfflineClock.ForwardGapSeconds(pendingLastSeenUtcTicks, DateTime.UtcNow.Ticks));
        pendingFlag = false;
        if (gap.TotalMinutes < MinGapMinutes) return;
        ShowWithGap(gap);
    }

    /// <summary>
    /// Force the welcome-back modal to open now using the most recent seeded lastSeen.
    /// Used by DevTools to test the modal after backdating the save file.
    /// </summary>
    public void ForceShow()
    {
        if (pendingLastSeenUtcTicks == 0) { Debug.LogWarning("[OfflineProgressManager] ForceShow with no seeded lastSeen — nothing to show."); return; }
        // Forward-only clamp (mirrors TryShow): a rolled-back clock can't push a negative gap into the sim.
        TimeSpan gap = TimeSpan.FromSeconds(OfflineClock.ForwardGapSeconds(pendingLastSeenUtcTicks, DateTime.UtcNow.Ticks));
        pendingFlag = false;
        ShowWithGap(gap);
    }

    private static string FormatAway(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
    }

    private void ShowWithGap(TimeSpan gap)
    {
        // Cow + research catch-up happen regardless (existing behavior).
        int cowCompost = 0;
        if (AnimalManager.Instance != null)
        {
            cowCompost = AnimalManager.Instance.RunOfflineCompostCatchUp();
            cowCompost += AnimalManager.Instance.RunOfflineCowEatingCatchUp(gap.TotalSeconds);
        }
        var researchReport = ResearchManager.Instance != null ? ResearchManager.Instance.LastOfflineReport : null;

        // Daily-quest catch-up: drop any quests that should have been awarded while away.
        // Added straight to the quest queue like a normal drop (no modal UI); the quest
        // list refreshes via QuestManager's own OnQuestsDropped event.
        if (QuestManager.Instance != null)
            QuestManager.Instance.RunOfflineQuestCatchUp();

        // No active run -> existing welcome-back (cow/research only).
        if (!pendingRunActive)
        {
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.Open(gap, cowCompost, researchReport);
            return;
        }

        // Active run -> simulate the away-period.
        var outcome = OfflineRunContextBuilder.BuildAndSimulate(
            (float)gap.TotalSeconds, pendingRunFarmSeconds, pendingRunMoney);

        // Sim unavailable (offline_progress locked / no zones): SaveManager already resumed the run the
        // old way — just show the existing welcome-back modal.
        if (outcome == null)
        {
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.Open(gap, cowCompost, researchReport);
            return;
        }

        // Grant compost (untaxed) now, for both branches.
        if (CurrencyManager.Instance != null && outcome.compostGranted > 0)
            CurrencyManager.Instance.AddCompost(outcome.compostGranted);

        // Apply grants + run-state for the outcome, then present ONE unified welcome-back modal that
        // opens as a loading bar and expands into the result (full stats if ended, recap + Continue if not).
        if (outcome.result.bankrupt)
        {
            // Ended while away: grant taxed coins, record the run, finalize it.
            if (CurrencyManager.Instance != null && outcome.taxedCoins > 0)
                CurrencyManager.Instance.AddCoins(outcome.taxedCoins);

            if (RunStats.Instance != null)
                RunStats.Instance.IngestOfflineResult(
                    outcome.harvestedByCrop,
                    outcome.result.eatenByDeer, outcome.result.eatenByCrows, outcome.result.struckByLightning,
                    outcome.result.driedUp, outcome.result.rotted,
                    outcome.result.seedsPlanted, outcome.result.moneyEarned, outcome.taxedCoins);

            int survived = Mathf.FloorToInt(outcome.result.finalFarmSeconds);
            int real = Mathf.FloorToInt((float)gap.TotalSeconds);
            if (RunManager.Instance != null) RunManager.Instance.FinalizeOfflineBankruptcy(survived, real);

            Debug.Log($"[Offline] Run ENDED bankrupt at {survived}s; +{outcome.taxedCoins} coins, +{outcome.compostGranted} compost.");
        }
        else
        {
            // Survived: the run is already resumed (SaveManager). Apply simulated farm time + taxed money.
            if (CurrencyManager.Instance != null)
            {
                if (outcome.taxedCoins > 0) CurrencyManager.Instance.AddCoins(outcome.taxedCoins);
                CurrencyManager.Instance.SetMoney(outcome.taxedResumeMoney);
            }
            if (RunManager.Instance != null) RunManager.Instance.OverrideRunProgress(outcome.result.finalFarmSeconds);

            Debug.Log($"[Offline] Run CONTINUES at {outcome.result.finalFarmSeconds:F0}s; +{outcome.taxedCoins} coins, resume $ {outcome.taxedResumeMoney}, +{outcome.compostGranted} compost.");
        }

        var ledger = RunLedgerData.FromOffline(outcome, gap);
        string farmAdvanced = TimeFormat.Hms(outcome.result.finalFarmSeconds - pendingRunFarmSeconds);
        string nowHms = TimeFormat.Hms(outcome.result.finalFarmSeconds);
        if (OfflineProgressModalUITK.Instance != null)
            OfflineProgressModalUITK.Instance.OpenOfflineRun(
                gap, outcome.result.bankrupt, ledger, farmAdvanced, nowHms, onContinue: null);
    }
}
