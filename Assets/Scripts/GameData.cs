using System;
using Research;

/// <summary>
/// Data structure that defines what gets saved/loaded
/// This will expand as we add more features (unlocks, upgrades, etc.)
/// </summary>
[Serializable]
public class GameData
{
    // Permanent currency (persists between runs)
    public int coins;
    public int gems;
    public int compost;
    public string[] unlockedAnimalIDs;
    public string equippedAnimalID;
    public string lastEggClaimTime;
    public string lastCompostClaimTime;

    // Daily quests
    public ActiveQuest[] activeQuests;
    public int questsCompletedThisWeek;
    public bool[] weeklyMilestonesClaimed;
    public string questWeekStart;
    public string lastQuestDropTime;

    // Research (Plan 1)
    public bool[] researchSlotsUnlocked;           // length 4
    public ResearchSlotState[] researchSlots;     // length 4, idle if startUtcTicks == 0
    public string[] binaryFeatureFlagsSet;        // ids that have been unlocked, e.g. "offline_progress"
    public ResearchLevelEntry[] researchLevels;   // serializable view of researchID -> currentLevel

    // Wall-clock anchor for offline-progress catch-up. Set on every save; read on load
    // to compute "time away" for the welcome-back modal.
    public long lastSeenUtcTicks;

    // Permanent upgrade levels (UpgradeManager). Flat array because JsonUtility can't
    // serialize Dictionary directly. Defaults to empty for new saves.
    public UpgradeLevelEntry[] permanentUpgradeLevels;

    // Purchased helper upgrades (HelperUpgradeManager). One entry per purchased ID.
    public string[] purchasedHelperUpgradeIDs;

    // Content IDs the player has already seen (NewContentTracker), e.g. "research:scarecrow_aoe",
    // "equip:scarecrow". Used to drive NEW badges. Empty on a new/legacy save → tracker seeds a baseline.
    public string[] seenContentIds;

    // Narrative one-shot ledger (NarrativeManager) + the player's farm/account name.
    public string farmName;
    public string[] firedNarrativeFlags;

    // Received inbox letters (InboxManager). State only; content is looked up from the catalog by id.
    public InboxEntry[] inboxLetters;

    // Active-run snapshot. If runActive=true on load, SaveManager calls RunManager.ResumeRun
    // with runStartUtcTicks and restores money + temporaryUpgradeLevels. Tactical state
    // (tiles, plants, helpers, threats) is not saved — those reset on resume.
    public bool runActive;
    public long runStartUtcTicks;
    public float runTotalSeconds; // Total (speed-scaled) run time at save; restored as the resume baseline
    public int money;
    public UpgradeLevelEntry[] temporaryUpgradeLevels;

    // Later we'll add:
    // - List of unlocked crops
    // - List of purchased upgrades
    // - Number of permanently tilled soil tiles
    // - Player statistics (total runs, best run, etc.)

    /// <summary>
    /// Constructor with default values for new game
    /// </summary>
    public GameData()
    {
        coins = 0;
        gems = 0;
        compost = 0;
        unlockedAnimalIDs = new string[0];
        equippedAnimalID = "";
        lastEggClaimTime = "";
        lastCompostClaimTime = "";
        activeQuests = new ActiveQuest[0];
        questsCompletedThisWeek = 0;
        weeklyMilestonesClaimed = new bool[8];
        questWeekStart = "";
        lastQuestDropTime = "";

        researchSlotsUnlocked = new bool[4];
        researchSlots = new ResearchSlotState[4];
        for (int i = 0; i < 4; i++) researchSlots[i] = new ResearchSlotState { slotIndex = i };
        binaryFeatureFlagsSet = new string[0];
        researchLevels = new ResearchLevelEntry[0];
        seenContentIds = new string[0];
        farmName = "";
        firedNarrativeFlags = new string[0];
        inboxLetters = new InboxEntry[0];
    }

    /// <summary>
    /// Constructor to create from current game state
    /// </summary>
    public GameData(
        int currentCoins, int currentGems, int currentCompost,
        string[] animalIDs, string equippedID, string eggTime, string compostTime,
        ActiveQuest[] quests, int questsCompleted, bool[] milestones, string weekStart, string lastDrop,
        bool[] researchUnlocked, ResearchSlotState[] researchSlotsIn, string[] featureFlags, ResearchLevelEntry[] levels)
    {
        coins = currentCoins;
        gems = currentGems;
        compost = currentCompost;
        unlockedAnimalIDs = animalIDs ?? new string[0];
        equippedAnimalID = equippedID ?? "";
        lastEggClaimTime = eggTime ?? "";
        lastCompostClaimTime = compostTime ?? "";
        activeQuests = quests ?? new ActiveQuest[0];
        questsCompletedThisWeek = questsCompleted;
        weeklyMilestonesClaimed = milestones ?? new bool[8];
        questWeekStart = weekStart ?? "";
        lastQuestDropTime = lastDrop ?? "";
        researchSlotsUnlocked = researchUnlocked ?? new bool[4];
        researchSlots = researchSlotsIn ?? new ResearchSlotState[4];
        binaryFeatureFlagsSet = featureFlags ?? new string[0];
        researchLevels = levels ?? new ResearchLevelEntry[0];
    }
}

/// <summary>
/// Serializable key/value entry for the researchID -> currentLevel map.
/// JsonUtility doesn't serialize Dictionary, so we flatten to an array.
/// </summary>
[Serializable]
public class ResearchLevelEntry
{
    public string researchID;
    public int level;
    public float partialSecs; // seconds already accumulated toward the next level (cost already paid)
}

/// <summary>
/// Serializable key/value entry for the upgradeID -> level map used by UpgradeManager.
/// </summary>
[Serializable]
public class UpgradeLevelEntry
{
    public string upgradeID;
    public int level;
}
