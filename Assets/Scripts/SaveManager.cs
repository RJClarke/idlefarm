using UnityEngine;
using System.IO;

/// <summary>
/// Handles saving and loading game data
/// Uses JSON format stored in persistent data path (works on mobile)
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool enableAutoSave = true;

    private string saveFilePath;
    private const string SAVE_FILE_NAME = "gamedata.json";

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Set save file path (works on mobile)
        saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        // Save file path set
    }

    /// <summary>
    /// Save current game data to disk
    /// </summary>
    public void SaveGame()
    {
        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("Cannot save: CurrencyManager not found!");
            return;
        }

        // Create data object from current game state
        string[] animalIDs = new string[0];
        string equippedID = "";
        string eggTime = "";

        if (AnimalManager.Instance != null)
        {
            animalIDs = AnimalManager.Instance.GetUnlockedAnimalIDs();
            equippedID = AnimalManager.Instance.GetEquippedAnimalID();
            eggTime = AnimalManager.Instance.GetLastEggClaimTimeISO();
        }

        ActiveQuest[] quests = new ActiveQuest[0];
        int questsCompleted = 0;
        bool[] milestones = new bool[8];
        string weekStart = "";
        string lastDrop = "";

        if (QuestManager.Instance != null)
        {
            quests = QuestManager.Instance.GetActiveQuestsForSave();
            questsCompleted = QuestManager.Instance.QuestsCompletedThisWeek;
            milestones = QuestManager.Instance.WeeklyMilestonesClaimed;
            weekStart = QuestManager.Instance.GetQuestWeekStartISO();
            lastDrop = QuestManager.Instance.GetLastQuestDropTimeISO();
        }

        bool[] researchUnlocked = new bool[ResearchManager.SlotCount];
        Research.ResearchSlotState[] researchSlots = null;
        string[] featureFlags = new string[0];
        ResearchLevelEntry[] researchLevels = new ResearchLevelEntry[0];
        if (ResearchManager.Instance != null)
        {
            researchUnlocked = ResearchManager.Instance.GetSlotsUnlockedForSave();
            researchSlots    = ResearchManager.Instance.GetSlotsForSave();
            featureFlags     = ResearchManager.Instance.GetFeatureFlagsForSave();
            researchLevels   = ResearchManager.Instance.GetLevelsForSave();
        }

        GameData data = new GameData(
            CurrencyManager.Instance.Coins,
            CurrencyManager.Instance.Gems,
            animalIDs,
            equippedID,
            eggTime,
            quests,
            questsCompleted,
            milestones,
            weekStart,
            lastDrop,
            researchUnlocked,
            researchSlots,
            featureFlags,
            researchLevels
        );

        // Convert to JSON
        string json = JsonUtility.ToJson(data, true); // true = pretty print for debugging

        // Write to file
        try
        {
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"Game saved! Coins: {data.coins}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save game: {e.Message}");
        }
    }

    /// <summary>
    /// Load game data from disk
    /// Returns true if load was successful
    /// </summary>
    public bool LoadGame()
    {
        // Check if save file exists
        if (!File.Exists(saveFilePath))
        {
            Debug.Log("No save file found. Starting new game.");
            return false;
        }

        try
        {
            // Read JSON from file
            string json = File.ReadAllText(saveFilePath);

            // Convert from JSON to GameData object
            GameData data = JsonUtility.FromJson<GameData>(json);

            // Apply loaded data to game
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.SetCoins(data.coins);
                CurrencyManager.Instance.SetGems(data.gems);

                if (AnimalManager.Instance != null)
                {
                    AnimalManager.Instance.LoadState(data.unlockedAnimalIDs, data.equippedAnimalID, data.lastEggClaimTime);
                }

                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.LoadState(
                        data.activeQuests,
                        data.questsCompletedThisWeek,
                        data.weeklyMilestonesClaimed,
                        data.questWeekStart,
                        data.lastQuestDropTime
                    );
                }

                if (ResearchManager.Instance != null)
                {
                    ResearchManager.Instance.LoadState(
                        data.researchSlotsUnlocked,
                        data.researchSlots,
                        data.binaryFeatureFlagsSet,
                        data.researchLevels
                    );
                }

                Debug.Log($"Game loaded! Coins: {data.coins}");
            }
            else
            {
                Debug.LogWarning("CurrencyManager not found during load. Data will be applied when available.");
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load game: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete save file (useful for testing or "reset game" feature)
    /// </summary>
    public void DeleteSave()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.Log("No save file to delete.");
        }
    }

    /// <summary>
    /// Check if a save file exists
    /// </summary>
    public bool SaveFileExists()
    {
        return File.Exists(saveFilePath);
    }

    #region Auto-Save on Application Events

    private void OnApplicationQuit()
    {
        if (!enableAutoSave)
        {
            return;
        }

        // Auto-save when game closes
        SaveGame();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!enableAutoSave) return;

        // Auto-save when app is paused (important for mobile!)
        if (pause)
        {
            SaveGame();
        }
    }

    #endregion

    #region Testing/Debug Methods

    [ContextMenu("Save Game Now")]
    private void TestSave()
    {
        SaveGame();
    }

    [ContextMenu("Load Game Now")]
    private void TestLoad()
    {
        LoadGame();
    }

    [ContextMenu("Delete Save File")]
    private void TestDelete()
    {
        DeleteSave();
    }

    [ContextMenu("Check Save File Exists")]
    private void TestExists()
    {
        Debug.Log($"Save file exists: {SaveFileExists()}");
    }

    #endregion
}