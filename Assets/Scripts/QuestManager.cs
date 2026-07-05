using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Pool")]
    [SerializeField] private List<QuestData> allQuests = new List<QuestData>();

    [Header("Settings")]
    [SerializeField] private int questsPerDrop = 2;
    [SerializeField] private int maxActiveQuests = 10;

    // Drop times in CT (hours): 6am, 12pm, 6pm, 12am
    private static readonly int[] DropHoursCT = { 0, 6, 12, 18 };

    // Milestone thresholds and rewards (index 0-7 → 5,10,15,20,25,30,35,40 quests)
    private static readonly int[] MilestoneThresholds = { 5, 10, 15, 20, 25, 30, 35, 40 };
    private static readonly int[] MilestoneGems = { 1, 1, 2, 2, 2, 2, 2, 10 };
    private static readonly int[] MilestoneCoins = { 50, 100, 150, 200, 200, 250, 250, 500 };

    // Runtime state
    private List<ActiveQuest> activeQuests = new List<ActiveQuest>();
    private int questsCompletedThisWeek;
    private bool[] weeklyMilestonesClaimed = new bool[8];
    private DateTime questWeekStart = DateTime.MinValue;
    private DateTime lastQuestDropTime = DateTime.MinValue;

    // questID -> QuestData, built once from allQuests (avoids a Find/closure per active quest per gameplay event).
    private Dictionary<string, QuestData> questsByID = new Dictionary<string, QuestData>();

    // RunStats.Instance is a session-long singleton, but RunManager.OnRunStarted fires once per
    // run (new run + resume). Guard so OnRunStarted doesn't re-subscribe lambda-free handlers onto
    // the same RunStats instance repeatedly, which would multiply IncrementProgress calls per run.
    private bool runStatsSubscribed;

    // Events
    public event Action OnQuestCompleted;
    public event Action OnQuestsDropped;
    public event Action OnMilestoneClaimed;

    // Public read-only access
    public int QuestsCompletedThisWeek => questsCompletedThisWeek;
    public bool[] WeeklyMilestonesClaimed => weeklyMilestonesClaimed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildQuestLookup();
    }

    // allQuests is populated by serialization before Awake runs and is never reassigned at
    // runtime; rebuild here anyway if that ever changes (e.g. a future hot-reload of the pool).
    private void BuildQuestLookup()
    {
        questsByID = new Dictionary<string, QuestData>(allQuests.Count);
        foreach (QuestData q in allQuests)
        {
            if (q == null || string.IsNullOrEmpty(q.questID)) continue;
            questsByID[q.questID] = q;
        }
    }

    private void Start()
    {
        SubscribeToEvents();
        CheckWeeklyReset();
        ProcessDrops();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            CheckWeeklyReset();
            ProcessDrops();
        }
    }

    // ── Scheduling ───────────────────────────────────────────────

    private void ProcessDrops()
    {
        DateTime nowUtc = DateTime.UtcNow;
        // Forward-only: if the device clock rolled back below the last drop time, clamp to now so
        // future drops resume from here instead of being frozen until real time passes the stale future stamp.
        if (lastQuestDropTime > nowUtc) lastQuestDropTime = nowUtc;
        TimeZoneInfo ct = GetCentralTime();
        DateTime nowCt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ct);

        // Build list of all drop times (UTC) for today and yesterday
        List<DateTime> recentDropsUtc = new List<DateTime>();
        for (int dayOffset = -1; dayOffset <= 0; dayOffset++)
        {
            DateTime day = nowCt.Date.AddDays(dayOffset);
            foreach (int hour in DropHoursCT)
            {
                DateTime dropCt = day.AddHours(hour);
                DateTime dropUtc = TimeZoneInfo.ConvertTimeToUtc(dropCt, ct);
                if (dropUtc <= nowUtc)
                    recentDropsUtc.Add(dropUtc);
            }
        }

        recentDropsUtc.Sort();

        // Count how many drop windows have passed since lastQuestDropTime
        int missedDrops = 0;
        DateTime latestDropUtc = lastQuestDropTime;
        foreach (DateTime drop in recentDropsUtc)
        {
            if (drop > lastQuestDropTime)
            {
                missedDrops++;
                latestDropUtc = drop;
            }
        }

        if (missedDrops == 0) return;

        int added = 0;
        for (int i = 0; i < missedDrops * questsPerDrop; i++)
        {
            if (ActiveQuestCount >= maxActiveQuests) break;
            QuestData picked = PickEligibleQuest();
            if (picked == null) break;
            activeQuests.Add(new ActiveQuest(picked.questID, latestDropUtc.ToString("o")));
            added++;
        }

        lastQuestDropTime = latestDropUtc;

        if (added > 0)
        {
            Debug.Log($"[Quests] Dropped {added} new quest(s).");
            OnQuestsDropped?.Invoke();
        }
    }

    private QuestData PickEligibleQuest()
    {
        HashSet<string> activeIDs = new HashSet<string>(activeQuests.Where(q => !q.isClaimed).Select(q => q.questID));
        List<QuestData> eligible = allQuests.Where(q =>
            !activeIDs.Contains(q.questID) && IsEligible(q)).ToList();

        if (eligible.Count == 0) return null;
        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    private bool IsEligible(QuestData quest)
    {
        if (!string.IsNullOrEmpty(quest.requiredUnlockID))
        {
            if (UpgradeManager.Instance == null ||
                UpgradeManager.Instance.GetPermanentLevel(quest.requiredUnlockID) <= 0)
                return false;
        }
        if (!string.IsNullOrEmpty(quest.requiredAnimalID))
        {
            if (AnimalManager.Instance == null ||
                !AnimalManager.Instance.IsUnlocked(quest.requiredAnimalID))
                return false;
        }
        return true;
    }

    // ── Weekly Reset ─────────────────────────────────────────────

    private void CheckWeeklyReset()
    {
        DateTime nowUtc = DateTime.UtcNow;
        TimeZoneInfo ct = GetCentralTime();
        DateTime nowCt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ct);

        // Sunday midnight CT = start of this week
        int daysSinceSunday = (int)nowCt.DayOfWeek;
        DateTime thisWeekStartCt = nowCt.Date.AddDays(-daysSinceSunday);
        DateTime thisWeekStartUtc = TimeZoneInfo.ConvertTimeToUtc(thisWeekStartCt, ct);

        if (questWeekStart < thisWeekStartUtc)
        {
            questsCompletedThisWeek = 0;
            weeklyMilestonesClaimed = new bool[8];
            questWeekStart = thisWeekStartUtc;
            Debug.Log("[Quests] Weekly milestone track reset.");
        }
    }

    // ── Public API ───────────────────────────────────────────────

    public List<ActiveQuest> GetActiveQuests()
    {
        return activeQuests.Where(q => !q.isClaimed).ToList();
    }

    public QuestData GetQuestData(string questID)
    {
        return questsByID.TryGetValue(questID, out QuestData data) ? data : null;
    }

    public bool TryClaimQuest(string questID)
    {
        ActiveQuest quest = activeQuests.Find(q => q.questID == questID && !q.isClaimed);
        if (quest == null || !quest.isCompleted) return false;

        if (!questsByID.TryGetValue(questID, out QuestData data)) return false;

        CurrencyManager.Instance?.AddCoins(data.coinReward);
        quest.isClaimed = true;
        questsCompletedThisWeek++;

        Debug.Log($"[Quests] Claimed quest '{data.displayName}' for {data.coinReward} coins. Week total: {questsCompletedThisWeek}");
        SaveManager.Instance?.SaveGame();
        OnQuestCompleted?.Invoke();
        return true;
    }

    public bool TryClaimMilestone(int tierIndex)
    {
        if (tierIndex < 0 || tierIndex >= 8) return false;
        if (weeklyMilestonesClaimed[tierIndex]) return false;
        if (questsCompletedThisWeek < MilestoneThresholds[tierIndex]) return false;

        CurrencyManager.Instance?.AddGems(MilestoneGems[tierIndex]);
        CurrencyManager.Instance?.AddCoins(MilestoneCoins[tierIndex]);
        weeklyMilestonesClaimed[tierIndex] = true;

        Debug.Log($"[Quests] Milestone {MilestoneThresholds[tierIndex]} claimed: {MilestoneGems[tierIndex]} gems, {MilestoneCoins[tierIndex]} coins.");
        SaveManager.Instance?.SaveGame();
        OnMilestoneClaimed?.Invoke();
        return true;
    }

    public DateTime GetNextDropTimeUtc()
    {
        TimeZoneInfo ct = GetCentralTime();
        DateTime nowCt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ct);

        foreach (int hour in DropHoursCT)
        {
            DateTime candidate = nowCt.Date.AddHours(hour);
            if (candidate > nowCt)
                return TimeZoneInfo.ConvertTimeToUtc(candidate, ct);
        }
        // Next drop is 6am tomorrow
        return TimeZoneInfo.ConvertTimeToUtc(nowCt.Date.AddDays(1).AddHours(6), ct);
    }

    public int ActiveQuestCount => activeQuests.Count(q => !q.isClaimed);
    public bool HasUnclaimedCompleted => activeQuests.Any(q => q.isCompleted && !q.isClaimed);
    public bool HasNewDrops => activeQuests.Any(q => q.progress == 0 && !q.isClaimed);

    // ── Save / Load ──────────────────────────────────────────────

    public ActiveQuest[] GetActiveQuestsForSave() => activeQuests.ToArray();
    public string GetQuestWeekStartISO() => questWeekStart == DateTime.MinValue ? "" : questWeekStart.ToString("o");
    public string GetLastQuestDropTimeISO() => lastQuestDropTime == DateTime.MinValue ? "" : lastQuestDropTime.ToString("o");

    public void LoadState(ActiveQuest[] quests, int completedCount, bool[] milestones,
        string weekStart, string lastDrop)
    {
        activeQuests = (quests ?? Array.Empty<ActiveQuest>()).ToList();
        questsCompletedThisWeek = completedCount;
        weeklyMilestonesClaimed = milestones ?? new bool[8];

        if (!string.IsNullOrEmpty(weekStart) && DateTime.TryParse(weekStart, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime ws))
            questWeekStart = ws;

        if (!string.IsNullOrEmpty(lastDrop) && DateTime.TryParse(lastDrop, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime ld))
            lastQuestDropTime = ld;
    }

    // ── Event Wiring ─────────────────────────────────────────────

    private void SubscribeToEvents()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted += OnRunStarted;
        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnEggClaimed += HandleEggClaimed;
            AnimalManager.Instance.OnGemClaimed += HandleGemClaimed;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted -= OnRunStarted;
        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnEggClaimed -= HandleEggClaimed;
            AnimalManager.Instance.OnGemClaimed -= HandleGemClaimed;
        }
        if (RunStats.Instance != null && runStatsSubscribed)
        {
            RunStats.Instance.OnCropHarvested -= HandleCropHarvested;
            RunStats.Instance.OnSeedPlanted   -= HandleSeedPlanted;
            RunStats.Instance.OnPlantWatered  -= HandlePlantWatered;
            RunStats.Instance.OnDeerRepelled  -= HandleDeerRepelled;
            RunStats.Instance.OnCrowRepelled  -= HandleCrowRepelled;
        }
        runStatsSubscribed = false;
    }

    // RunManager.OnRunStarted fires once per run (new run + resume), but RunStats.Instance is a
    // session-long singleton — only subscribe to it the first time so repeat run starts don't
    // stack duplicate (unremovable) handlers that would multiply IncrementProgress calls.
    private void OnRunStarted()
    {
        if (RunStats.Instance == null || runStatsSubscribed) return;
        RunStats.Instance.OnCropHarvested += HandleCropHarvested;
        RunStats.Instance.OnSeedPlanted   += HandleSeedPlanted;
        RunStats.Instance.OnPlantWatered  += HandlePlantWatered;
        RunStats.Instance.OnDeerRepelled  += HandleDeerRepelled;
        RunStats.Instance.OnCrowRepelled  += HandleCrowRepelled;
        runStatsSubscribed = true;
    }

    private void HandleEggClaimed() => IncrementProgress(QuestObjectiveType.GatherEggs);
    private void HandleGemClaimed() => IncrementProgress(QuestObjectiveType.GatherGems);
    private void HandleCropHarvested() => IncrementProgress(QuestObjectiveType.HarvestCrops);
    private void HandleSeedPlanted() => IncrementProgress(QuestObjectiveType.PlantSeeds);
    private void HandlePlantWatered() => IncrementProgress(QuestObjectiveType.WaterPlants);
    private void HandleDeerRepelled() => IncrementProgress(QuestObjectiveType.RepelDeer);
    private void HandleCrowRepelled() => IncrementProgress(QuestObjectiveType.RepelCrows);

    private static TimeZoneInfo GetCentralTime()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
    }

    private void IncrementProgress(QuestObjectiveType type)
    {
        bool anyCompleted = false;
        foreach (ActiveQuest quest in activeQuests)
        {
            if (quest.isClaimed || quest.isCompleted) continue;
            if (!questsByID.TryGetValue(quest.questID, out QuestData data) || data.objectiveType != type) continue;
            quest.progress++;
            if (quest.progress >= data.targetCount)
            {
                quest.isCompleted = true;
                anyCompleted = true;
                Debug.Log($"[Quests] Quest complete: {data.displayName}");
            }
        }
        if (anyCompleted) OnQuestCompleted?.Invoke();
    }

    // ── Debug / Test ─────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Debug: Complete All Active Quests")]
    public void DebugCompleteAll()
    {
        foreach (ActiveQuest quest in activeQuests)
        {
            if (quest.isClaimed || quest.isCompleted) continue;
            if (!questsByID.TryGetValue(quest.questID, out QuestData data)) continue;
            quest.progress = data.targetCount;
            quest.isCompleted = true;
        }
        OnQuestCompleted?.Invoke();
    }

    [ContextMenu("Debug: Claim All Completed Quests")]
    public void DebugClaimAll()
    {
        foreach (ActiveQuest quest in activeQuests.Where(q => q.isCompleted && !q.isClaimed).ToList())
            TryClaimQuest(quest.questID);
    }

    [ContextMenu("Debug: Force Drop Quests")]
    public void DebugForceDrop()
    {
        int added = 0;
        for (int i = 0; i < questsPerDrop; i++)
        {
            if (ActiveQuestCount >= maxActiveQuests) break;
            QuestData picked = PickEligibleQuest();
            if (picked == null) break;
            activeQuests.Add(new ActiveQuest(picked.questID, DateTime.UtcNow.ToString("o")));
            added++;
        }
        if (added > 0) OnQuestsDropped?.Invoke();
    }

    [ContextMenu("Debug: Claim All Milestones")]
    public void DebugClaimAllMilestones()
    {
        questsCompletedThisWeek = 40;
        for (int i = 0; i < 8; i++)
            weeklyMilestonesClaimed[i] = false;
        OnQuestCompleted?.Invoke();
    }

    [ContextMenu("Debug: Reset All Quest Progress")]
    public void DebugResetAllProgress()
    {
        activeQuests.Clear();
        questsCompletedThisWeek = 0;
        for (int i = 0; i < 8; i++)
            weeklyMilestonesClaimed[i] = false;
        lastQuestDropTime = DateTime.MinValue;
        questWeekStart = DateTime.MinValue;
        OnQuestsDropped?.Invoke();
        OnQuestCompleted?.Invoke();
        Debug.Log("[Quests] All quest progress reset.");
    }
#endif
}
