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

    /// <summary>Called by SaveManager.LoadGame with the timestamp from the save file.</summary>
    public static void SeedLastSeen(long lastSeenUtcTicks)
    {
        pendingLastSeenUtcTicks = lastSeenUtcTicks;
        pendingFlag = true;
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
        int cowCompost = 0;
        if (AnimalManager.Instance != null)
        {
            cowCompost = AnimalManager.Instance.RunOfflineCompostCatchUp();              // passive trickle
            cowCompost += AnimalManager.Instance.RunOfflineCowEatingCatchUp(gap.TotalSeconds); // grazing (continued offline run)
        }
        var researchReport = ResearchManager.Instance != null
            ? ResearchManager.Instance.LastOfflineReport
            : null;
        if (OfflineProgressModalUITK.Instance != null)
            OfflineProgressModalUITK.Instance.Open(gap, cowCompost, researchReport);
    }
}
