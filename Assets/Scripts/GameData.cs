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
    }

    /// <summary>
    /// Constructor to create from current game state
    /// </summary>
    public GameData(
        int currentCoins, int currentGems, int currentCompost,
        string[] animalIDs, string equippedID, string eggTime,
        ActiveQuest[] quests, int questsCompleted, bool[] milestones, string weekStart, string lastDrop,
        bool[] researchUnlocked, ResearchSlotState[] researchSlotsIn, string[] featureFlags, ResearchLevelEntry[] levels)
    {
        coins = currentCoins;
        gems = currentGems;
        compost = currentCompost;
        unlockedAnimalIDs = animalIDs ?? new string[0];
        equippedAnimalID = equippedID ?? "";
        lastEggClaimTime = eggTime ?? "";
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
}
