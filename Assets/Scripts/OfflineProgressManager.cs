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
    public const double MinGapMinutes = 5.0;

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
        DateTime lastSeen = new DateTime(pendingLastSeenUtcTicks, DateTimeKind.Utc);
        TimeSpan gap = DateTime.UtcNow - lastSeen;
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
        DateTime lastSeen = new DateTime(pendingLastSeenUtcTicks, DateTimeKind.Utc);
        TimeSpan gap = DateTime.UtcNow - lastSeen;
        pendingFlag = false;
        ShowWithGap(gap);
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

        if (outcome.result.bankrupt)
        {
            // Ended while away: grant taxed coins, record the run, show the existing stats popup.
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
            var ledger = RunLedgerData.FromOffline(outcome, gap);
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.OpenEnded(gap, ledger,
                    onNewRun: () => { if (SeedSelectionPopup.Instance != null) SeedSelectionPopup.Instance.Show(); });
        }
        else
        {
            // Survived: the run is already resumed (SaveManager). Apply the simulated farm time + taxed
            // money on top, and grant taxed coins.
            if (CurrencyManager.Instance != null)
            {
                if (outcome.taxedCoins > 0) CurrencyManager.Instance.AddCoins(outcome.taxedCoins);
                CurrencyManager.Instance.SetMoney(outcome.taxedResumeMoney);
            }
            if (RunManager.Instance != null) RunManager.Instance.OverrideRunProgress(outcome.result.finalFarmSeconds);

            Debug.Log($"[Offline] Run CONTINUES at {outcome.result.finalFarmSeconds:F0}s; +{outcome.taxedCoins} coins, resume $ {outcome.taxedResumeMoney}, +{outcome.compostGranted} compost.");
            var ledger = RunLedgerData.FromOffline(outcome, gap);
            string farmAdvanced = TimeFormat.Hms(outcome.result.finalFarmSeconds - pendingRunFarmSeconds);
            string nowHms = TimeFormat.Hms(outcome.result.finalFarmSeconds);
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.OpenContinue(gap, ledger, farmAdvanced, nowHms, onContinue: null);
        }
    }
}
