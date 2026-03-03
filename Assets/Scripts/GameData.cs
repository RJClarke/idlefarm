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
    }

    /// <summary>
    /// Constructor to create from current game state
    /// </summary>
    public GameData(int currentCoins)
    {
        coins = currentCoins;
    }
}