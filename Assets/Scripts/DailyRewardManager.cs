using UnityEngine;
using System;

/// <summary>
/// Manages daily login rewards on a weekly calendar (Sunday–Saturday).
/// Each day has an escalating coin reward. Claim only today's reward.
/// Missed days are gone. Claiming all 7 in a week grants a bonus.
/// Week resets automatically on Sunday.
/// </summary>
public class DailyRewardManager : MonoBehaviour
{
    public static DailyRewardManager Instance { get; private set; }

    [Header("Daily Rewards (Sun–Sat)")]
    [SerializeField] private int[] dailyRewards = new int[] { 10, 20, 30, 50, 75, 100, 150 };
    [SerializeField] private int weeklyBonusReward = 500;

    [Header("Daily Gem Rewards (Sun–Sat)")]
    [SerializeField] private int[] dailyGemRewards = new int[] { 0, 1, 0, 2, 0, 1, 0 };
    [SerializeField] private int weeklyGemBonus = 10;

    // PlayerPrefs keys
    private const string PREF_WEEK_START = "daily_reward_week_start";
    private const string PREF_CLAIMED_DAYS = "daily_reward_claimed_days";

    // Runtime state
    private DateTime currentWeekStart;
    private bool[] claimedDays = new bool[7];

    // Events
    public event Action OnRewardClaimed;
    public event Action OnWeekReset;

    public bool[] ClaimedDays => claimedDays;
    public int[] DailyRewards => dailyRewards;
    public int WeeklyBonusReward => weeklyBonusReward;
    public int WeeklyGemBonus => weeklyGemBonus;

    public int GetDailyGemReward(int dayIndex)
    {
        if (dayIndex < 0 || dayIndex >= dailyGemRewards.Length) return 0;
        return dailyGemRewards[dayIndex];
    }

    /// <summary>
    /// True if today's reward hasn't been claimed yet.
    /// </summary>
    public bool CanClaimToday
    {
        get
        {
            int todayIndex = GetTodayIndex();
            return todayIndex >= 0 && todayIndex < 7 && !claimedDays[todayIndex];
        }
    }

    /// <summary>
    /// How many days have been claimed this week.
    /// </summary>
    public int ClaimedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < 7; i++)
                if (claimedDays[i]) count++;
            return count;
        }
    }

    /// <summary>
    /// True if all 7 days were claimed this week (bonus earned).
    /// </summary>
    public bool EarnedWeeklyBonus => ClaimedCount >= 7;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadState();
        CheckWeekRollover();
    }

    /// <summary>
    /// Get the day index (0=Sun, 6=Sat) for today.
    /// </summary>
    public int GetTodayIndex()
    {
        return (int)DateTime.Now.DayOfWeek; // Sunday=0, Saturday=6
    }

    /// <summary>
    /// Get the day name for a given index.
    /// </summary>
    public string GetDayName(int index)
    {
        string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        if (index >= 0 && index < 7) return names[index];
        return "???";
    }

    /// <summary>
    /// Get the status of a day for display purposes.
    /// </summary>
    public DayStatus GetDayStatus(int dayIndex)
    {
        int today = GetTodayIndex();

        if (claimedDays[dayIndex])
            return DayStatus.Claimed;

        if (dayIndex == today)
            return DayStatus.Available;

        if (dayIndex < today)
            return DayStatus.Missed;

        return DayStatus.Upcoming;
    }

    /// <summary>
    /// Attempt to claim today's reward.
    /// </summary>
    public bool ClaimToday()
    {
        if (!CanClaimToday) return false;

        int todayIndex = GetTodayIndex();
        int reward = dailyRewards[todayIndex];

        // Grant coins
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCoins(reward);

        // Grant gems
        int gemReward = dailyGemRewards[todayIndex];
        if (gemReward > 0 && CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddGems(gemReward);

        claimedDays[todayIndex] = true;
        Debug.Log($"[Daily] Claimed day {GetDayName(todayIndex)} reward: {reward} coins" + (gemReward > 0 ? $", {gemReward} gems" : ""));

        // Check for weekly bonus
        if (EarnedWeeklyBonus)
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddCoins(weeklyBonusReward);
                if (weeklyGemBonus > 0)
                    CurrencyManager.Instance.AddGems(weeklyGemBonus);
            }
            Debug.Log($"[Daily] Weekly bonus earned: {weeklyBonusReward} coins, {weeklyGemBonus} gems!");
        }

        SaveState();
        OnRewardClaimed?.Invoke();
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Debug: wipe the current week's claim state so today becomes claimable again.
    /// Keeps the week-start date, just clears the claimed-days flags.
    /// </summary>
    public void DebugResetDaily()
    {
        for (int i = 0; i < claimedDays.Length; i++) claimedDays[i] = false;
        PlayerPrefs.DeleteKey(PREF_CLAIMED_DAYS);
        PlayerPrefs.DeleteKey(PREF_WEEK_START);
        PlayerPrefs.Save();
        currentWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        Debug.Log("[Daily] Debug: claim state reset.");
        OnWeekReset?.Invoke();
    }
#endif

    /// <summary>
    /// Check if the week has rolled over (it's a new week since last save).
    /// If so, reset all claimed days.
    /// </summary>
    private void CheckWeekRollover()
    {
        DateTime thisWeekStart = GetWeekStart(DateTime.Now);

        if (thisWeekStart > currentWeekStart)
        {
            // New week — reset
            currentWeekStart = thisWeekStart;
            claimedDays = new bool[7];
            SaveState();
            OnWeekReset?.Invoke();
            Debug.Log("[Daily] New week started, rewards reset");
        }
    }

    /// <summary>
    /// Get the Sunday that starts the week containing the given date.
    /// </summary>
    private DateTime GetWeekStart(DateTime date)
    {
        int diff = (int)date.DayOfWeek; // Sunday=0
        return date.Date.AddDays(-diff);
    }

    private void SaveState()
    {
        PlayerPrefs.SetString(PREF_WEEK_START, currentWeekStart.ToString("yyyy-MM-dd"));

        string claimed = "";
        for (int i = 0; i < 7; i++)
        {
            if (i > 0) claimed += ",";
            claimed += claimedDays[i] ? "1" : "0";
        }
        PlayerPrefs.SetString(PREF_CLAIMED_DAYS, claimed);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        // Load week start
        string weekStr = PlayerPrefs.GetString(PREF_WEEK_START, "");
        if (!string.IsNullOrEmpty(weekStr) && DateTime.TryParse(weekStr, out DateTime parsed))
            currentWeekStart = parsed;
        else
            currentWeekStart = GetWeekStart(DateTime.Now);

        // Load claimed days
        claimedDays = new bool[7];
        string claimedStr = PlayerPrefs.GetString(PREF_CLAIMED_DAYS, "");
        if (!string.IsNullOrEmpty(claimedStr))
        {
            string[] parts = claimedStr.Split(',');
            for (int i = 0; i < Mathf.Min(parts.Length, 7); i++)
                claimedDays[i] = parts[i] == "1";
        }
    }
}

public enum DayStatus
{
    Claimed,    // Already collected
    Available,  // Today — can claim
    Missed,     // Past day, not claimed
    Upcoming    // Future day
}
