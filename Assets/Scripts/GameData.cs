using System;

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
    public string[] unlockedAnimalIDs;
    public string equippedAnimalID;
    public string lastEggClaimTime;

    // Daily quests
    public ActiveQuest[] activeQuests;
    public int questsCompletedThisWeek;
    public bool[] weeklyMilestonesClaimed;
    public string questWeekStart;
    public string lastQuestDropTime;

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
        unlockedAnimalIDs = new string[0];
        equippedAnimalID = "";
        lastEggClaimTime = "";
        activeQuests = new ActiveQuest[0];
        questsCompletedThisWeek = 0;
        weeklyMilestonesClaimed = new bool[8];
        questWeekStart = "";
        lastQuestDropTime = "";
    }

    /// <summary>
    /// Constructor to create from current game state
    /// </summary>
    public GameData(int currentCoins, int currentGems, string[] animalIDs, string equippedID, string eggTime,
        ActiveQuest[] quests, int questsCompleted, bool[] milestones, string weekStart, string lastDrop)
    {
        coins = currentCoins;
        gems = currentGems;
        unlockedAnimalIDs = animalIDs ?? new string[0];
        equippedAnimalID = equippedID ?? "";
        lastEggClaimTime = eggTime ?? "";
        activeQuests = quests ?? new ActiveQuest[0];
        questsCompletedThisWeek = questsCompleted;
        weeklyMilestonesClaimed = milestones ?? new bool[8];
        questWeekStart = weekStart ?? "";
        lastQuestDropTime = lastDrop ?? "";
    }
}